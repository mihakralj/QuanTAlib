using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class StarchannelTests
{
    [Fact]
    public void Starchannel_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Starchannel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Starchannel(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Starchannel(10, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Starchannel(10, -1.0));

        var s = new Starchannel(10, 2.0);
        Assert.Equal(10, s.WarmupPeriod); // period (SMA warmup)
        Assert.Contains("Starchannel", s.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Starchannel_InitialState_Defaults()
    {
        var s = new Starchannel(5);

        Assert.Equal(0, s.Last.Value);
        Assert.Equal(0, s.Upper.Value);
        Assert.Equal(0, s.Lower.Value);
        Assert.False(s.IsHot);
    }

    [Fact]
    public void Starchannel_FirstBar_AllBandsEqualClose()
    {
        var s = new Starchannel(10, 2.0);

        var result = s.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));

        // First bar: SMA = close, ATR = 0, so all bands = close
        Assert.Equal(102.0, result.Value, 1e-10);
        Assert.Equal(102.0, s.Upper.Value, 1e-10);
        Assert.Equal(102.0, s.Lower.Value, 1e-10);
    }

    [Fact]
    public void Starchannel_SecondBar_BandsExpand()
    {
        var s = new Starchannel(10, 2.0);

        s.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));

        // Second bar with volatility
        _ = s.Update(new TBar(DateTime.UtcNow, 102, 110, 92, 102, 1000));

        // SMA shifts toward 101, ATR > 0, bands expand
        Assert.True(s.Upper.Value > s.Last.Value, "Upper should be above middle");
        Assert.True(s.Lower.Value < s.Last.Value, "Lower should be below middle");
    }

    [Fact]
    public void Starchannel_BandWidth_ProportionalToATR()
    {
        var s1 = new Starchannel(10, 1.0);
        var s2 = new Starchannel(10, 2.0);
        var s3 = new Starchannel(10, 3.0);

        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            s1.Update(bar);
            s2.Update(bar);
            s3.Update(bar);
        }

        double width1 = s1.Upper.Value - s1.Lower.Value;
        double width2 = s2.Upper.Value - s2.Lower.Value;
        double width3 = s3.Upper.Value - s3.Lower.Value;

        // Width should scale linearly with multiplier
        Assert.Equal(width2, width1 * 2, 1e-9);
        Assert.Equal(width3, width1 * 3, 1e-9);
    }

    [Fact]
    public void Starchannel_BandOrder_Correct()
    {
        var s = new Starchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.15, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            s.Update(bar);

            // After first bar, upper > middle > lower
            if (i > 0)
            {
                Assert.True(s.Upper.Value > s.Last.Value, $"Upper > Middle at bar {i}");
                Assert.True(s.Lower.Value < s.Last.Value, $"Lower < Middle at bar {i}");
            }
        }
    }

    [Fact]
    public void Starchannel_MiddleIsSMA()
    {
        var s = new Starchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = s.Update(bar);

            // Middle is SMA (returned value)
            Assert.Equal(result.Value, s.Last.Value, 1e-10);
        }
    }

    [Fact]
    public void Starchannel_BandSymmetry()
    {
        var s = new Starchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            s.Update(bar);

            // Bands should be symmetric around middle
            double upperDist = s.Upper.Value - s.Last.Value;
            double lowerDist = s.Last.Value - s.Lower.Value;
            Assert.Equal(upperDist, lowerDist, 1e-10);
        }
    }

    [Fact]
    public void Starchannel_IsHot_TurnsTrueAfterWarmup()
    {
        var s = new Starchannel(5);
        // WarmupPeriod = 5 (SMA period)

        for (int i = 0; i < 4; i++)
        {
            s.Update(new TBar(DateTime.UtcNow, 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(s.IsHot);
        }

        s.Update(new TBar(DateTime.UtcNow, 200, 201, 199, 200, 1000));
        Assert.True(s.IsHot);
    }

    [Fact]
    public void Starchannel_IsNewFalse_RebuildsState()
    {
        var s = new Starchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TBar remembered = default;
        for (int i = 0; i < 30; i++)
        {
            remembered = gbm.Next(isNew: true);
            s.Update(remembered, isNew: true);
        }

        double mid = s.Last.Value;
        double up = s.Upper.Value;
        double lo = s.Lower.Value;

        // Apply corrections
        for (int i = 0; i < 5; i++)
        {
            var corrected = gbm.Next(isNew: false);
            s.Update(corrected, isNew: false);
        }

        // Restore with remembered bar
        s.Update(remembered, isNew: false);

        Assert.Equal(mid, s.Last.Value, 1e-10);
        Assert.Equal(up, s.Upper.Value, 1e-10);
        Assert.Equal(lo, s.Lower.Value, 1e-10);
    }

    [Fact]
    public void Starchannel_NaN_UsesLastValid()
    {
        var s = new Starchannel(10, 2.0);

        s.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        s.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 106, 1000));

        var result = s.Update(new TBar(DateTime.UtcNow, 102, double.NaN, 92, 107, 1000));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(s.Upper.Value));
        Assert.True(double.IsFinite(s.Lower.Value));

        var result2 = s.Update(new TBar(DateTime.UtcNow, 103, 113, double.PositiveInfinity, 108, 1000));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Starchannel_Reset_Clears()
    {
        var s = new Starchannel(10, 2.0);
        s.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        s.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));
        s.Update(new TBar(DateTime.UtcNow, 102, 112, 92, 102, 1000));

        s.Reset();

        Assert.Equal(0, s.Last.Value);
        Assert.Equal(0, s.Upper.Value);
        Assert.Equal(0, s.Lower.Value);
        Assert.False(s.IsHot);

        s.Update(new TBar(DateTime.UtcNow, 50, 60, 40, 55, 1000));
        Assert.NotEqual(0, s.Last.Value);
    }

    [Fact]
    public void Starchannel_BatchVsStreaming_Match()
    {
        var sStream = new Starchannel(20, 1.5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var series = new TBarSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
            sStream.Update(bar, isNew: true);
        }

        double expectedMid = sStream.Last.Value;
        double expectedUp = sStream.Upper.Value;
        double expectedLo = sStream.Lower.Value;

        var (midBatch, upBatch, loBatch) = Starchannel.Batch(series, 20, 1.5);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-10);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Starchannel_SpanBatch_Validates()
    {
        double[] high = [110, 115, 120];
        double[] low = [90, 95, 100];
        double[] close = [100, 105, 110];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] highShort = [110, 115];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentOutOfRangeException>(() => Starchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Starchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Starchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 10, 0.0));
        Assert.Throws<ArgumentException>(() => Starchannel.Batch(highShort.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Starchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Starchannel_SpanBatch_ComputesCorrectly()
    {
        double[] high = [105, 110, 115, 112, 118];
        double[] low = [95, 100, 105, 102, 108];
        double[] close = [100, 105, 110, 107, 115];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        Starchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        // First bar: all equal close
        Assert.Equal(100.0, middle[0], 1e-10);
        Assert.Equal(100.0, upper[0], 1e-10);
        Assert.Equal(100.0, lower[0], 1e-10);

        // Subsequent bars: upper > middle > lower
        for (int i = 1; i < 5; i++)
        {
            Assert.True(upper[i] > middle[i], $"Upper > Middle at {i}");
            Assert.True(lower[i] < middle[i], $"Lower < Middle at {i}");
        }
    }

    [Fact]
    public void Starchannel_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 102, 112, 92, 102, 1000);

        var ((mid, up, lo), ind) = Starchannel.Calculate(series, 2);

        Assert.True(double.IsFinite(mid.Last.Value));
        Assert.True(double.IsFinite(up.Last.Value));
        Assert.True(double.IsFinite(lo.Last.Value));

        // Continue streaming
        ind.Update(new TBar(DateTime.UtcNow, 108, 118, 98, 108, 1000));
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
    }

    [Fact]
    public void Starchannel_Event_Publishes()
    {
        var src = new TBarSeries();
        var s = new Starchannel(src, 2);
        bool fired = false;
        s.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Starchannel_HighVolatility_WiderBands()
    {
        var sLow = new Starchannel(20, 2.0);
        var sHigh = new Starchannel(20, 2.0);

        // Low volatility data
        for (int i = 0; i < 50; i++)
        {
            sLow.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));
        }

        // High volatility data
        for (int i = 0; i < 50; i++)
        {
            sHigh.Update(new TBar(DateTime.UtcNow, 100, 120, 80, 100, 1000));
        }

        double lowWidth = sLow.Upper.Value - sLow.Lower.Value;
        double highWidth = sHigh.Upper.Value - sHigh.Lower.Value;

        Assert.True(highWidth > lowWidth, "Higher volatility should produce wider bands");
    }

    [Fact]
    public void Starchannel_ShorterPeriod_FasterResponse()
    {
        var sShort = new Starchannel(5, 2.0);
        var sLong = new Starchannel(20, 2.0);

        // Initial stable period
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow, 100, 102, 98, 100, 1000);
            sShort.Update(bar);
            sLong.Update(bar);
        }

        double shortInitial = sShort.Last.Value;
        double longInitial = sLong.Last.Value;

        // Sudden price jump
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(DateTime.UtcNow, 150, 152, 148, 150, 1000);
            sShort.Update(bar);
            sLong.Update(bar);
        }

        double shortMove = sShort.Last.Value - shortInitial;
        double longMove = sLong.Last.Value - longInitial;

        // Shorter period should respond faster
        Assert.True(shortMove > longMove, "Shorter period SMA should respond faster to price changes");
    }

    [Fact]
    public void Starchannel_TrueRange_IncludesGaps()
    {
        var s = new Starchannel(3, 2.0);

        // Bar 1: normal range
        s.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));

        // Bar 2: gap up (close was 100, now low is 110)
        // True range should include the gap: high - prevClose or high - low
        s.Update(new TBar(DateTime.UtcNow, 115, 120, 110, 115, 1000));

        // ATR should reflect the gap
        double width = s.Upper.Value - s.Lower.Value;
        Assert.True(width > 0, "Band width should be positive after gap");

        // Bar 3: another check
        s.Update(new TBar(DateTime.UtcNow, 118, 122, 114, 118, 1000));
        Assert.True(double.IsFinite(s.Upper.Value));
        Assert.True(double.IsFinite(s.Lower.Value));
    }

    [Fact]
    public void Starchannel_WarmupCompensation_ReducesStartupBias()
    {
        // Warmup compensation should make early values more accurate
        var s = new Starchannel(20, 2.0);

        // Create bars with consistent volatility
        for (int i = 0; i < 100; i++)
        {
            s.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        }

        // Middle should converge to close (100) as SMA stabilizes
        Assert.InRange(s.Last.Value, 99.5, 100.5);

        // Band width should stabilize (ATR converges to true range = 20)
        // Width = Upper - Lower = (SMA + mult*ATR) - (SMA - mult*ATR) = 2 * mult * ATR
        double expectedWidth = 2.0 * 2.0 * 20.0; // 2 * multiplier * ATR = 80
        double actualWidth = s.Upper.Value - s.Lower.Value;
        Assert.InRange(actualWidth, expectedWidth * 0.9, expectedWidth * 1.1);
    }

    [Fact]
    public void Starchannel_LongSeriesStability()
    {
        var s = new Starchannel(20, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.001, sigma: 0.02, seed: 123);

        for (int i = 0; i < 10000; i++)
        {
            var bar = gbm.Next(isNew: true);
            s.Update(bar);

            Assert.True(double.IsFinite(s.Last.Value), $"Middle finite at {i}");
            Assert.True(double.IsFinite(s.Upper.Value), $"Upper finite at {i}");
            Assert.True(double.IsFinite(s.Lower.Value), $"Lower finite at {i}");

            if (i > 0)
            {
                Assert.True(s.Upper.Value > s.Last.Value, $"Upper > Middle at {i}");
                Assert.True(s.Lower.Value < s.Last.Value, $"Lower < Middle at {i}");
            }
        }
    }

    [Fact]
    public void Starchannel_SMA_ConvergesToMean()
    {
        // SMA should converge to the mean price unlike EMA which weights recent more
        var s = new Starchannel(10, 2.0);

        // Feed constant price
        for (int i = 0; i < 20; i++)
        {
            s.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        }

        // SMA should be exactly 100 after enough bars
        Assert.Equal(100.0, s.Last.Value, 1e-10);
    }

    [Fact]
    public void Starchannel_SMA_EquallyWeightsWindow()
    {
        // SMA equally weights all bars in window, unlike EMA
        var s = new Starchannel(5, 2.0);

        // Feed prices 100, 110, 120, 130, 140 (mean = 120)
        s.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        s.Update(new TBar(DateTime.UtcNow, 110, 115, 105, 110, 1000));
        s.Update(new TBar(DateTime.UtcNow, 120, 125, 115, 120, 1000));
        s.Update(new TBar(DateTime.UtcNow, 130, 135, 125, 130, 1000));
        s.Update(new TBar(DateTime.UtcNow, 140, 145, 135, 140, 1000));

        // SMA(5) = (100+110+120+130+140)/5 = 120
        Assert.Equal(120.0, s.Last.Value, 1e-10);

        // Add one more: window shifts to 110,120,130,140,150 -> mean = 130
        s.Update(new TBar(DateTime.UtcNow, 150, 155, 145, 150, 1000));
        Assert.Equal(130.0, s.Last.Value, 1e-10);
    }
}
