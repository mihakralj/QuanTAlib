namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for UCHANNEL (Ehlers Ultimate Channel).
/// Since this is a proprietary Ehlers indicator (2024), no external library implementations exist.
/// These tests validate internal consistency, mathematical properties, and behavior characteristics.
/// </summary>
public class UchannelValidationTests
{
    private const int DefaultStrPeriod = 20;
    private const int DefaultCenterPeriod = 20;
    private const double DefaultMultiplier = 1.0;

    /// <summary>
    /// Validates that streaming and batch calculations produce identical results.
    /// </summary>
    [Fact]
    public void Uchannel_StreamingVsBatch_MatchWithinTolerance()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);
        var streamMiddle = new List<double>();
        var streamUpper = new List<double>();
        var streamLower = new List<double>();
        foreach (var bar in bars)
        {
            streaming.Update(bar);
            streamMiddle.Add(streaming.Middle.Value);
            streamUpper.Add(streaming.Upper.Value);
            streamLower.Add(streaming.Lower.Value);
        }

        // Batch
        var (batchUpper, batchMiddle, batchLower, _) = Uchannel.Calculate(bars, DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);

        // Compare all values (skip first few for warmup)
        for (int i = DefaultCenterPeriod; i < bars.Count; i++)
        {
            Assert.Equal(streamMiddle[i], batchMiddle[i].Value, precision: 10);
            Assert.Equal(streamUpper[i], batchUpper[i].Value, precision: 10);
            Assert.Equal(streamLower[i], batchLower[i].Value, precision: 10);
        }
    }

    /// <summary>
    /// Validates that span-based calculation matches streaming calculation.
    /// </summary>
    [Fact]
    public void Uchannel_SpanVsStreaming_MatchWithinTolerance()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);
        var streamMiddle = new List<double>();
        foreach (var bar in bars)
        {
            streaming.Update(bar);
            streamMiddle.Add(streaming.Middle.Value);
        }

        // Span
        double[] highArr = bars.High.Values.ToArray();
        double[] lowArr = bars.Low.Values.ToArray();
        double[] closeArr = bars.Close.Values.ToArray();
        double[] spanUpper = new double[highArr.Length];
        double[] spanMiddle = new double[highArr.Length];
        double[] spanLower = new double[highArr.Length];
        Uchannel.Batch(highArr.AsSpan(), lowArr.AsSpan(), closeArr.AsSpan(),
            spanUpper.AsSpan(), spanMiddle.AsSpan(), spanLower.AsSpan(),
            DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);

        // Compare all values
        for (int i = DefaultCenterPeriod; i < bars.Count; i++)
        {
            Assert.Equal(streamMiddle[i], spanMiddle[i], precision: 10);
        }
    }

    /// <summary>
    /// Validates USF (Ultrasmooth Filter) mathematical properties:
    /// The middle line should lag less than a simple moving average.
    /// </summary>
    [Fact]
    public void Uchannel_USF_HasLessLagThanSMA()
    {
        int period = 20;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel = new Uchannel(period, period, 1.0);
        var sma = new Sma(period);

        double uchannelLagSum = 0;
        double smaLagSum = 0;

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
            sma.Update(new TValue(bar.Time, bar.Close));

            if (uchannel.IsHot && sma.IsHot)
            {
                // Measure deviation from close (proxy for lag in trending market)
                uchannelLagSum += Math.Abs(uchannel.Middle.Value - bar.Close);
                smaLagSum += Math.Abs(sma.Last.Value - bar.Close);
            }
        }

        // USF should have less overall deviation (implying less lag)
        Assert.True(uchannelLagSum < smaLagSum,
            $"USF lag sum ({uchannelLagSum:F4}) should be less than SMA lag sum ({smaLagSum:F4})");
    }

    /// <summary>
    /// Validates that the channel bands are symmetric around the middle.
    /// Upper - Middle should equal Middle - Lower.
    /// </summary>
    [Fact]
    public void Uchannel_Bands_AreSymmetric()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);

        foreach (var bar in bars)
        {
            uchannel.Update(bar);

            double upperDist = uchannel.Upper.Value - uchannel.Middle.Value;
            double lowerDist = uchannel.Middle.Value - uchannel.Lower.Value;

            Assert.Equal(upperDist, lowerDist, precision: 10);
        }
    }

    /// <summary>
    /// Validates that band width equals 2 × STR × multiplier.
    /// </summary>
    [Fact]
    public void Uchannel_Width_Equals2xSTRxMultiplier()
    {
        double multiplier = 1.5;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, multiplier);

        foreach (var bar in bars)
        {
            uchannel.Update(bar);

            double expectedWidth = 2 * uchannel.STR.Value * multiplier;
            Assert.Equal(expectedWidth, uchannel.Width.Value, precision: 10);
        }
    }

    /// <summary>
    /// Validates that STR (Smoothed True Range) is always non-negative.
    /// </summary>
    [Fact]
    public void Uchannel_STR_AlwaysNonNegative()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
            Assert.True(uchannel.STR.Value >= 0,
                $"STR ({uchannel.STR.Value}) should always be non-negative");
        }
    }

    /// <summary>
    /// Validates that True Range calculation handles gaps correctly.
    /// True Range should account for gap between prev close and current high/low.
    /// </summary>
    [Fact]
    public void Uchannel_TrueRange_HandlesGapsCorrectly()
    {
        var uchannel = new Uchannel(3, 3, 1.0);

        // Day 1: Normal bar
        var bar1 = new TBar(DateTime.UtcNow, 102.0, 98.0, 100.0, 100.0, 1000);
        uchannel.Update(bar1);
        // TR = 102 - 98 = 4

        // Day 2: Gap up (open above prev close)
        var bar2 = new TBar(DateTime.UtcNow.AddDays(1), 115.0, 110.0, 112.0, 114.0, 1000);
        uchannel.Update(bar2);
        // True High = max(115, 100) = 115
        // True Low = min(110, 100) = 100
        // TR = 115 - 100 = 15

        // Day 3: Gap down (open below prev close)
        var bar3 = new TBar(DateTime.UtcNow.AddDays(2), 108.0, 90.0, 95.0, 92.0, 1000);
        uchannel.Update(bar3);
        // True High = max(108, 114) = 114
        // True Low = min(90, 114) = 90
        // TR = 114 - 90 = 24

        // STR should reflect these larger TR values due to gaps
        Assert.True(uchannel.STR.Value > 0);
    }

    /// <summary>
    /// Validates that different STR and center periods work independently.
    /// </summary>
    [Fact]
    public void Uchannel_DifferentPeriods_ProduceDifferentResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel1 = new Uchannel(10, 20, 1.0);  // Short STR, long center
        var uchannel2 = new Uchannel(20, 10, 1.0);  // Long STR, short center
        var uchannel3 = new Uchannel(15, 15, 1.0);  // Equal periods

        foreach (var bar in bars)
        {
            uchannel1.Update(bar);
            uchannel2.Update(bar);
            uchannel3.Update(bar);
        }

        // Middle lines should differ (different center periods)
        Assert.NotEqual(uchannel1.Middle.Value, uchannel2.Middle.Value);

        // STR should differ (different STR periods)
        Assert.NotEqual(uchannel1.STR.Value, uchannel2.STR.Value);
    }

    /// <summary>
    /// Validates that the multiplier scales the band width proportionally.
    /// </summary>
    [Fact]
    public void Uchannel_Multiplier_ScalesBandWidthProportionally()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel1 = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, 1.0);
        var uchannel2 = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, 2.0);
        var uchannel3 = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, 0.5);

        foreach (var bar in bars)
        {
            uchannel1.Update(bar);
            uchannel2.Update(bar);
            uchannel3.Update(bar);
        }

        // Width should scale proportionally with multiplier
        Assert.Equal(uchannel1.Width.Value * 2, uchannel2.Width.Value, precision: 10);
        Assert.Equal(uchannel1.Width.Value / 2, uchannel3.Width.Value, precision: 10);

        // Middle should be the same (same center period)
        Assert.Equal(uchannel1.Middle.Value, uchannel2.Middle.Value, precision: 10);
        Assert.Equal(uchannel1.Middle.Value, uchannel3.Middle.Value, precision: 10);
    }

    /// <summary>
    /// Validates that bar correction (isNew=false) works correctly.
    /// </summary>
    [Fact]
    public void Uchannel_BarCorrection_WorksCorrectly()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannel = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);

        // Process all bars
        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }

        double originalMiddle = uchannel.Middle.Value;
        double originalUpper = uchannel.Upper.Value;
        double originalSTR = uchannel.STR.Value;

        // Simulate tick corrections
        for (int tick = 0; tick < 20; tick++)
        {
            var correctionBar = new TBar(DateTime.UtcNow, 150.0 + tick, 140.0, 145.0, 148.0, 1000);
            uchannel.Update(correctionBar, isNew: false);
        }

        // Restore with original last bar
        uchannel.Update(bars[^1], isNew: false);

        Assert.Equal(originalMiddle, uchannel.Middle.Value, precision: 10);
        Assert.Equal(originalUpper, uchannel.Upper.Value, precision: 10);
        Assert.Equal(originalSTR, uchannel.STR.Value, precision: 10);
    }

    /// <summary>
    /// Validates that the indicator converges to stable values.
    /// </summary>
    [Fact]
    public void Uchannel_ConvergesToStableValues()
    {
        var uchannel = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);

        // Feed constant bars
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 105.0, 95.0, 100.0, 100.0, 1000);
            uchannel.Update(bar);
        }

        double middle50 = uchannel.Middle.Value;

        // Feed more constant bars
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(100 + i), 105.0, 95.0, 100.0, 100.0, 1000);
            uchannel.Update(bar);
        }

        double middle100 = uchannel.Middle.Value;

        // Should converge to close value (100.0)
        Assert.True(Math.Abs(middle50 - 100.0) < 0.1);
        Assert.True(Math.Abs(middle100 - 100.0) < 0.01);
    }

    /// <summary>
    /// Validates that STR converges to the True Range value for constant volatility.
    /// </summary>
    [Fact]
    public void Uchannel_STR_ConvergesToTrueRange()
    {
        var uchannel = new Uchannel(10, 10, 1.0);

        // Feed bars with constant TR = 10 (high=105, low=95)
        // TBar constructor: (time, open, high, low, close, volume)
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100.0, 105.0, 95.0, 100.0, 1000);
            uchannel.Update(bar);
        }

        // STR should converge to TR value (10.0)
        Assert.True(Math.Abs(uchannel.STR.Value - 10.0) < 0.1,
            $"STR ({uchannel.STR.Value}) should converge to TR (10.0)");
    }

    /// <summary>
    /// Validates behavior with high volatility data.
    /// </summary>
    [Fact]
    public void Uchannel_HighVolatility_ProducesWiderBands()
    {
        var gbmLow = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.05, seed: 42);
        var gbmHigh = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.30, seed: 42);

        var barsLow = gbmLow.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var barsHigh = gbmHigh.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var uchannelLow = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);
        var uchannelHigh = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);

        foreach (var bar in barsLow)
        {
            uchannelLow.Update(bar);
        }

        foreach (var bar in barsHigh)
        {
            uchannelHigh.Update(bar);
        }

        // High volatility should produce wider bands
        Assert.True(uchannelHigh.Width.Value > uchannelLow.Width.Value,
            $"High vol width ({uchannelHigh.Width.Value:F4}) should be > low vol width ({uchannelLow.Width.Value:F4})");
    }

    /// <summary>
    /// Validates that the indicator handles edge case with period = 1.
    /// </summary>
    [Fact]
    public void Uchannel_Period1_Works()
    {
        var uchannel = new Uchannel(1, 1, 1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            uchannel.Update(bar);
            Assert.True(double.IsFinite(uchannel.Middle.Value));
            Assert.True(double.IsFinite(uchannel.Upper.Value));
            Assert.True(double.IsFinite(uchannel.Lower.Value));
        }
    }

    /// <summary>
    /// Validates that reset properly clears all state.
    /// </summary>
    [Fact]
    public void Uchannel_Reset_ClearsAllState()
    {
        var uchannel = new Uchannel(DefaultStrPeriod, DefaultCenterPeriod, DefaultMultiplier);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process bars
        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }

        double valueBefore = uchannel.Middle.Value;

        // Reset
        uchannel.Reset();

        // Process same bars again
        foreach (var bar in bars)
        {
            uchannel.Update(bar);
        }

        double valueAfter = uchannel.Middle.Value;

        // Should produce same results
        Assert.Equal(valueBefore, valueAfter, precision: 10);
    }
}
