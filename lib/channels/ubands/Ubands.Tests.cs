namespace QuanTAlib.Tests;

public class UbandsTests
{
    [Fact]
    public void Ubands_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ubands(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ubands(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ubands(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ubands(10, -1));

        var ubands = new Ubands(10, 1.0);
        Assert.NotNull(ubands);
    }

    [Fact]
    public void Ubands_Update_ReturnsValue()
    {
        var ubands = new Ubands(10, 1.0);
        var result = ubands.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(ubands.Upper.Value));
        Assert.True(double.IsFinite(ubands.Middle.Value));
        Assert.True(double.IsFinite(ubands.Lower.Value));
    }

    [Fact]
    public void Ubands_FirstValue_InitializesCorrectly()
    {
        var ubands = new Ubands(10, 1.0);
        _ = ubands.Update(new TValue(DateTime.UtcNow, 100.0));

        // First value should be the input (USF returns input initially)
        Assert.Equal(100.0, ubands.Middle.Value, precision: 10);
        // First RMS is 0 (no deviation from smooth yet)
        Assert.Equal(100.0, ubands.Upper.Value, precision: 10);
        Assert.Equal(100.0, ubands.Lower.Value, precision: 10);
    }

    [Fact]
    public void Ubands_Properties_Accessible()
    {
        var ubands = new Ubands(10, 1.0);

        Assert.False(ubands.IsHot);
        Assert.Contains("Ubands", ubands.Name, StringComparison.Ordinal);
        Assert.Equal(10, ubands.WarmupPeriod);
    }

