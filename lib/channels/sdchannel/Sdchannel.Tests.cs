using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class SdchannelTests
{
    [Fact]
    public void Sdchannel_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sdchannel(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sdchannel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sdchannel(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sdchannel(10, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sdchannel(10, -1.0));

        var s = new Sdchannel(10, 2.0);
        Assert.Equal(10, s.WarmupPeriod);
        Assert.Contains("Sdchannel", s.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sdchannel_InitialState_Defaults()
    {
        var s = new Sdchannel(5);

        Assert.Equal(0, s.Last.Value);
        Assert.Equal(0, s.Upper.Value);
        Assert.Equal(0, s.Lower.Value);
        Assert.False(s.IsHot);
        Assert.Equal(0, s.Slope);
        Assert.Equal(0, s.StdDev);
    }

    [Fact]
    public void Sdchannel_FirstValue_AllBandsEqual()
    {
        var s = new Sdchannel(10, 2.0);

        var result = s.Update(new TValue(DateTime.UtcNow, 100));

        // First value: regression = input, stdDev = 0, bands equal
        Assert.Equal(100.0, result.Value, 1e-10);
        Assert.Equal(100.0, s.Upper.Value, 1e-10);
        Assert.Equal(100.0, s.Lower.Value, 1e-10);
        Assert.Equal(0.0, s.Slope, 1e-10);
        Assert.Equal(0.0, s.StdDev, 1e-10);
    }

    [Fact]
    public void Sdchannel_TwoValues_LinearFit()
    {
        var s = new Sdchannel(10, 2.0);

        s.Update(new TValue(DateTime.UtcNow, 100));
        var result = s.Update(new TValue(DateTime.UtcNow, 110));

        // Two points: y=100 at x=0, y=110 at x=1
        // Regression line: y = 100 + 10*x
        // At x=1: regression = 110
        // Both points lie exactly on line, so stdDev = 0
        Assert.Equal(110.0, result.Value, 1e-10);
        Assert.Equal(10.0, s.Slope, 1e-10);
        Assert.Equal(0.0, s.StdDev, 1e-10);
        Assert.Equal(110.0, s.Upper.Value, 1e-10);
        Assert.Equal(110.0, s.Lower.Value, 1e-10);
    }

    [Fact]
    public void Sdchannel_ThreeValues_WithResiduals()
    {
        var s = new Sdchannel(10, 1.0);

        // Points: (0,100), (1,120), (2,110)
        // Sum x = 0+1+2 = 3, Sum x² = 0+1+4 = 5
        // Sum y = 330, Sum xy = 0*100 + 1*120 + 2*110 = 340
        // n=3, denom = 3*5 - 3*3 = 6
        // slope = (3*340 - 3*330) / 6 = (1020 - 990) / 6 = 5
        // intercept = (330 - 5*3) / 3 = 315/3 = 105
        // regression at x=2: 105 + 5*2 = 115
        s.Update(new TValue(DateTime.UtcNow, 100));
        s.Update(new TValue(DateTime.UtcNow, 120));
        var result = s.Update(new TValue(DateTime.UtcNow, 110));

        Assert.Equal(115.0, result.Value, 1e-10);
        Assert.Equal(5.0, s.Slope, 1e-10);

        // Residuals: 100-105=-5, 120-110=10, 110-115=-5
        // Sum residuals² = 25+100+25 = 150
        // StdDev = sqrt(150/3) = sqrt(50) ≈ 7.07
        double expectedStdDev = Math.Sqrt(50);
        Assert.Equal(expectedStdDev, s.StdDev, 1e-10);

        // Bands at ±1 stdDev
        Assert.Equal(115.0 + expectedStdDev, s.Upper.Value, 1e-10);
        Assert.Equal(115.0 - expectedStdDev, s.Lower.Value, 1e-10);
    }

    [Fact]
    public void Sdchannel_BandWidth_ProportionalToMultiplier()
    {
        var s1 = new Sdchannel(10, 1.0);
        var s2 = new Sdchannel(10, 2.0);
        var s3 = new Sdchannel(10, 3.0);

        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            s1.Update(tv);
            s2.Update(tv);
            s3.Update(tv);
        }

        double width1 = s1.Upper.Value - s1.Lower.Value;
        double width2 = s2.Upper.Value - s2.Lower.Value;
        double width3 = s3.Upper.Value - s3.Lower.Value;

        // Width should scale with multiplier (width = 2 * multiplier * stdDev)
        Assert.Equal(width2, width1 * 2, 1e-9);
        Assert.Equal(width3, width1 * 3, 1e-9);
    }

    [Fact]
    public void Sdchannel_BandOrder_Correct()
    {
        var s = new Sdchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            s.Update(new TValue(bar.Time, bar.Close));

            // After warmup with real data, bands should separate
            if (i > 3 && s.StdDev > 0)
            {
                Assert.True(s.Upper.Value >= s.Last.Value, $"Upper >= Middle at bar {i}");
                Assert.True(s.Lower.Value <= s.Last.Value, $"Lower <= Middle at bar {i}");
            }
        }
    }

    [Fact]
    public void Sdchannel_BandSymmetry_AroundRegression()
    {
        var s = new Sdchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            s.Update(new TValue(bar.Time, bar.Close));

            // Bands should be symmetric around middle
            double upperDist = s.Upper.Value - s.Last.Value;
            double lowerDist = s.Last.Value - s.Lower.Value;
            Assert.Equal(upperDist, lowerDist, 1e-10);

            // Distance should be exactly multiplier * stdDev
            double expectedDist = 2.0 * s.StdDev;
            Assert.Equal(expectedDist, upperDist, 1e-10);
        }
    }

    [Fact]
    public void Sdchannel_ConstantValues_ZeroStdDev()
    {
        var s = new Sdchannel(5, 2.0);

        for (int i = 0; i < 20; i++)
        {
            s.Update(new TValue(DateTime.UtcNow, 100));
        }

        // All same values on regression line -> no residuals
        Assert.Equal(100.0, s.Last.Value, 1e-10);
        Assert.Equal(0.0, s.Slope, 1e-10);
        Assert.Equal(0.0, s.StdDev, 1e-10);
        Assert.Equal(100.0, s.Upper.Value, 1e-10);
        Assert.Equal(100.0, s.Lower.Value, 1e-10);
    }

    [Fact]
    public void Sdchannel_LinearTrend_ZeroStdDev()
    {
        var s = new Sdchannel(5, 2.0);

        // Perfect linear trend: 100, 102, 104, 106, 108
        for (int i = 0; i < 5; i++)
        {
            s.Update(new TValue(DateTime.UtcNow, 100 + i * 2));
        }

        // All points lie exactly on regression line
        Assert.Equal(108.0, s.Last.Value, 1e-10);
        Assert.Equal(2.0, s.Slope, 1e-10);
        Assert.Equal(0.0, s.StdDev, 1e-10);
    }

    [Fact]
    public void Sdchannel_IsHot_TurnsTrueAfterWarmup()
    {
        var s = new Sdchannel(5, 2.0);

        for (int i = 0; i < 4; i++)
        {
            s.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(s.IsHot);
        }

        s.Update(new TValue(DateTime.UtcNow, 200));
        Assert.True(s.IsHot);
    }

    [Fact]
    public void Sdchannel_IsNewFalse_RebuildsState()
    {
        var s = new Sdchannel(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TValue remembered = default;
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            remembered = new TValue(bar.Time, bar.Close);
            s.Update(remembered, isNew: true);
        }

        double mid = s.Last.Value;
        double up = s.Upper.Value;
        double lo = s.Lower.Value;
        double slope = s.Slope;
        double stdDev = s.StdDev;

        // Apply corrections
        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            s.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Restore with remembered value
        s.Update(remembered, isNew: false);

        Assert.Equal(mid, s.Last.Value, 1e-6);
        Assert.Equal(up, s.Upper.Value, 1e-6);
        Assert.Equal(lo, s.Lower.Value, 1e-6);
        Assert.Equal(slope, s.Slope, 1e-6);
        Assert.Equal(stdDev, s.StdDev, 1e-6);
    }

    [Fact]
    public void Sdchannel_NaN_UsesLastValid()
    {
        var s = new Sdchannel(10, 2.0);

        s.Update(new TValue(DateTime.UtcNow, 100));
        s.Update(new TValue(DateTime.UtcNow, 105));

        var result = s.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(s.Upper.Value));
        Assert.True(double.IsFinite(s.Lower.Value));

        var result2 = s.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Sdchannel_BarCorrection_UpdatesLastValid()
    {
        // Verifies that bar correction (isNew:false) with a finite value updates LastValid,
        // so subsequent NaN/Inf inputs use the corrected value, not the pre-correction value.
        var s = new Sdchannel(5, 2.0);
        var now = DateTime.UtcNow;

        // Feed initial values
        s.Update(new TValue(now, 100));
        s.Update(new TValue(now, 110));
        s.Update(new TValue(now, 120));

        // Last bar: 130 (LastValid should be 130)
        s.Update(new TValue(now, 130));

        // Correct the last bar with isNew:false to 140 (should update LastValid to 140)
        s.Update(new TValue(now, 140), isNew: false);

        // Now send NaN - it should use LastValid=140, not the old 130
        var resultWithNaN = s.Update(new TValue(now, double.NaN));

        // The regression should include 100, 110, 120, 140 (the corrected value)
        // If bug existed, it would use 130 instead
        Assert.True(double.IsFinite(resultWithNaN.Value));

        // Verify by checking the buffer contains the corrected value
        // The regression endpoint should reflect using 140 not 130
        // For 4 values [100, 110, 120, 140]:
        // sumX = 0+1+2+3 = 6, sumX² = 14, n=4
        // sumY = 470, sumXY = 0*100 + 1*110 + 2*120 + 3*140 = 770
        // denom = 4*14 - 36 = 20
        // slope = (4*770 - 6*470) / 20 = (3080 - 2820) / 20 = 13
        // intercept = (470 - 13*6) / 4 = (470 - 78) / 4 = 98
        // regression at x=3: 98 + 13*3 = 137
        Assert.Equal(137.0, resultWithNaN.Value, 1e-9);
    }

    [Fact]
    public void Sdchannel_Reset_Clears()
    {
        var s = new Sdchannel(10, 2.0);
        s.Update(new TValue(DateTime.UtcNow, 100));
        s.Update(new TValue(DateTime.UtcNow, 110));
        s.Update(new TValue(DateTime.UtcNow, 120));

        s.Reset();

        Assert.Equal(0, s.Last.Value);
        Assert.Equal(0, s.Upper.Value);
        Assert.Equal(0, s.Lower.Value);
        Assert.Equal(0, s.Slope);
        Assert.Equal(0, s.StdDev);
        Assert.False(s.IsHot);

        s.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, s.Last.Value);
    }

    [Fact]
    public void Sdchannel_BatchVsStreaming_Match()
    {
        var sStream = new Sdchannel(20, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close));
            sStream.Update(series.Last, isNew: true);
        }

        double expectedMid = sStream.Last.Value;
        double expectedUp = sStream.Upper.Value;
        double expectedLo = sStream.Lower.Value;

        var (midBatch, upBatch, loBatch) = Sdchannel.Batch(series, 20, 2.0);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-9);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-9);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-9);
    }

    [Fact]
    public void Sdchannel_SpanBatch_Validates()
    {
        double[] source = [100, 105, 110];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentOutOfRangeException>(() => Sdchannel.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sdchannel.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sdchannel.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 10, 0.0));
        Assert.Throws<ArgumentException>(() => Sdchannel.Batch(source.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Sdchannel_SpanBatch_ComputesCorrectly()
    {
        double[] source = [100, 110, 100, 110, 100];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        Sdchannel.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, 2.0);

        // First value: regression = 100, stdDev = 0
        Assert.Equal(100.0, middle[0], 1e-10);
        Assert.Equal(100.0, upper[0], 1e-10);
        Assert.Equal(100.0, lower[0], 1e-10);

        // When stdDev > 0, bands should be symmetric around middle
        for (int i = 0; i < 5; i++)
        {
            double upperDist = upper[i] - middle[i];
            double lowerDist = middle[i] - lower[i];
            Assert.Equal(upperDist, lowerDist, 1e-10);
        }
    }

    [Fact]
    public void Sdchannel_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TSeries();
        series.Add(new TValue(DateTime.UtcNow, 100));
        series.Add(new TValue(DateTime.UtcNow, 105));
        series.Add(new TValue(DateTime.UtcNow, 102));

        var ((mid, up, lo), ind) = Sdchannel.Calculate(series, 2);

        Assert.True(double.IsFinite(mid.Last.Value));
        Assert.True(double.IsFinite(up.Last.Value));
        Assert.True(double.IsFinite(lo.Last.Value));

        // Continue streaming
        ind.Update(new TValue(DateTime.UtcNow, 108));
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Sdchannel_Event_Publishes()
    {
        var src = new TSeries();
        var s = new Sdchannel(src, 2);
        bool fired = false;
        s.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(fired);
    }

    [Fact]
    public void Sdchannel_LongSeriesStability()
    {
        var s = new Sdchannel(20, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.001, sigma: 0.02, seed: 123);

        for (int i = 0; i < 10000; i++)
        {
            var bar = gbm.Next(isNew: true);
            s.Update(new TValue(bar.Time, bar.Close));

            Assert.True(double.IsFinite(s.Last.Value), $"Middle finite at {i}");
            Assert.True(double.IsFinite(s.Upper.Value), $"Upper finite at {i}");
            Assert.True(double.IsFinite(s.Lower.Value), $"Lower finite at {i}");
            Assert.True(double.IsFinite(s.Slope), $"Slope finite at {i}");
            Assert.True(double.IsFinite(s.StdDev), $"StdDev finite at {i}");
        }
    }

    [Fact]
    public void Sdchannel_SlidingWindow_PeriodRespected()
    {
        var s = new Sdchannel(3, 2.0);

        // Feed 5 values: 100, 200, 300, 400, 500
        s.Update(new TValue(DateTime.UtcNow, 100));
        s.Update(new TValue(DateTime.UtcNow, 200));
        s.Update(new TValue(DateTime.UtcNow, 300));
        s.Update(new TValue(DateTime.UtcNow, 400));
        s.Update(new TValue(DateTime.UtcNow, 500));

        // Window should now contain: 300, 400, 500
        // Perfect linear trend with slope = 100
        Assert.Equal(500.0, s.Last.Value, 1e-10);
        Assert.Equal(100.0, s.Slope, 1e-10);
        Assert.Equal(0.0, s.StdDev, 1e-10);
    }
}
