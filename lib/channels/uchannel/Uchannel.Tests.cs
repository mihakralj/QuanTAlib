namespace QuanTAlib.Tests;

public class UchannelTests
{
    [Fact]
    public void Uchannel_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Uchannel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Uchannel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Uchannel(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Uchannel(10, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Uchannel(10, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Uchannel(10, 10, -1));

        var uchannel = new Uchannel(10, 10, 1.0);
        Assert.NotNull(uchannel);
    }

    [Fact]
    public void Uchannel_Update_ReturnsValue()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var bar = new TBar(DateTime.UtcNow, 102.0, 98.0, 100.0, 101.0, 1000);
        var result = uchannel.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(uchannel.Upper.Value));
        Assert.True(double.IsFinite(uchannel.Middle.Value));
        Assert.True(double.IsFinite(uchannel.Lower.Value));
        Assert.True(double.IsFinite(uchannel.STR.Value));
    }

    [Fact]
    public void Uchannel_FirstValue_InitializesCorrectly()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var bar = new TBar(DateTime.UtcNow, 102.0, 98.0, 100.0, 101.0, 1000);
        _ = uchannel.Update(bar);

        // First value should be the close (USF returns input initially)
        Assert.Equal(101.0, uchannel.Middle.Value, precision: 10);
        // First TR = high - low = 102 - 98 = 4 (no prevClose yet)
        Assert.True(double.IsFinite(uchannel.STR.Value));
    }

    [Fact]
    public void Uchannel_Properties_Accessible()
    {
        var uchannel = new Uchannel(10, 10, 1.0);

        Assert.False(uchannel.IsHot);
        Assert.Contains("Uchannel", uchannel.Name, StringComparison.Ordinal);
        Assert.Equal(10, uchannel.WarmupPeriod);
    }

    [Fact]
    public void Uchannel_Update_IsNew_AcceptsParameter()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var bar1 = new TBar(DateTime.UtcNow, 102.0, 98.0, 100.0, 101.0, 1000);
        var bar2 = new TBar(DateTime.UtcNow, 103.0, 99.0, 101.0, 102.0, 1000);

        var result1 = uchannel.Update(bar1, isNew: true);
        var result2 = uchannel.Update(bar2, isNew: false);

        Assert.True(double.IsFinite(result1.Value));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Uchannel_Update_IsNew_False_UpdatesValue()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(15, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process several bars
        foreach (var bar in bars)
        {
            uchannel.Update(bar, isNew: true);
        }

        double beforeCorrection = uchannel.Middle.Value;

        // Correct last bar with different value
        var correctionBar = new TBar(DateTime.UtcNow, 250.0, 150.0, 200.0, 200.0, 1000);
        uchannel.Update(correctionBar, isNew: false);
        double afterCorrection = uchannel.Middle.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Uchannel_IterativeCorrections_RestoreToOriginalState()
    {
        var uchannel = new Uchannel(5, 5, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process all bars
        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }
        double originalMiddle = uchannel.Middle.Value;
        double originalUpper = uchannel.Upper.Value;
        double originalLower = uchannel.Lower.Value;

        // Make multiple corrections
        for (int i = 0; i < 10; i++)
        {
            var correctionBar = new TBar(DateTime.UtcNow, 200.0 + i, 140.0 + i, 150.0 + i, 190.0 + i, 1000);
            uchannel.Update(correctionBar, isNew: false);
        }

        // Restore original
        var lastBar = bars[^1];
        uchannel.Update(lastBar, isNew: false);
        double restoredMiddle = uchannel.Middle.Value;
        double restoredUpper = uchannel.Upper.Value;
        double restoredLower = uchannel.Lower.Value;

        Assert.Equal(originalMiddle, restoredMiddle, precision: 8);
        Assert.Equal(originalUpper, restoredUpper, precision: 8);
        Assert.Equal(originalLower, restoredLower, precision: 8);
    }

    [Fact]
    public void Uchannel_Reset_ClearsState()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }

        Assert.True(uchannel.IsHot);

        uchannel.Reset();

        Assert.False(uchannel.IsHot);
    }

    [Fact]
    public void Uchannel_IsHot_BecomesTrueAfterWarmup()
    {
        var uchannel = new Uchannel(5, 5, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 4; i++)
        {
            uchannel.Update(bars[i]);
            Assert.False(uchannel.IsHot);
        }

        uchannel.Update(bars[4]);
        Assert.True(uchannel.IsHot);
    }

    [Fact]
    public void Uchannel_WarmupPeriod_IsMaxOfPeriods()
    {
        var uchannel1 = new Uchannel(5, 10, 1.0);
        Assert.Equal(10, uchannel1.WarmupPeriod);

        var uchannel2 = new Uchannel(20, 10, 2.0);
        Assert.Equal(20, uchannel2.WarmupPeriod);

        var uchannel3 = new Uchannel(15, 15, 1.5);
        Assert.Equal(15, uchannel3.WarmupPeriod);
    }

    [Fact]
    public void Uchannel_NaN_Input_UsesLastValidValue()
    {
        var uchannel = new Uchannel(5, 5, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }

        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000);
        uchannel.Update(nanBar);
        double afterNaN = uchannel.Middle.Value;

        Assert.True(double.IsFinite(afterNaN));
        Assert.True(double.IsFinite(uchannel.Upper.Value));
        Assert.True(double.IsFinite(uchannel.Lower.Value));
    }

    [Fact]
    public void Uchannel_Infinity_Input_UsesLastValidValue()
    {
        var uchannel = new Uchannel(5, 5, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }

        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.NegativeInfinity, 100.0, double.PositiveInfinity, 1000);
        uchannel.Update(infBar);
        Assert.True(double.IsFinite(uchannel.Middle.Value));
        Assert.True(double.IsFinite(uchannel.Upper.Value));
        Assert.True(double.IsFinite(uchannel.Lower.Value));
    }

    [Fact]
    public void Uchannel_BandRelationship_UpperGreaterThanLower()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
            Assert.True(uchannel.Upper.Value >= uchannel.Lower.Value,
                $"Upper ({uchannel.Upper.Value}) should be >= Lower ({uchannel.Lower.Value})");
        }
    }

    [Fact]
    public void Uchannel_MiddleBetweenBands()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
            Assert.True(uchannel.Middle.Value <= uchannel.Upper.Value,
                $"Middle ({uchannel.Middle.Value}) should be <= Upper ({uchannel.Upper.Value})");
            Assert.True(uchannel.Middle.Value >= uchannel.Lower.Value,
                $"Middle ({uchannel.Middle.Value}) should be >= Lower ({uchannel.Lower.Value})");
        }
    }

    [Fact]
    public void Uchannel_Width_EqualsUpperMinusLower()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
            double expectedWidth = uchannel.Upper.Value - uchannel.Lower.Value;
            Assert.Equal(expectedWidth, uchannel.Width.Value, precision: 10);
        }
    }

    [Fact]
    public void Uchannel_BatchCalc_MatchesIterativeCalc()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Iterative
        var uchannelIterative = new Uchannel(10, 10, 1.0);
        var iterativeMiddle = new List<double>();
        foreach (var bar in bars)
        {
            uchannelIterative.Update(bar);
            iterativeMiddle.Add(uchannelIterative.Middle.Value);
        }

        // Batch
        var (_, batchMiddle, _, _) = Uchannel.Calculate(bars, 10, 10, 1.0);

        // Compare last 50 values
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(iterativeMiddle[i], batchMiddle[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Uchannel_AllModes_ProduceSameResult()
    {
        int strPeriod = 10;
        int centerPeriod = 10;
        double multiplier = 1.0;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var (_, batchMiddle, _, _) = Uchannel.Calculate(bars, strPeriod, centerPeriod, multiplier);
        double batchLast = batchMiddle.Last.Value;

        // 2. Span Mode
        double[] highArr = bars.High.Values.ToArray();
        double[] lowArr = bars.Low.Values.ToArray();
        double[] closeArr = bars.Close.Values.ToArray();
        double[] spanUpper = new double[highArr.Length];
        double[] spanMiddle = new double[highArr.Length];
        double[] spanLower = new double[highArr.Length];
        Uchannel.Calculate(highArr.AsSpan(), lowArr.AsSpan(), closeArr.AsSpan(),
            spanUpper.AsSpan(), spanMiddle.AsSpan(), spanLower.AsSpan(),
            strPeriod, centerPeriod, multiplier);
        double spanLast = spanMiddle[^1];

        // 3. Streaming Mode
        var streamingInd = new Uchannel(strPeriod, centerPeriod, multiplier);
        foreach (var bar in bars)
        {
            streamingInd.Update(bar);
        }
        double streamingLast = streamingInd.Middle.Value;

        Assert.Equal(batchLast, spanLast, precision: 10);
        Assert.Equal(batchLast, streamingLast, precision: 10);
    }

    [Fact]
    public void Uchannel_SpanCalculate_ValidatesInput()
    {
        double[] high = [102, 103, 104, 105, 106];
        double[] low = [98, 99, 100, 101, 102];
        double[] close = [100, 101, 102, 103, 104];
        double[] upper = new double[5];
        double[] middle = new double[5];
        double[] lower = new double[5];
        double[] wrongSize = new double[3];

        // Period must be >= 1
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Uchannel.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                upper.AsSpan(), middle.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Uchannel.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                upper.AsSpan(), middle.AsSpan(), lower.AsSpan(), -1));

        // All arrays must be same length
        Assert.Throws<ArgumentException>(() =>
            Uchannel.Calculate(high.AsSpan(), wrongSize.AsSpan(), close.AsSpan(),
                upper.AsSpan(), middle.AsSpan(), lower.AsSpan(), 3));
    }

    [Fact]
    public void Uchannel_SpanCalculate_HandlesNaN()
    {
        double[] high = [102, 103, double.NaN, 105, 106];
        double[] low = [98, 99, double.NaN, 101, 102];
        double[] close = [100, 101, double.NaN, 103, 104];
        double[] upper = new double[5];
        double[] middle = new double[5];
        double[] lower = new double[5];

        Uchannel.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(),
            upper.AsSpan(), middle.AsSpan(), lower.AsSpan(), 3, 3, 1.0);

        foreach (var val in middle)
        {
            Assert.True(double.IsFinite(val), $"Middle should be finite, got {val}");
        }
    }

    [Fact]
    public void Uchannel_FlatLine_ReturnsSameValueForMiddle()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow, 100.0, 100.0, 100.0, 100.0, 1000);
            uchannel.Update(bar);
        }

        // After warmup with constant input, middle should equal input
        Assert.Equal(100.0, uchannel.Middle.Value, precision: 6);
        // TR of flat bars = 0, so STR = 0, upper = lower = middle
        Assert.Equal(uchannel.Middle.Value, uchannel.Upper.Value, precision: 6);
        Assert.Equal(uchannel.Middle.Value, uchannel.Lower.Value, precision: 6);
    }

    [Fact]
    public void Uchannel_HigherMultiplier_WiderBands()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel1 = new Uchannel(period, period, 1.0);
        var uchannel2 = new Uchannel(period, period, 2.0);

        foreach (var bar in bars)
        {
            uchannel1.Update(bar);
            uchannel2.Update(bar);
        }

        // Same middle (USF is the same)
        Assert.Equal(uchannel1.Middle.Value, uchannel2.Middle.Value, precision: 10);

        // Higher multiplier = wider bands
        Assert.True(uchannel2.Width.Value > uchannel1.Width.Value,
            $"Width with mult=2 ({uchannel2.Width.Value}) should be > width with mult=1 ({uchannel1.Width.Value})");
    }

    [Fact]
    public void Uchannel_STR_IsAlwaysNonNegative()
    {
        var uchannel = new Uchannel(10, 10, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
            Assert.True(uchannel.STR.Value >= 0,
                $"STR ({uchannel.STR.Value}) should be >= 0");
        }
    }

    [Fact]
    public void Uchannel_TrueRange_UsedCorrectly()
    {
        var uchannel = new Uchannel(10, 10, 1.0);

        // First bar: TR = High - Low (no prevClose)
        var bar1 = new TBar(DateTime.UtcNow, 105.0, 95.0, 100.0, 102.0, 1000);
        uchannel.Update(bar1);
        // TR = 105 - 95 = 10

        // Second bar: Gap up, TR should use prevClose
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 120.0, 110.0, 115.0, 118.0, 1000);
        uchannel.Update(bar2);
        // TrueHigh = max(120, 102) = 120
        // TrueLow = min(110, 102) = 102
        // TR = 120 - 102 = 18

        Assert.True(uchannel.STR.Value > 0);
    }

    [Fact]
    public void Uchannel_StaticCalculate_Works()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (upper, middle, lower, str) = Uchannel.Calculate(bars, 10, 10, 1.0);

        Assert.Equal(50, upper.Count);
        Assert.Equal(50, middle.Count);
        Assert.Equal(50, lower.Count);
        Assert.Equal(50, str.Count);
        Assert.True(double.IsFinite(middle.Last.Value));
    }

    [Fact]
    public void Uchannel_DifferentStrAndCenterPeriods_Work()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Different periods for STR and centerline
        var uchannel = new Uchannel(5, 20, 1.0);

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }

        Assert.True(uchannel.IsHot);
        Assert.True(double.IsFinite(uchannel.Middle.Value));
        Assert.True(double.IsFinite(uchannel.STR.Value));
    }

    [Fact]
    public void Uchannel_UpdateWithLongTime_MatchesUpdateWithTBar()
    {
        var uchannel1 = new Uchannel(10, 10, 1.0);
        var uchannel2 = new Uchannel(10, 10, 1.0);

        var time = DateTime.UtcNow;
        long timeTicks = time.Ticks;
        double open = 100.0, high = 105.0, low = 95.0, close = 102.0;

        // TBar constructor is: (time, open, high, low, close, volume)
        var bar = new TBar(time, open, high, low, close, 1000);

        var result1 = uchannel1.Update(bar);
        var result2 = uchannel2.Update(timeTicks, high, low, close, isNew: true);

        Assert.Equal(result1.Value, result2.Value, precision: 10);
        Assert.Equal(uchannel1.Upper.Value, uchannel2.Upper.Value, precision: 10);
        Assert.Equal(uchannel1.Lower.Value, uchannel2.Lower.Value, precision: 10);
    }
}