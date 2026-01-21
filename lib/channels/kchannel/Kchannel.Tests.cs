using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class KchannelTests
{
    [Fact]
    public void Kchannel_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kchannel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kchannel(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kchannel(10, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kchannel(10, -1.0));

        var k = new Kchannel(10, 2.0);
        Assert.Equal(20, k.WarmupPeriod); // period * 2
        Assert.Contains("Kchannel", k.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Kchannel_InitialState_Defaults()
    {
        var k = new Kchannel(5);

        Assert.Equal(0, k.Last.Value);
        Assert.Equal(0, k.Upper.Value);
        Assert.Equal(0, k.Lower.Value);
        Assert.False(k.IsHot);
    }

    [Fact]
    public void Kchannel_FirstBar_AllBandsEqualClose()
    {
        var k = new Kchannel(10, 2.0);

        var result = k.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));

        // First bar: EMA = close, ATR = 0, so all bands = close
        Assert.Equal(102.0, result.Value, 1e-10);
        Assert.Equal(102.0, k.Upper.Value, 1e-10);
        Assert.Equal(102.0, k.Lower.Value, 1e-10);
    }

    [Fact]
    public void Kchannel_SecondBar_BandsExpand()
    {
        var k = new Kchannel(10, 2.0);

        k.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));

        // Second bar with volatility
        _ = k.Update(new TBar(DateTime.UtcNow, 102, 110, 92, 102, 1000));

        // EMA shifts toward 102, ATR > 0, bands expand
        Assert.True(k.Upper.Value > k.Last.Value, "Upper should be above middle");
        Assert.True(k.Lower.Value < k.Last.Value, "Lower should be below middle");
    }

    [Fact]
    public void Kchannel_BandWidth_ProportionalToATR()
    {
        var k1 = new Kchannel(10, 1.0);
        var k2 = new Kchannel(10, 2.0);
        var k3 = new Kchannel(10, 3.0);

        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            k1.Update(bar);
            k2.Update(bar);
            k3.Update(bar);
        }

        double width1 = k1.Upper.Value - k1.Lower.Value;
        double width2 = k2.Upper.Value - k2.Lower.Value;
        double width3 = k3.Upper.Value - k3.Lower.Value;

        // Width should scale linearly with multiplier
        Assert.Equal(width2, width1 * 2, 1e-9);
        Assert.Equal(width3, width1 * 3, 1e-9);
    }

    [Fact]
    public void Kchannel_BandOrder_Correct()
    {
        var k = new Kchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.15, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            k.Update(bar);

            // After first bar, upper > middle > lower
            if (i > 0)
            {
                Assert.True(k.Upper.Value > k.Last.Value, $"Upper > Middle at bar {i}");
                Assert.True(k.Lower.Value < k.Last.Value, $"Lower < Middle at bar {i}");
            }
        }
    }

    [Fact]
    public void Kchannel_MiddleIsEMA()
    {
        var k = new Kchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = k.Update(bar);

            // Middle is EMA (returned value)
            Assert.Equal(result.Value, k.Last.Value, 1e-10);
        }
    }

    [Fact]
    public void Kchannel_BandSymmetry()
    {
        var k = new Kchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            k.Update(bar);

            // Bands should be symmetric around middle
            double upperDist = k.Upper.Value - k.Last.Value;
            double lowerDist = k.Last.Value - k.Lower.Value;
            Assert.Equal(upperDist, lowerDist, 1e-10);
        }
    }

    [Fact]
    public void Kchannel_IsHot_TurnsTrueAfterWarmup()
    {
        var k = new Kchannel(5);
        // WarmupPeriod = 5 * 2 = 10

        for (int i = 0; i < 9; i++)
        {
            k.Update(new TBar(DateTime.UtcNow, 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(k.IsHot);
        }

        k.Update(new TBar(DateTime.UtcNow, 200, 201, 199, 200, 1000));
        Assert.True(k.IsHot);
    }

    [Fact]
    public void Kchannel_IsNewFalse_RebuildsState()
    {
        var k = new Kchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TBar remembered = default;
        for (int i = 0; i < 30; i++)
        {
            remembered = gbm.Next(isNew: true);
            k.Update(remembered, isNew: true);
        }

        double mid = k.Last.Value;
        double up = k.Upper.Value;
        double lo = k.Lower.Value;

        // Apply corrections
        for (int i = 0; i < 5; i++)
        {
            var corrected = gbm.Next(isNew: false);
            k.Update(corrected, isNew: false);
        }

        // Restore with remembered bar
        k.Update(remembered, isNew: false);

        Assert.Equal(mid, k.Last.Value, 1e-10);
        Assert.Equal(up, k.Upper.Value, 1e-10);
        Assert.Equal(lo, k.Lower.Value, 1e-10);
    }

    [Fact]
    public void Kchannel_NaN_UsesLastValid()
    {
        var k = new Kchannel(10, 2.0);

        k.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        k.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 106, 1000));

        var result = k.Update(new TBar(DateTime.UtcNow, 102, double.NaN, 92, 107, 1000));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(k.Upper.Value));
        Assert.True(double.IsFinite(k.Lower.Value));

        var result2 = k.Update(new TBar(DateTime.UtcNow, 103, 113, double.PositiveInfinity, 108, 1000));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Kchannel_Reset_Clears()
    {
        var k = new Kchannel(10, 2.0);
        k.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        k.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));
        k.Update(new TBar(DateTime.UtcNow, 102, 112, 92, 102, 1000));

        k.Reset();

        Assert.Equal(0, k.Last.Value);
        Assert.Equal(0, k.Upper.Value);
        Assert.Equal(0, k.Lower.Value);
        Assert.False(k.IsHot);

        k.Update(new TBar(DateTime.UtcNow, 50, 60, 40, 55, 1000));
        Assert.NotEqual(0, k.Last.Value);
    }

    [Fact]
    public void Kchannel_BatchVsStreaming_Match()
    {
        var kStream = new Kchannel(20, 1.5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var series = new TBarSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
            kStream.Update(bar, isNew: true);
        }

        double expectedMid = kStream.Last.Value;
        double expectedUp = kStream.Upper.Value;
        double expectedLo = kStream.Lower.Value;

        var (midBatch, upBatch, loBatch) = Kchannel.Batch(series, 20, 1.5);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-10);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Kchannel_SpanBatch_Validates()
    {
        double[] high = [110, 115, 120];
        double[] low = [90, 95, 100];
        double[] close = [100, 105, 110];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] highShort = [110, 115];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentOutOfRangeException>(() => Kchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Kchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Kchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 10, 0.0));
        Assert.Throws<ArgumentException>(() => Kchannel.Batch(highShort.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Kchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Kchannel_SpanBatch_ComputesCorrectly()
    {
        double[] high = [105, 110, 115, 112, 118];
        double[] low = [95, 100, 105, 102, 108];
        double[] close = [100, 105, 110, 107, 115];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        Kchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

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
    public void Kchannel_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 102, 112, 92, 102, 1000);

        var ((mid, up, lo), ind) = Kchannel.Calculate(series, 2);

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
    public void Kchannel_Event_Publishes()
    {
        var src = new TBarSeries();
        var k = new Kchannel(src, 2);
        bool fired = false;
        k.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Kchannel_HighVolatility_WiderBands()
    {
        var kLow = new Kchannel(20, 2.0);
        var kHigh = new Kchannel(20, 2.0);

        // Low volatility data
        for (int i = 0; i < 50; i++)
        {
            kLow.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));
        }

        // High volatility data
        for (int i = 0; i < 50; i++)
        {
            kHigh.Update(new TBar(DateTime.UtcNow, 100, 120, 80, 100, 1000));
        }

        double lowWidth = kLow.Upper.Value - kLow.Lower.Value;
        double highWidth = kHigh.Upper.Value - kHigh.Lower.Value;

        Assert.True(highWidth > lowWidth, "Higher volatility should produce wider bands");
    }

    [Fact]
    public void Kchannel_ShorterPeriod_FasterResponse()
    {
        var kShort = new Kchannel(5, 2.0);
        var kLong = new Kchannel(20, 2.0);

        // Initial stable period
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow, 100, 102, 98, 100, 1000);
            kShort.Update(bar);
            kLong.Update(bar);
        }

        double shortInitial = kShort.Last.Value;
        double longInitial = kLong.Last.Value;

        // Sudden price jump
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(DateTime.UtcNow, 150, 152, 148, 150, 1000);
            kShort.Update(bar);
            kLong.Update(bar);
        }

        double shortMove = kShort.Last.Value - shortInitial;
        double longMove = kLong.Last.Value - longInitial;

        // Shorter period should respond faster
        Assert.True(shortMove > longMove, "Shorter period EMA should respond faster to price changes");
    }

    [Fact]
    public void Kchannel_TrueRange_IncludesGaps()
    {
        var k = new Kchannel(3, 2.0);

        // Bar 1: normal range
        k.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));

        // Bar 2: gap up (close was 100, now low is 110)
        // True range should include the gap: high - prevClose or high - low
        k.Update(new TBar(DateTime.UtcNow, 115, 120, 110, 115, 1000));

        // ATR should reflect the gap
        double width = k.Upper.Value - k.Lower.Value;
        Assert.True(width > 0, "Band width should be positive after gap");

        // Bar 3: another check
        k.Update(new TBar(DateTime.UtcNow, 118, 122, 114, 118, 1000));
        Assert.True(double.IsFinite(k.Upper.Value));
        Assert.True(double.IsFinite(k.Lower.Value));
    }

    [Fact]
    public void Kchannel_WarmupCompensation_ReducesStartupBias()
    {
        // Warmup compensation should make early values more accurate
        var k = new Kchannel(20, 2.0);

        // Create bars with consistent volatility
        for (int i = 0; i < 100; i++)
        {
            k.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        }

        // Middle should converge to close (100) as EMA stabilizes
        Assert.InRange(k.Last.Value, 99.5, 100.5);

        // Band width should stabilize (ATR converges to true range = 20)
        // Width = Upper - Lower = (EMA + mult*ATR) - (EMA - mult*ATR) = 2 * mult * ATR
        double expectedWidth = 2.0 * 2.0 * 20.0; // 2 * multiplier * ATR = 80
        double actualWidth = k.Upper.Value - k.Lower.Value;
        Assert.InRange(actualWidth, expectedWidth * 0.9, expectedWidth * 1.1);
    }

    [Fact]
    public void Kchannel_LongSeriesStability()
    {
        var k = new Kchannel(20, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.001, sigma: 0.02, seed: 123);

        for (int i = 0; i < 10000; i++)
        {
            var bar = gbm.Next(isNew: true);
            k.Update(bar);

            Assert.True(double.IsFinite(k.Last.Value), $"Middle finite at {i}");
            Assert.True(double.IsFinite(k.Upper.Value), $"Upper finite at {i}");
            Assert.True(double.IsFinite(k.Lower.Value), $"Lower finite at {i}");

            if (i > 0)
            {
                Assert.True(k.Upper.Value > k.Last.Value, $"Upper > Middle at {i}");
                Assert.True(k.Lower.Value < k.Last.Value, $"Lower < Middle at {i}");
            }
        }
    }
}
