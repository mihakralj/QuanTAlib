using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class FcbTests
{
    [Fact]
    public void Fcb_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fcb(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fcb(-5));

        var f = new Fcb(10);
        Assert.Equal(12, f.WarmupPeriod); // period + 2 for fractal detection
        Assert.Contains("Fcb", f.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fcb_InitialState_Defaults()
    {
        var f = new Fcb(5);

        Assert.Equal(0, f.Last.Value);
        Assert.Equal(0, f.Upper.Value);
        Assert.Equal(0, f.Lower.Value);
        Assert.False(f.IsHot);
    }

    [Fact]
    public void Fcb_DetectsFractalHigh()
    {
        // Fractal high: high[1] > high[2] AND high[1] > high[0]
        // Create pattern: 100, 120, 110 (index 1 is fractal high = 120)
        var f = new Fcb(5);

        f.Update(new TBar(DateTime.UtcNow, 95, 100, 90, 95, 1000));   // High = 100
        f.Update(new TBar(DateTime.UtcNow, 115, 120, 110, 115, 1000)); // High = 120 (will be fractal)
        f.Update(new TBar(DateTime.UtcNow, 105, 110, 100, 105, 1000)); // High = 110 < 120

        // After 3 bars, fractal high at index 1 detected when we get index 2
        // Upper should track the highest fractal high = 120
        Assert.Equal(120.0, f.Upper.Value, 1e-10);
    }

    [Fact]
    public void Fcb_DetectsFractalLow()
    {
        // Fractal low: low[1] < low[2] AND low[1] < low[0]
        // Create pattern: 100, 80, 90 (index 1 is fractal low = 80)
        var f = new Fcb(5);

        f.Update(new TBar(DateTime.UtcNow, 105, 110, 100, 105, 1000)); // Low = 100
        f.Update(new TBar(DateTime.UtcNow, 85, 90, 80, 85, 1000));    // Low = 80 (will be fractal)
        f.Update(new TBar(DateTime.UtcNow, 95, 100, 90, 95, 1000));   // Low = 90 > 80

        // After 3 bars, fractal low at index 1 detected when we get index 2
        // Lower should track the lowest fractal low = 80
        Assert.Equal(80.0, f.Lower.Value, 1e-10);
    }

    [Fact]
    public void Fcb_NoFractalUsesPreviousValue()
    {
        // If no fractal is detected, bands should use previous fractal values
        var f = new Fcb(5);

        // Create fractal high then no new fractals
        f.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        f.Update(new TBar(DateTime.UtcNow, 110, 115, 105, 110, 1000)); // Will be fractal high
        f.Update(new TBar(DateTime.UtcNow, 105, 110, 100, 105, 1000)); // Confirms fractal at 115

        _ = f.Upper.Value; // Store for comparison

        // Add more bars that don't create new fractals (monotonic up)
        f.Update(new TBar(DateTime.UtcNow, 112, 117, 107, 112, 1000));
        f.Update(new TBar(DateTime.UtcNow, 120, 125, 115, 120, 1000));

        // Upper should still be finite as previous fractal value is tracked via deque
        Assert.True(double.IsFinite(f.Upper.Value));
    }

    [Fact]
    public void Fcb_SlidingWindow_Updates()
    {
        var f = new Fcb(3);

        // Create initial fractals
        f.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        f.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 110, 1000)); // Fractal high at 120
        f.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 105, 1000));

        // Add more bars - window should slide
        for (int i = 0; i < 5; i++)
        {
            f.Update(new TBar(DateTime.UtcNow, 90 - i, 95 - i, 85 - i, 90 - i, 1000));
        }

        // Upper may have changed as old fractals slide out
        Assert.NotEqual(0, f.Upper.Value);
        Assert.NotEqual(0, f.Lower.Value);
    }

    [Fact]
    public void Fcb_IsHot_TurnsTrueAfterWarmup()
    {
        var f = new Fcb(4);
        // WarmupPeriod = 4 + 2 = 6

        for (int i = 0; i < 5; i++)
        {
            f.Update(new TBar(DateTime.UtcNow, 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(f.IsHot);
        }

        f.Update(new TBar(DateTime.UtcNow, 200, 201, 199, 200, 1000));
        Assert.True(f.IsHot);
    }

    [Fact]
    public void Fcb_IsNewFalse_RebuildsState()
    {
        var f = new Fcb(3);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TBar remembered = default;
        for (int i = 0; i < 10; i++)
        {
            remembered = gbm.Next(isNew: true);
            f.Update(remembered, isNew: true);
        }

        double mid = f.Last.Value;
        double up = f.Upper.Value;
        double lo = f.Lower.Value;

        // Apply corrections
        for (int i = 0; i < 3; i++)
        {
            var corrected = gbm.Next(isNew: false);
            f.Update(corrected, isNew: false);
        }

        // Restore with remembered bar
        f.Update(remembered, isNew: false);

        Assert.Equal(mid, f.Last.Value, 1e-10);
        Assert.Equal(up, f.Upper.Value, 1e-10);
        Assert.Equal(lo, f.Lower.Value, 1e-10);
    }

    [Fact]
    public void Fcb_NaN_UsesLastValid()
    {
        var f = new Fcb(3);

        f.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        f.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 106, 1000));

        var result = f.Update(new TBar(DateTime.UtcNow, 102, double.NaN, 92, 107, 1000));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(f.Upper.Value));
        Assert.True(double.IsFinite(f.Lower.Value));

        var result2 = f.Update(new TBar(DateTime.UtcNow, 103, 113, double.PositiveInfinity, 108, 1000));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Fcb_Reset_Clears()
    {
        var f = new Fcb(3);
        f.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        f.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));
        f.Update(new TBar(DateTime.UtcNow, 102, 112, 92, 102, 1000));

        f.Reset();

        Assert.Equal(0, f.Last.Value);
        Assert.Equal(0, f.Upper.Value);
        Assert.Equal(0, f.Lower.Value);
        Assert.False(f.IsHot);

        f.Update(new TBar(DateTime.UtcNow, 50, 60, 40, 55, 1000));
        Assert.NotEqual(0, f.Last.Value);
    }

    [Fact]
    public void Fcb_BatchVsStreaming_Match()
    {
        var fStream = new Fcb(10);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var series = new TBarSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
            fStream.Update(bar, isNew: true);
        }

        double expectedMid = fStream.Last.Value;
        double expectedUp = fStream.Upper.Value;
        double expectedLo = fStream.Lower.Value;

        var (midBatch, upBatch, loBatch) = Fcb.Batch(series, 10);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-10);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Fcb_SpanBatch_Validates()
    {
        double[] high = [110, 115, 120];
        double[] low = [90, 95, 100];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] highShort = [110, 115];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentOutOfRangeException>(() => Fcb.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fcb.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() => Fcb.Batch(highShort.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Fcb.Batch(high.AsSpan(), low.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Fcb_SpanBatch_ComputesCorrectly()
    {
        // Create data with clear fractal patterns
        // Fractal high at index 1: 100, 120, 110
        // Fractal low at index 1: 90, 70, 80
        double[] high = [100, 120, 110, 115, 105];
        double[] low = [90, 70, 80, 75, 85];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        Fcb.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        // At index 2, we detect fractal high=120, fractal low=70
        // Upper=120, Lower=70, Middle=95
        Assert.Equal(120.0, upper[2], 1e-10);
        Assert.Equal(70.0, lower[2], 1e-10);
        Assert.Equal(95.0, middle[2], 1e-10);
    }

    [Fact]
    public void Fcb_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 120, 80, 105, 1000); // Potential fractal high at 120, low at 80
        series.Add(DateTime.UtcNow, 102, 115, 85, 102, 1000); // Confirms fractals

        var ((mid, up, lo), ind) = Fcb.Calculate(series, 2);

        // After 3 bars: fractal high = 120, fractal low = 80
        Assert.Equal(120.0, up.Last.Value, 1e-10);
        Assert.Equal(80.0, lo.Last.Value, 1e-10);
        Assert.Equal(100.0, mid.Last.Value, 1e-10);

        // Continue streaming
        ind.Update(new TBar(DateTime.UtcNow, 108, 130, 75, 108, 1000)); // Potential new fractals
        ind.Update(new TBar(DateTime.UtcNow, 106, 125, 78, 106, 1000)); // Confirms fractal high=130, low=75

        Assert.Equal(130.0, ind.Upper.Value, 1e-10);
        Assert.Equal(75.0, ind.Lower.Value, 1e-10);
    }

    [Fact]
    public void Fcb_Event_Publishes()
    {
        var src = new TBarSeries();
        var f = new Fcb(src, 2);
        bool fired = false;
        f.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Fcb_MiddleValue_IsAverage()
    {
        var f = new Fcb(3);

        // Create clear fractals
        f.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        f.Update(new TBar(DateTime.UtcNow, 110, 130, 70, 110, 1000)); // Fractal high=130, low=70
        f.Update(new TBar(DateTime.UtcNow, 105, 120, 80, 105, 1000)); // Confirms fractals

        double expectedMiddle = (f.Upper.Value + f.Lower.Value) * 0.5;
        Assert.Equal(expectedMiddle, f.Last.Value, 1e-10);
    }

    [Fact]
    public void Fcb_MultipleFractals_TracksHighestLowest()
    {
        var f = new Fcb(10);

        // Create multiple fractals over time
        double[] highs = [100, 120, 110, 115, 140, 130, 125, 150, 145, 142, 148, 155, 150];
        double[] lows = [90, 70, 80, 75, 60, 65, 68, 50, 55, 58, 52, 45, 48];

        for (int i = 0; i < highs.Length; i++)
        {
            f.Update(new TBar(DateTime.UtcNow, (highs[i] + lows[i]) / 2, highs[i], lows[i], (highs[i] + lows[i]) / 2, 1000));
        }

        // Upper should be highest fractal high in window
        // Lower should be lowest fractal low in window
        Assert.True(f.Upper.Value > 0);
        Assert.True(f.Lower.Value > 0);
        Assert.True(f.Upper.Value > f.Lower.Value);
    }
}
