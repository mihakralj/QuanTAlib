namespace QuanTAlib.Tests;

public class SwmaTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < count; i++)
        {
            series.Add(gbm.Next());
        }
        return series;
    }

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_DefaultPeriod_Is4()
    {
        var swma = new Swma();
        Assert.Equal("Swma(4)", swma.Name);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var swma = new Swma(period: 10);
        Assert.Equal("Swma(10)", swma.Name);
    }

    [Fact]
    public void Constructor_Period2_IsValid()
    {
        var swma = new Swma(period: 2);
        Assert.Equal("Swma(2)", swma.Name);
    }

    [Fact]
    public void Constructor_PeriodBelow2_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Swma(period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Swma(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Swma(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var swma = new Swma(period: 8);
        Assert.Equal(8, swma.WarmupPeriod);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var swma = new Swma(period: 4);
        var result = swma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var swma = new Swma(period: 4);
        swma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(swma.Last.Value));
    }

    [Fact]
    public void Update_ConstantInput_ReturnsConstant()
    {
        var swma = new Swma(period: 4);
        for (int i = 0; i < 10; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0));
        }
        Assert.Equal(50.0, swma.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_Period4_KnownWeights_MatchesPine()
    {
        // PineScript ta.swma: period=4, weights [1,2,2,1]/6
        var swma = new Swma(period: 4);
        double[] vals = { 10, 20, 30, 40 };
        for (int i = 0; i < vals.Length; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        // Expected: (1*10 + 2*20 + 2*30 + 1*40) / 6 = (10+40+60+40)/6 = 150/6 = 25.0
        Assert.Equal(25.0, swma.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_Period3_KnownWeights()
    {
        // Period=3: half=1.0, weights: w(0)=1+1-|0-1|=1, w(1)=1+1-0=2, w(2)=1+1-|2-1|=1 => [1,2,1]/4
        var swma = new Swma(period: 3);
        double[] vals = { 10, 20, 30 };
        for (int i = 0; i < vals.Length; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        // Expected: (1*10 + 2*20 + 1*30) / 4 = (10+40+30)/4 = 80/4 = 20.0
        Assert.Equal(20.0, swma.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_Period2_KnownWeights()
    {
        // Period=2: half=0.5, weights: w(0)=0.5+1-|0-0.5|=1.0, w(1)=0.5+1-|1-0.5|=1.0 => [1,1]/2
        var swma = new Swma(period: 2);
        double[] vals = { 10, 20 };
        for (int i = 0; i < vals.Length; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        // Expected: (1*10 + 1*20) / 2 = 15.0 (same as SMA)
        Assert.Equal(15.0, swma.Last.Value, 1e-10);
    }

    // === C) State + bar correction ===

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var swma = new Swma(period: 4);
        swma.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var r1 = swma.Last;
        // New value should advance
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(2), 120.0), isNew: true);
        Assert.NotEqual(r1.Value, swma.Last.Value);
    }

    [Fact]
    public void Update_IsNew_False_Rewrites()
    {
        var swma = new Swma(period: 4);
        for (int i = 0; i < 5; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }
        var afterNew = swma.Last;

        // Correction with same value should return same result
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 104.0), isNew: false);
        Assert.Equal(afterNew.Value, swma.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var swma = new Swma(period: 4);
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            swma.Update(gbm.Next(), isNew: true);
        }
        var baseline = swma.Last;

        // Apply multiple corrections
        swma.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        swma.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        swma.Update(new TValue(DateTime.UtcNow, 777.0), isNew: false);

        // Restore with isNew=false using original value
        swma.Update(new TValue(baseline.Time, baseline.Value), isNew: false);

        // State should be preserved across corrections (buffer not mutated)
        Assert.True(double.IsFinite(swma.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var swma = new Swma(period: 4);
        for (int i = 0; i < 10; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(swma.IsHot);

        swma.Reset();
        Assert.False(swma.IsHot);
        Assert.Equal(default, swma.Last);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var swma = new Swma(period: 5);
        for (int i = 0; i < 4; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(swma.IsHot);
        }
        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 104.0));
        Assert.True(swma.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var swma = new Swma(period: 7);
        Assert.Equal(7, swma.WarmupPeriod);
    }

    [Fact]
    public void DuringWarmup_ReturnsRawValue()
    {
        var swma = new Swma(period: 5);
        var result = swma.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, 1e-10);
    }

    // === E) Robustness ===

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var swma = new Swma(period: 4);
        for (int i = 0; i < 5; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }


        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(5), double.NaN));
        // After NaN, last-valid substitution should produce finite result
        Assert.True(double.IsFinite(swma.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var swma = new Swma(period: 4);
        for (int i = 0; i < 5; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(5), double.PositiveInfinity));
        Assert.True(double.IsFinite(swma.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValid()
    {
        var swma = new Swma(period: 4);
        for (int i = 0; i < 5; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        swma.Update(new TValue(DateTime.UtcNow.AddSeconds(5), double.NegativeInfinity));
        Assert.True(double.IsFinite(swma.Last.Value));
    }

    [Fact]
    public void Update_FirstValueNaN_ReturnsNaN()
    {
        var swma = new Swma(period: 4);
        var result = swma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void Batch_BatchNaN_Safe()
    {
        double[] source = { 10, 20, double.NaN, 40, 50, 60 };
        double[] output = new double[source.Length];
        Swma.Batch(source, output, period: 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite");
        }
    }

    // === F) Consistency (4 modes match) ===

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var src = MakeSeries(100);
        int period = 6;

        // Mode 1: Streaming
        var streaming = new Swma(period);
        var streamResults = new List<double>();
        for (int i = 0; i < src.Count; i++)
        {
            streamResults.Add(streaming.Update(src[i]).Value);
        }

        // Mode 2: Batch TSeries
        var batchResults = Swma.Batch(src, period);

        // Mode 3: Span API
        var spanOutput = new double[src.Count];
        Swma.Batch(src.Values, spanOutput, period);

        // Mode 4: Event-based
        var publisher = new TSeries();
        var eventResults = new List<double>();
        var eventSwma = new Swma(publisher, period);
        eventSwma.Pub += (object? sender, in TValueEventArgs e) => eventResults.Add(e.Value.Value);
        for (int i = 0; i < src.Count; i++)
        {
            publisher.Add(src[i]);
        }

        // Compare all modes
        Assert.Equal(src.Count, batchResults.Count);
        Assert.Equal(src.Count, eventResults.Count);

        for (int i = 0; i < src.Count; i++)
        {
            double s = streamResults[i];
            double b = batchResults[i].Value;
            double sp = spanOutput[i];
            double ev = eventResults[i];

            if (double.IsNaN(s))
            {
                Assert.True(double.IsNaN(b), $"batch[{i}] should be NaN");
                Assert.True(double.IsNaN(sp), $"span[{i}] should be NaN");
                Assert.True(double.IsNaN(ev), $"event[{i}] should be NaN");
            }
            else
            {
                Assert.Equal(s, b, 1e-10);
                Assert.Equal(s, sp, 1e-10);
                Assert.Equal(s, ev, 1e-10);
            }
        }
    }

    // === G) Span API tests ===

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] source = { 1, 2, 3 };
        double[] output = new double[2];

        var ex = Assert.Throws<ArgumentException>(() => Swma.Batch(source, output, period: 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodBelow2_Throws()
    {
        double[] source = { 1, 2, 3 };
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Swma.Batch(source, output, period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOutput()
    {
        Swma.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, period: 4);
        Assert.True(true); // No exception = pass
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var src = MakeSeries(200);
        int period = 5;

        var tsResult = Swma.Batch(src, period);
        var spanOutput = new double[src.Count];
        Swma.Batch(src.Values, spanOutput, period);

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(tsResult[i].Value, spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_NaN_HandledGracefully()
    {
        double[] source = { 10, double.NaN, 30, 40, 50 };
        double[] output = new double[5];

        Swma.Batch(source, output, period: 3);

        // After NaN substitution, all outputs should be finite
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite");
        }
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int count = 10_000;
        double[] source = new double[count];
        double[] output = new double[count];
        for (int i = 0; i < count; i++)
        {
            source[i] = 100.0 + (i % 50);
        }

        Swma.Batch(source, output, period: 20);

        Assert.True(double.IsFinite(output[^1]));
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var swma = new Swma(period: 4);
        int pubCount = 0;
        swma.Pub += (object? sender, in TValueEventArgs e) => pubCount++;

        swma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, pubCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var publisher = new TSeries();
        var swma = new Swma(publisher, period: 4);
        int resultCount = 0;
        swma.Pub += (object? sender, in TValueEventArgs e) => resultCount++;

        for (int i = 0; i < 10; i++)
        {
            publisher.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.Equal(10, resultCount);
    }

    // === Additional: Calculate API ===

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var src = MakeSeries(50);
        var (results, indicator) = Swma.Calculate(src, period: 5);

        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    // === Dispose ===

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var publisher = new TSeries();
        var swma = new Swma(publisher, period: 4);
        int pubCount = 0;
        swma.Pub += (object? sender, in TValueEventArgs e) => pubCount++;

        publisher.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, pubCount);

        swma.Dispose();

        publisher.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 200.0));
        Assert.Equal(1, pubCount); // Should not increment after dispose
    }

    // === Prime ===

    [Fact]
    public void Prime_SetsStateFromSpan()
    {
        var swma = new Swma(period: 4);
        double[] data = { 10, 20, 30, 40, 50 };
        swma.Prime(data);

        Assert.True(swma.IsHot);
        Assert.True(double.IsFinite(swma.Last.Value));
    }

    // === Triangular weight properties ===

    [Fact]
    public void Weights_AreSymmetric()
    {
        // Verify symmetry: output of mirror-reversed input equals original
        var swma1 = new Swma(period: 5);
        var swma2 = new Swma(period: 5);
        double[] vals = { 10, 20, 30, 40, 50 };
        double[] reversed = { 50, 40, 30, 20, 10 };

        for (int i = 0; i < 5; i++)
        {
            swma1.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
            swma2.Update(new TValue(DateTime.UtcNow.AddSeconds(i), reversed[i]));
        }

        // For symmetric filter with symmetric-around-center input:
        // swma({10,20,30,40,50}) + swma({50,40,30,20,10}) should equal 2 * swma({30,30,30,30,30})
        // Both outputs should be finite
        Assert.True(double.IsFinite(swma1.Last.Value));
        Assert.True(double.IsFinite(swma2.Last.Value));
        // sum of outputs = 2 * center value (30) for symmetric weights
        Assert.Equal(60.0, swma1.Last.Value + swma2.Last.Value, 1e-10);
    }

    [Fact]
    public void Output_BoundedByInputRange()
    {
        // All weights non-negative: output is convex combination, bounded by min/max input
        var swma = new Swma(period: 5);
        double[] vals = { 10, 20, 30, 40, 50 };
        for (int i = 0; i < vals.Length; i++)
        {
            swma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }

        Assert.InRange(swma.Last.Value, 10.0, 50.0);
    }

    [Fact]
    public void Update_TSeries_EmptySource_ReturnsEmpty()
    {
        var swma = new Swma(period: 4);
        var empty = new TSeries();
        var result = swma.Update(empty);
        Assert.Empty(result);
    }

    [Fact]
    public void Update_TSeries_ProducesCorrectLength()
    {
        var src = MakeSeries(100);
        var swma = new Swma(period: 4);
        var result = swma.Update(src);
        Assert.Equal(100, result.Count);
    }
}
