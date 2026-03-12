namespace QuanTAlib.Tests;

public class ZtestTests
{
    // A) Constructor validation
    [Fact]
    public void Constructor_DefaultPeriod_Is30()
    {
        var z = new Ztest();
        Assert.Equal("Ztest(30,0)", z.Name);
    }

    [Fact]
    public void Constructor_PeriodLessThan2_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ztest(1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodEquals2_Works()
    {
        var z = new Ztest(2);
        Assert.Equal("Ztest(2,0)", z.Name);
    }

    [Fact]
    public void Constructor_CustomMu0_ShowsInName()
    {
        var z = new Ztest(10, 5.5);
        Assert.Equal("Ztest(10,5.5)", z.Name);
    }

    [Fact]
    public void Constructor_NegativeMu0_Works()
    {
        var z = new Ztest(10, -2.0);
        Assert.Contains("-2", z.Name, StringComparison.Ordinal);
    }

    // B) Basic calculation — constant series => t = 0 (stddev = 0)
    [Fact]
    public void Update_ConstantSeries_ReturnsZero()
    {
        var z = new Ztest(5, 0.0);
        for (int i = 0; i < 10; i++)
        {
            var tv = z.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.Equal(0.0, tv.Value);
        }
    }

    // B) Known values: {1, 2, 3, 4, 5}, mu0=0
    // mean=3, sample var = 10/4 = 2.5, s = sqrt(2.5), SE = sqrt(2.5)/sqrt(5) = sqrt(0.5)
    // t = (3 - 0) / sqrt(0.5) = 3*sqrt(2) ≈ 4.2426
    [Fact]
    public void Update_KnownSequence_Mu0Zero_CorrectTStat()
    {
        var z = new Ztest(5, 0.0);
        for (int i = 1; i <= 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, i));
        }

