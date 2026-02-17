namespace QuanTAlib.Tests;

public class ZscoreTests
{
    // A) Constructor validation
    [Fact]
    public void Constructor_DefaultPeriod_Is14()
    {
        var z = new Zscore();
        Assert.Equal("Zscore(14)", z.Name);
    }

    [Fact]
    public void Constructor_PeriodLessThan2_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Zscore(1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodEquals2_Works()
    {
        var z = new Zscore(2);
        Assert.Equal("Zscore(2)", z.Name);
    }

    // B) Basic calculation — constant series => z = 0
    [Fact]
    public void Update_ConstantSeries_ReturnsZero()
    {
        var z = new Zscore(5);
        for (int i = 0; i < 10; i++)
        {
            var tv = z.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.Equal(0.0, tv.Value);
        }
    }

    // B) Known values: {1, 2, 3, 4, 5} => z(5) = (5 - 3) / sqrt(2) ≈ 1.4142
    [Fact]
    public void Update_KnownSequence_CorrectZScore()
    {
        var z = new Zscore(5);
        for (int i = 1; i <= 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, i));
        }

        // mean = 3, pop variance = ((1-3)²+(2-3)²+(3-3)²+(4-3)²+(5-3)²)/5 = 10/5 = 2
        // sigma = sqrt(2) ≈ 1.4142
        // z(5) = (5 - 3) / sqrt(2) = 2/sqrt(2) = sqrt(2) ≈ 1.4142
        double expected = Math.Sqrt(2.0);
        Assert.Equal(expected, z.Last.Value, 1e-9);
    }

    // B) Check z-score of mean value = 0
    [Fact]
    public void Update_MeanValue_ReturnsZero()
    {
        var z = new Zscore(3);
        z.Update(new TValue(DateTime.UtcNow, 10.0));
        z.Update(new TValue(DateTime.UtcNow, 20.0));
        var result = z.Update(new TValue(DateTime.UtcNow, 15.0));

        // mean of {10, 20, 15} = 15, so z(15) = 0
        Assert.Equal(0.0, result.Value, 1e-9);
    }

    // B) Negative z-score for below-mean value
    [Fact]
    public void Update_BelowMean_ReturnsNegative()
    {
        var z = new Zscore(5);
        for (int i = 1; i <= 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, i));
        }

        // Replace last with value 1 (below mean=3)
        var result = z.Update(new TValue(DateTime.UtcNow, 1.0));
        Assert.True(result.Value < 0);
    }

    // C) State + bar correction
    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var z = new Zscore(5);
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
        var z = new Zscore(5);
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
        var z = new Zscore(5);
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
        var z = new Zscore(5);
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
        var z = new Zscore(5);
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
        var z = new Zscore(10);
        Assert.Equal(10, z.WarmupPeriod);
    }

    // E) Robustness — NaN/Infinity
    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var z = new Zscore(5);
        for (int i = 0; i < 5; i++)
        {
            z.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        _ = z.Last.Value;
        z.Update(new TValue(DateTime.UtcNow, double.NaN));

        // NaN substituted with last valid — result may differ but should be finite
        Assert.True(double.IsFinite(z.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var z = new Zscore(5);
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
        var z = new Zscore(5);
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
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        var source = new TSeries(count);
        for (int i = 0; i < count; i++)
        {
            TBar bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close), true);
        }

        // 1. Batch via TSeries
        TSeries batchResult = Zscore.Batch(source, period);

        // 2. Streaming
        var streaming = new Zscore(period);
        var streamResult = new List<double>(count);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamResult.Add(streaming.Last.Value);
        }

        // 3. Span
        Span<double> spanOutput = new double[count];
        Zscore.Batch(source.Values, spanOutput, period);

        // 4. Eventing
        var publisher = new TSeries(count);
        var eventIndicator = new Zscore(publisher, period);
        var eventResult = new List<double>(count);
        eventIndicator.Pub += (object? _, in TValueEventArgs _) => eventResult.Add(eventIndicator.Last.Value);
        for (int i = 0; i < source.Count; i++)
        {
            publisher.Add(source[i], true);
        }

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i], 1e-9);
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-8); // FP addition order differs between ring scan paths
            Assert.Equal(batchResult[i].Value, eventResult[i], 1e-9);
        }
    }

    // G) Span API tests
    [Fact]
    public void Batch_Span_EmptySource_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Zscore.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Zscore.Batch(src, output, 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodTooSmall_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Zscore.Batch(src, output, 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int period = 5;
        int count = 30;
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);

        var source = new TSeries(count);
        for (int i = 0; i < count; i++)
        {
            TBar bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close), true);
        }

        TSeries batchResult = Zscore.Batch(source, period);
        Span<double> spanOutput = new double[count];
        Zscore.Batch(source.Values, spanOutput, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-8); // FP addition order differs between ring scan paths
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        ReadOnlySpan<double> src = stackalloc double[] { 1, 2, double.NaN, 4, 5 };
        Span<double> output = stackalloc double[5];
        Zscore.Batch(src, output, 3);

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

        Zscore.Batch(src, output, 300); // above stackalloc threshold

        for (int i = 0; i < size; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // H) Chainability
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var z = new Zscore(5);
        int fireCount = 0;
        z.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        z.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var publisher = new TSeries(10);
        var z = new Zscore(publisher, 5);

        publisher.Add(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.True(double.IsFinite(z.Last.Value));
    }

    // Additional: population stddev vs sample stddev distinction
    [Fact]
    public void Update_UsesPopulationStdDev()
    {
        // For data {2, 4, 4, 4, 5, 5, 7, 9}, population σ = 2
        // Population mean = 5, pop variance = 4, σ = 2
        // z(9) = (9 - 5) / 2 = 2.0
        var z = new Zscore(8);
        double[] data = [2, 4, 4, 4, 5, 5, 7, 9];
        foreach (double d in data)
        {
            z.Update(new TValue(DateTime.UtcNow, d));
        }

        Assert.Equal(2.0, z.Last.Value, 1e-9);
    }

    // Symmetry: z-score of min value should be negative of z-score of max value for symmetric data
    [Fact]
    public void Update_SymmetricData_SymmetricZScores()
    {
        // {1, 2, 3, 4, 5} => z(1) = -sqrt(2), z(5) = +sqrt(2)
        var z1 = new Zscore(5);
        for (int i = 1; i <= 5; i++)
        {
            z1.Update(new TValue(DateTime.UtcNow, i));
        }

        double zMax = z1.Last.Value; // z(5)

        var z2 = new Zscore(5);
        for (int i = 5; i >= 1; i--)
        {
            z2.Update(new TValue(DateTime.UtcNow, i));
        }

        double zMin = z2.Last.Value; // z(1) with reversed input

        Assert.Equal(zMax, -zMin, 1e-9);
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

        var (results, indicator) = Zscore.Calculate(source, 5);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // Prime method
    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var z = new Zscore(5);
        double[] data = [10, 20, 30, 40, 50];
        z.Prime(data);
        Assert.True(z.IsHot);
    }
}
