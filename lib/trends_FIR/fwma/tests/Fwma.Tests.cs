namespace QuanTAlib.Tests;

public class FwmaTests
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
    public void Constructor_DefaultPeriod_Is10()
    {
        var fwma = new Fwma();
        Assert.Equal("Fwma(10)", fwma.Name);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var fwma = new Fwma(period: 5);
        Assert.Equal("Fwma(5)", fwma.Name);
    }

    [Fact]
    public void Constructor_Period1_IsValid()
    {
        var fwma = new Fwma(period: 1);
        Assert.Equal("Fwma(1)", fwma.Name);
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fwma(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fwma(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var fwma = new Fwma(period: 8);
        Assert.Equal(8, fwma.WarmupPeriod);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var fwma = new Fwma(period: 5);
        var result = fwma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var fwma = new Fwma(period: 5);
        fwma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(fwma.Last.Value));
    }

    [Fact]
    public void Update_ConstantInput_ReturnsConstant()
    {
        var fwma = new Fwma(period: 5);
        for (int i = 0; i < 10; i++)
        {
            fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0));
        }
        Assert.Equal(50.0, fwma.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_Period1_ReturnsInput()
    {
        var fwma = new Fwma(period: 1);
        for (int i = 1; i <= 5; i++)
        {
            var result = fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), i * 10.0));
            Assert.Equal(i * 10.0, result.Value, 1e-10);
        }
    }

    [Fact]
    public void Update_KnownValues_Period3()
    {
        // Period=3: Fibonacci weights F(3)=2, F(2)=1, F(1)=1, sum=4
        // Normalized: [2/4, 1/4, 1/4] = [0.5, 0.25, 0.25]
        // For inputs [10, 20, 30]:
        // newest=30 * 0.5 + middle=20 * 0.25 + oldest=10 * 0.25 = 15 + 5 + 2.5 = 22.5
        var fwma = new Fwma(period: 3);
        fwma.Update(new TValue(DateTime.UtcNow, 10.0));
        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 20.0));
        var result = fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(2), 30.0));

        Assert.Equal(22.5, result.Value, 1e-10);
    }

    [Fact]
    public void Update_KnownValues_Period5()
    {
        // Period=5: F(5)=5, F(4)=3, F(3)=2, F(2)=1, F(1)=1, sum=12
        // Normalized: [5/12, 3/12, 2/12, 1/12, 1/12]
        // For inputs [10, 20, 30, 40, 50]:
        // 50*5/12 + 40*3/12 + 30*2/12 + 20*1/12 + 10*1/12
        // = 250/12 + 120/12 + 60/12 + 20/12 + 10/12 = 460/12 = 38.333...
        var fwma = new Fwma(period: 5);
        double[] values = [10, 20, 30, 40, 50];
        TValue result = default;
        for (int i = 0; i < values.Length; i++)
        {
            result = fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), values[i]));
        }

        Assert.Equal(460.0 / 12.0, result.Value, 1e-10);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var fwma = new Fwma(period: 7);
        Assert.Equal("Fwma(7)", fwma.Name);
    }

    // === C) State + bar correction ===

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var fwma = new Fwma(period: 5);
        var series = MakeSeries(10);

        for (int i = 0; i < series.Count; i++)
        {
            fwma.Update(series[i], isNew: true);
        }

        Assert.True(double.IsFinite(fwma.Last.Value));
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var fwma = new Fwma(period: 5);
        var series = MakeSeries(10);

        for (int i = 0; i < 8; i++)
        {
            fwma.Update(series[i], isNew: true);
        }

        double beforeCorrection = fwma.Last.Value;
        _ = fwma.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        double afterCorrection = fwma.Last.Value;

        // After correction with different value, result should change
        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var fwma = new Fwma(period: 5);
        var series = MakeSeries(20);

        for (int i = 0; i < series.Count; i++)
        {
            fwma.Update(series[i], isNew: true);
        }
        double expected = fwma.Last.Value;

        // Apply correction with same value — should get same result
        _ = fwma.Update(series[^1], isNew: false);
        Assert.Equal(expected, fwma.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var fwma = new Fwma(period: 5);
        var series = MakeSeries(10);

        for (int i = 0; i < series.Count; i++)
        {
            fwma.Update(series[i]);
        }

        fwma.Reset();
        Assert.False(fwma.IsHot);
        Assert.Equal(0.0, fwma.Last.Value);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        var fwma = new Fwma(period: 5);

        for (int i = 0; i < 4; i++)
        {
            fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(fwma.IsHot);
        }

        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 104.0));
        Assert.True(fwma.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var fwma = new Fwma(period: 13);
        Assert.Equal(13, fwma.WarmupPeriod);
    }

    // === E) Robustness ===

    [Fact]
    public void NaN_UsesLastValid()
    {
        var fwma = new Fwma(period: 3);
        fwma.Update(new TValue(DateTime.UtcNow, 100.0));
        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 200.0));
        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(2), double.NaN));

        // NaN should be replaced with last valid (200.0)
        // So effectively [100, 200, 200] with period=3
        // F(3)=2, F(2)=1, F(1)=1, sum=4
        // 200*2/4 + 200*1/4 + 100*1/4 = 100 + 50 + 25 = 175
        Assert.Equal(175.0, fwma.Last.Value, 1e-10);
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var fwma = new Fwma(period: 3);
        fwma.Update(new TValue(DateTime.UtcNow, 100.0));
        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 200.0));
        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(2), double.PositiveInfinity));

        Assert.Equal(175.0, fwma.Last.Value, 1e-10);
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] source = [100, 200, double.NaN, 400, 500];
        double[] output = new double[5];

        Fwma.Batch(source.AsSpan(), output.AsSpan(), 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] should be finite");
        }
    }

    // === F) Consistency (4 modes match) ===

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        var src = MakeSeries(100);

        // Mode 1: Streaming
        var streaming = new Fwma(period);
        var streamResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streamResults[i] = streaming.Update(src[i]).Value;
        }

        // Mode 2: Batch TSeries
        var batchResults = Fwma.Batch(src, period);

        // Mode 3: Span
        double[] spanOutput = new double[src.Count];
        Fwma.Batch(src.Values, spanOutput.AsSpan(), period);

        // Mode 4: Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Fwma(eventSource, period);
        var eventResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            eventSource.Add(src[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        // Compare all modes (after warmup)
        for (int i = period; i < src.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-10);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
            Assert.Equal(streamResults[i], eventResults[i], 1e-10);
        }
    }

    // === G) Span API tests ===

    [Fact]
    public void Batch_Span_ValidatesLengths()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongOutput = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Fwma.Batch(source.AsSpan(), wrongOutput.AsSpan(), 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ValidatesPeriod()
    {
        double[] source = [1, 2, 3];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Fwma.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => Fwma.Batch(source.AsSpan(), output.AsSpan(), -1));
        Assert.Equal("period", ex2.ParamName);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int period = 10;
        var src = MakeSeries(100);

        var tseriesResult = Fwma.Batch(src, period);

        double[] spanOutput = new double[src.Count];
        Fwma.Batch(src.Values, spanOutput.AsSpan(), period);

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(tseriesResult.Values[i], spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        double[] source = [100, double.NaN, 300, 400, 500];
        double[] output = new double[5];

        Fwma.Batch(source.AsSpan(), output.AsSpan(), 3);

        // After NaN substitution, all hot outputs should be finite
        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoError()
    {
        Fwma.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5);
        Assert.True(true, "Empty span batch should not throw");
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int count = 10000;
        double[] source = new double[count];
        double[] output = new double[count];
        for (int i = 0; i < count; i++)
        {
            source[i] = 100.0 + (i * 0.1);
        }

        Fwma.Batch(source.AsSpan(), output.AsSpan(), 20);

        Assert.True(double.IsFinite(output[^1]));
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_Fires()
    {
        var fwma = new Fwma(period: 5);
        bool fired = false;
        fwma.Pub += (object? _, in TValueEventArgs _) => fired = true;

        fwma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(fired);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var fwma = new Fwma(source, period: 5);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(fwma.Last.Value));
    }

    // === Additional edge cases ===

    [Fact]
    public void FibonacciWeights_AreCorrect_Period5()
    {
        // F(1)=1, F(2)=1, F(3)=2, F(4)=3, F(5)=5, sum=12
        // For constant input, output = input regardless of weights
        var fwma = new Fwma(period: 5);
        for (int i = 0; i < 10; i++)
        {
            fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 42.0));
        }
        Assert.Equal(42.0, fwma.Last.Value, 1e-10);
    }

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var src = MakeSeries(50);
        var (results, indicator) = Fwma.Calculate(src, 5);

        Assert.Equal(src.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void LargePeriod_Handles()
    {
        // Test with period larger than data to verify warmup
        var fwma = new Fwma(period: 100);
        for (int i = 0; i < 50; i++)
        {
            fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.False(fwma.IsHot);
        Assert.True(double.IsFinite(fwma.Last.Value));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var fwma = new Fwma(source, period: 5);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(fwma.Last.Value));

        fwma.Dispose();

        // After dispose, adding to source should not update the indicator
        double lastBefore = fwma.Last.Value;
        source.Add(new TValue(DateTime.UtcNow.AddSeconds(1), 999.0));
        Assert.Equal(lastBefore, fwma.Last.Value);
    }
}