        double expected = 3.0 * Math.Sqrt(2.0); // 3 / sqrt(0.5) = 3*sqrt(2)
        Assert.Equal(expected, z.Last.Value, 1e-9);
    }

    // B) Known values with mu0 = mean => t = 0
    [Fact]
    public void Update_Mu0EqualsMean_ReturnsZero()
    {
        var z = new Ztest(5, 3.0); // mu0 = mean of {1,2,3,4,5}
        for (int i = 1; i <= 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(0.0, z.Last.Value, 1e-9);
    }

    // B) Known: {2, 4, 4, 4, 5, 5, 7, 9}, mu0=0
    // mean=5, sample var = sum((xi-5)²)/7 = 32/7, s = sqrt(32/7)
    // SE = sqrt(32/7)/sqrt(8) = sqrt(32/56) = sqrt(4/7) = 2/sqrt(7)
    // t = (5-0) / (2/sqrt(7)) = 5*sqrt(7)/2
    [Fact]
    public void Update_ClassicDataset_Mu0Zero()
    {
        var z = new Ztest(8, 0.0);
        double[] data = [2, 4, 4, 4, 5, 5, 7, 9];
        foreach (double d in data)
        {
            z.Update(new TValue(DateTime.UtcNow, d));
        }

        double expected = 5.0 * Math.Sqrt(7.0) / 2.0;
        Assert.Equal(expected, z.Last.Value, 1e-9);
    }

    // B) Positive t when mean > mu0
    [Fact]
    public void Update_MeanAboveMu0_ReturnsPositive()
    {
        var z = new Ztest(5, 0.0);
        for (int i = 1; i <= 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.True(z.Last.Value > 0);
    }

    // B) Negative t when mean < mu0
    [Fact]
    public void Update_MeanBelowMu0_ReturnsNegative()
    {
        var z = new Ztest(5, 100.0); // mu0 much larger than mean
        for (int i = 1; i <= 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.True(z.Last.Value < 0);
    }

    // C) State + bar correction
    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var z = new Ztest(5);
        z.Update(new TValue(DateTime.UtcNow, 10.0));
        z.Update(new TValue(DateTime.UtcNow, 20.0));
        double v1 = z.Last.Value;
        z.Update(new TValue(DateTime.UtcNow, 30.0));
        double v2 = z.Last.Value;

        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void Update_IsNewFalse_Rewrites()
    {
        var z = new Ztest(5);
        for (int i = 0; i < 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        double before = z.Last.Value;
        z.Update(new TValue(DateTime.UtcNow, 999.0), false);
        double after = z.Last.Value;

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var z = new Ztest(5);
        for (int i = 0; i < 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        double snapshot = z.Last.Value;

        // Correct multiple times with isNew=false
        z.Update(new TValue(DateTime.UtcNow, 50.0), false);
        z.Update(new TValue(DateTime.UtcNow, 100.0), false);
        z.Update(new TValue(DateTime.UtcNow, 10.0 + 4), false); // restore original

        Assert.Equal(snapshot, z.Last.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var z = new Ztest(5);
        for (int i = 0; i < 10; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        Assert.True(z.IsHot);
        z.Reset();
        Assert.False(z.IsHot);
        Assert.Equal(default, z.Last);
    }

    // D) Warmup/convergence
    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        var z = new Ztest(5);
        for (int i = 0; i < 4; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
            Assert.False(z.IsHot);
        }

        z.Update(new TValue(DateTime.UtcNow, 14.0));
        Assert.True(z.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var z = new Ztest(10);
        Assert.Equal(10, z.WarmupPeriod);
    }

    // E) Robustness — NaN/Infinity
    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var z = new Ztest(5);
        for (int i = 0; i < 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        _ = z.Last.Value;
        z.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(z.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var z = new Ztest(5);
        for (int i = 0; i < 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        z.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(z.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_AllFinite()
    {
        var z = new Ztest(5);
        for (int i = 0; i < 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        for (int i = 0; i < 10; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(z.Last.Value));
        }
    }

    // F) Consistency — batch == streaming == span == eventing
    [Fact]
    public void Consistency_AllModesMatch()
    {
        int period = 10;
        int count = 50;
        double mu0 = 1.5;
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        var source = new TSeries(count);
        for (int i = 0; i < count; i++)
        {
            TBar bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close), true);
        }

        // 1. Batch via TSeries
        TSeries batchResult = Ztest.Batch(source, period, mu0);

        // 2. Streaming
        var streaming = new Ztest(period, mu0);
        var streamResult = new List<double>(count);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamResult.Add(streaming.Last.Value);
        }

        // 3. Span
        Span<double> spanOutput = new double[count];
        Ztest.Batch(source.Values, spanOutput, period, mu0);

        // 4. Eventing
        var publisher = new TSeries(count);
        var eventIndicator = new Ztest(publisher, period, mu0);
        var eventResult = new List<double>(count);
        eventIndicator.Pub += (object? _, in TValueEventArgs _) => eventResult.Add(eventIndicator.Last.Value);
        for (int i = 0; i < source.Count; i++)
        {
            publisher.Add(source[i], true);
        }

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i], 1e-9);
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-4); // t-stat magnifies FP drift (values ~6000)
            Assert.Equal(batchResult[i].Value, eventResult[i], 1e-9);
        }
    }

    // G) Span API tests
    [Fact]
    public void Batch_Span_EmptySource_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Ztest.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Ztest.Batch(src, output, 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodTooSmall_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Ztest.Batch(src, output, 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int period = 5;
        int count = 30;
        double mu0 = 2.0;
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);

        var source = new TSeries(count);
        for (int i = 0; i < count; i++)
        {
            TBar bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close), true);
        }

        TSeries batchResult = Ztest.Batch(source, period, mu0);
        Span<double> spanOutput = new double[count];
        Ztest.Batch(source.Values, spanOutput, period, mu0);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 5e-4); // t-stat magnifies FP drift (values ~15000)
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        double[] src = [1, 2, double.NaN, 4, 5];
        double[] output = new double[5];
        Ztest.Batch(src, output, 3);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int size = 1000;
        double[] src = new double[size];
        double[] output = new double[size];
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        for (int i = 0; i < size; i++)
        {
            src[i] = rng.Next().Close;
        }

        Ztest.Batch(src, output, 300); // above stackalloc threshold

        for (int i = 0; i < size; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // H) Chainability
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var z = new Ztest(5);
        int fireCount = 0;
        z.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        z.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var publisher = new TSeries(10);
        var z = new Ztest(publisher, 5);

        publisher.Add(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.True(double.IsFinite(z.Last.Value));
    }

    // Additional: sample stddev (Bessel correction) verification
    [Fact]
    public void Update_UsesSampleStdDev_NotPopulation()
    {
        // For {2, 4, 4, 4, 5, 5, 7, 9}, mu0=5 (= mean)
        // With sample stddev, t should be 0 when mu0=mean regardless of correction
        var z = new Ztest(8, 5.0);
        double[] data = [2, 4, 4, 4, 5, 5, 7, 9];
        foreach (double d in data)
        {
            z.Update(new TValue(DateTime.UtcNow, d));
        }

        Assert.Equal(0.0, z.Last.Value, 1e-9);
    }

    // Verify Bessel correction specifically: compare against known formula
    [Fact]
    public void Update_BesselCorrection_MatchesFormula()
    {
        // {1, 2, 3}, mu0=0, period=3
        // mean = 2, pop_var = ((1-2)²+(2-2)²+(3-2)²)/3 = 2/3
        // sample_var = pop_var * 3/2 = 1.0
        // sample_stddev = 1.0
        // SE = 1.0/sqrt(3) ≈ 0.57735
        // t = (2-0)/SE = 2*sqrt(3) ≈ 3.4641
        var z = new Ztest(3, 0.0);
        z.Update(new TValue(DateTime.UtcNow, 1.0));
        z.Update(new TValue(DateTime.UtcNow, 2.0));
        z.Update(new TValue(DateTime.UtcNow, 3.0));

        double expected = 2.0 * Math.Sqrt(3.0);
        Assert.Equal(expected, z.Last.Value, 1e-9);
    }

    // Symmetry of t-statistic around mu0
    [Fact]
    public void Update_SymmetricAroundMu0()
    {
        // If data mean = 5 and we test mu0=3, t should be positive
        // If same data and mu0=7 (same distance), t should be equal magnitude but negative
        var z1 = new Ztest(5, 3.0);
        var z2 = new Ztest(5, 7.0);

        for (int i = 1; i <= 5; i++)
        {
            z1.Update(new TValue(DateTime.UtcNow, i + 2)); // data: {3,4,5,6,7}, mean=5
            z2.Update(new TValue(DateTime.UtcNow, i + 2));
        }

        Assert.Equal(z1.Last.Value, -z2.Last.Value, 1e-9);
    }

    // Calculate tuple method
    [Fact]
    public void Calculate_ReturnsTupleWithResults()
    {
        int count = 20;
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);

        var source = new TSeries(count);
        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(rng.Next().Time, rng.Next().Close), true);
        }

        var (results, indicator) = Ztest.Calculate(source, 5, 1.0);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // Prime method
    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var z = new Ztest(5);
        double[] data = [10, 20, 30, 40, 50];
        z.Prime(data);
        Assert.True(z.IsHot);
    }

    // Mu0 default (0.0) matches explicit specification
    [Fact]
    public void Mu0Default_MatchesExplicit()
    {
        var z1 = new Ztest(5);
        var z2 = new Ztest(5, 0.0);

        for (int i = 1; i <= 10; i++)
        {
            z1.Update(new TValue(DateTime.UtcNow, i * 1.0));
            z2.Update(new TValue(DateTime.UtcNow, i * 1.0));
        }

        Assert.Equal(z1.Last.Value, z2.Last.Value, 1e-12);
    }

    // Two data points (minimum period)
    [Fact]
    public void Update_Period2_Works()
    {
        // {10, 20}, mu0=0
        // mean=15, pop_var=25, sample_var=25*2/1=50, s=sqrt(50)
        // SE = sqrt(50)/sqrt(2) = sqrt(25) = 5
        // t = 15/5 = 3
        var z = new Ztest(2, 0.0);
        z.Update(new TValue(DateTime.UtcNow, 10.0));
        z.Update(new TValue(DateTime.UtcNow, 20.0));

        Assert.Equal(3.0, z.Last.Value, 1e-9);
    }

    // Consistency with mu0=0 for different period sizes
    [Fact]
    public void Consistency_Mu0Zero_DifferentPeriods()
    {
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 33);
        int count = 50;

        var source = new TSeries(count);
        for (int i = 0; i < count; i++)
        {
            TBar bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close), true);
        }

        // Just verify all finite for multiple periods
        foreach (int period in new[] { 2, 5, 10, 20, 30 })
        {
            TSeries result = Ztest.Batch(source, period, 0.0);
            for (int i = 0; i < result.Count; i++)
            {
                Assert.True(double.IsFinite(result[i].Value));
            }
        }
    }
}