    [Fact]
    public void Ubands_Update_IsNew_AcceptsParameter()
    {
        var ubands = new Ubands(10, 1.0);

        var result1 = ubands.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var result2 = ubands.Update(new TValue(DateTime.UtcNow, 101.0), isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Ubands_Update_IsNew_False_UpdatesValue()
    {
        var ubands = new Ubands(10, 1.0);

        // Process several bars
        for (int i = 0; i < 15; i++)
        {
            ubands.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        double beforeCorrection = ubands.Middle.Value;

        // Correct last bar with different value
        ubands.Update(new TValue(DateTime.UtcNow, 200.0), isNew: false);
        double afterCorrection = ubands.Middle.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Ubands_IterativeCorrections_RestoreToOriginalState()
    {
        var ubands = new Ubands(5, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        // Process all bars
        foreach (var val in series)
        {
            ubands.Update(val);
        }
        double originalMiddle = ubands.Middle.Value;
        double originalUpper = ubands.Upper.Value;
        double originalLower = ubands.Lower.Value;

        // Make multiple corrections
        for (int i = 0; i < 10; i++)
        {
            ubands.Update(new TValue(DateTime.UtcNow, 150.0 + i), isNew: false);
        }

        // Restore original
        ubands.Update(series[^1], isNew: false);
        double restoredMiddle = ubands.Middle.Value;
        double restoredUpper = ubands.Upper.Value;
        double restoredLower = ubands.Lower.Value;

        Assert.Equal(originalMiddle, restoredMiddle, precision: 8);
        Assert.Equal(originalUpper, restoredUpper, precision: 8);
        Assert.Equal(originalLower, restoredLower, precision: 8);
    }

    [Fact]
    public void Ubands_Reset_ClearsState()
    {
        var ubands = new Ubands(10, 1.0);

        for (int i = 0; i < 20; i++)
        {
            ubands.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(ubands.IsHot);

        ubands.Reset();

        Assert.False(ubands.IsHot);
    }

    [Fact]
    public void Ubands_IsHot_BecomesTrueAfterWarmup()
    {
        var ubands = new Ubands(5, 1.0);

        for (int i = 0; i < 4; i++)
        {
            ubands.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(ubands.IsHot);
        }

        ubands.Update(new TValue(DateTime.UtcNow, 104.0));
        Assert.True(ubands.IsHot);
    }

    [Fact]
    public void Ubands_WarmupPeriod_IsSetCorrectly()
    {
        var ubands5 = new Ubands(5, 1.0);
        Assert.Equal(5, ubands5.WarmupPeriod);

        var ubands20 = new Ubands(20, 2.0);
        Assert.Equal(20, ubands20.WarmupPeriod);
    }

    [Fact]
    public void Ubands_NaN_Input_UsesLastValidValue()
    {
        var ubands = new Ubands(5, 1.0);

        for (int i = 0; i < 10; i++)
        {
            ubands.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        ubands.Update(new TValue(DateTime.UtcNow, double.NaN));
        double afterNaN = ubands.Middle.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Ubands_Infinity_Input_UsesLastValidValue()
    {
        var ubands = new Ubands(5, 1.0);

        for (int i = 0; i < 10; i++)
        {
            ubands.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        ubands.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(ubands.Middle.Value));

        ubands.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(ubands.Middle.Value));
    }

    [Fact]
    public void Ubands_BandRelationship_UpperGreaterThanLower()
    {
        var ubands = new Ubands(10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        foreach (var val in series)
        {
            ubands.Update(val);
            Assert.True(ubands.Upper.Value >= ubands.Lower.Value,
                $"Upper ({ubands.Upper.Value}) should be >= Lower ({ubands.Lower.Value})");
        }
    }

    [Fact]
    public void Ubands_MiddleBetweenBands()
    {
        var ubands = new Ubands(10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        foreach (var val in series)
        {
            ubands.Update(val);
            Assert.True(ubands.Middle.Value <= ubands.Upper.Value,
                $"Middle ({ubands.Middle.Value}) should be <= Upper ({ubands.Upper.Value})");
            Assert.True(ubands.Middle.Value >= ubands.Lower.Value,
                $"Middle ({ubands.Middle.Value}) should be >= Lower ({ubands.Lower.Value})");
        }
    }

    [Fact]
    public void Ubands_Width_EqualsUpperMinusLower()
    {
        var ubands = new Ubands(10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        foreach (var val in series)
        {
            ubands.Update(val);
            double expectedWidth = ubands.Upper.Value - ubands.Lower.Value;
            Assert.Equal(expectedWidth, ubands.Width.Value, precision: 10);
        }
    }

    [Fact]
    public void Ubands_BatchCalc_MatchesIterativeCalc()
    {
        var ubandsIterative = new Ubands(10, 1.0);
        var ubandsBatch = new Ubands(10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        // Iterative
        var iterativeMiddle = new List<double>();
        foreach (var val in series)
        {
            ubandsIterative.Update(val);
            iterativeMiddle.Add(ubandsIterative.Middle.Value);
        }

        // Batch
        var batchResult = ubandsBatch.Update(series);

        // Compare last 50 values
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(iterativeMiddle[i], batchResult[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Ubands_AllModes_ProduceSameResult()
    {
        int period = 10;
        double multiplier = 1.0;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        // 1. Batch Mode
        var batchResult = Ubands.Calculate(series, period, multiplier);
        double batchLast = batchResult.Last.Value;

        // 2. Span Mode
        double[] source = series.Values.ToArray();
        double[] spanUpper = new double[source.Length];
        double[] spanMiddle = new double[source.Length];
        double[] spanLower = new double[source.Length];
        Ubands.Calculate(source.AsSpan(), spanUpper.AsSpan(), spanMiddle.AsSpan(), spanLower.AsSpan(), period, multiplier);
        double spanLast = spanMiddle[^1];

        // 3. Streaming Mode
        var streamingInd = new Ubands(period, multiplier);
        foreach (var val in series)
        {
            streamingInd.Update(val);
        }
        double streamingLast = streamingInd.Middle.Value;

        Assert.Equal(batchLast, spanLast, precision: 10);
        Assert.Equal(batchLast, streamingLast, precision: 10);
    }

    [Fact]
    public void Ubands_SpanCalculate_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] upper = new double[5];
        double[] middle = new double[5];
        double[] lower = new double[5];
        double[] wrongSize = new double[3];

        // Period must be >= 1
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Ubands.Calculate(source.AsSpan(), upper.AsSpan(), middle.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Ubands.Calculate(source.AsSpan(), upper.AsSpan(), middle.AsSpan(), lower.AsSpan(), -1));

        // All arrays must be same length
        Assert.Throws<ArgumentException>(() =>
            Ubands.Calculate(source.AsSpan(), wrongSize.AsSpan(), middle.AsSpan(), lower.AsSpan(), 3));
    }

    [Fact]
    public void Ubands_SpanCalculate_HandlesNaN()
    {
        double[] source = [100, 101, double.NaN, 103, 104];
        double[] upper = new double[5];
        double[] middle = new double[5];
        double[] lower = new double[5];

        Ubands.Calculate(source.AsSpan(), upper.AsSpan(), middle.AsSpan(), lower.AsSpan(), 3, 1.0);

        foreach (var val in middle)
        {
            Assert.True(double.IsFinite(val), $"Middle should be finite, got {val}");
        }
    }

    [Fact]
    public void Ubands_FlatLine_ReturnsSameValueForMiddle()
    {
        var ubands = new Ubands(10, 1.0);
        for (int i = 0; i < 30; i++)
        {
            ubands.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        // After warmup with constant input, middle should equal input
        Assert.Equal(100.0, ubands.Middle.Value, precision: 6);
        // RMS of zero residuals = 0, so upper = lower = middle
        Assert.Equal(ubands.Middle.Value, ubands.Upper.Value, precision: 6);
        Assert.Equal(ubands.Middle.Value, ubands.Lower.Value, precision: 6);
    }

    [Fact]
    public void Ubands_HigherMultiplier_WiderBands()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        var ubands1 = new Ubands(period, 1.0);
        var ubands2 = new Ubands(period, 2.0);

        foreach (var val in series)
        {
            ubands1.Update(val);
            ubands2.Update(val);
        }

        // Same middle (USF is the same)
        Assert.Equal(ubands1.Middle.Value, ubands2.Middle.Value, precision: 10);

        // Higher multiplier = wider bands
        Assert.True(ubands2.Width.Value > ubands1.Width.Value,
            $"Width with mult=2 ({ubands2.Width.Value}) should be > width with mult=1 ({ubands1.Width.Value})");
    }

    [Fact]
    public void Ubands_Prime_SetsStateCorrectly()
    {
        var ubands = new Ubands(5, 1.0);
        double[] history = [10, 20, 30, 40, 50, 60, 70];

        ubands.Prime(history);

        Assert.True(ubands.IsHot);
        Assert.True(double.IsFinite(ubands.Middle.Value));
    }

    [Fact]
    public void Ubands_StaticCalculate_Works()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        var result = Ubands.Calculate(series, 10, 1.0);

        Assert.Equal(50, result.Count);
        Assert.True(double.IsFinite(result.Last.Value));
    }
}