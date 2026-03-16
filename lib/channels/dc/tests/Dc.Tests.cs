using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class DcTests
{
    [Fact]
    public void Dc_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Dc(0));
        Assert.Throws<ArgumentException>(() => new Dc(-5));

        var d = new Dc(10);
        Assert.Equal(10, d.WarmupPeriod);
        Assert.Contains("Dc", d.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dc_InitialState_Defaults()
    {
        var d = new Dc(5);

        Assert.Equal(0, d.Last.Value);
        Assert.Equal(0, d.Upper.Value);
        Assert.Equal(0, d.Lower.Value);
        Assert.False(d.IsHot);
    }

    [Fact]
    public void Dc_CalculatesBands()
    {
        var d = new Dc(3);

        d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        d.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 110, 1000));
        d.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 115, 1000));

        // Highest High = 120, Lowest Low = 90, Middle = 105
        Assert.Equal(120.0, d.Upper.Value, 1e-10);
        Assert.Equal(90.0, d.Lower.Value, 1e-10);
        Assert.Equal(105.0, d.Last.Value, 1e-10);
        Assert.True(d.IsHot);
    }

    [Fact]
    public void Dc_SlidingWindow_Updates()
    {
        var d = new Dc(2);

        d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        d.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));
        double mid1 = d.Last.Value;

        d.Update(new TBar(DateTime.UtcNow, 102, 109, 95, 102, 1000));
        Assert.NotEqual(mid1, d.Last.Value);

        // Period=2: last 2 bars have H=[111,109], L=[91,95]
        // Upper=111, Lower=91, Middle=101
        Assert.Equal(111.0, d.Upper.Value, 1e-10);
        Assert.Equal(91.0, d.Lower.Value, 1e-10);
        Assert.Equal(101.0, d.Last.Value, 1e-10);
    }

    [Fact]
    public void Dc_IsHot_TurnsTrueAfterWarmup()
    {
        var d = new Dc(4);

        for (int i = 0; i < 3; i++)
        {
            d.Update(new TBar(DateTime.UtcNow, 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(d.IsHot);
        }

        d.Update(new TBar(DateTime.UtcNow, 200, 201, 199, 200, 1000));
        Assert.True(d.IsHot);
    }

    [Fact]
    public void Dc_IsNewFalse_RebuildsState()
    {
        var d = new Dc(3);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TBar remembered = default;
        for (int i = 0; i < 6; i++)
        {
            remembered = gbm.Next(isNew: true);
            d.Update(remembered, isNew: true);
        }

        double mid = d.Last.Value;
        double up = d.Upper.Value;
        double lo = d.Lower.Value;

        for (int i = 0; i < 3; i++)
        {
            var corrected = gbm.Next(isNew: false);
            d.Update(corrected, isNew: false);
        }

        d.Update(remembered, isNew: false);

        Assert.Equal(mid, d.Last.Value, 1e-10);
        Assert.Equal(up, d.Upper.Value, 1e-10);
        Assert.Equal(lo, d.Lower.Value, 1e-10);
    }

    [Fact]
    public void Dc_NaN_UsesLastValid()
    {
        var d = new Dc(3);

        d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        d.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 106, 1000));

        var result = d.Update(new TBar(DateTime.UtcNow, 102, double.NaN, 92, 107, 1000));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(d.Upper.Value));
        Assert.True(double.IsFinite(d.Lower.Value));

        var result2 = d.Update(new TBar(DateTime.UtcNow, 103, 113, double.PositiveInfinity, 108, 1000));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Dc_Reset_Clears()
    {
        var d = new Dc(3);
        d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        d.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));

        d.Reset();

        Assert.Equal(0, d.Last.Value);
        Assert.Equal(0, d.Upper.Value);
        Assert.Equal(0, d.Lower.Value);
        Assert.False(d.IsHot);

        d.Update(new TBar(DateTime.UtcNow, 50, 60, 40, 55, 1000));
        Assert.NotEqual(0, d.Last.Value);
    }

    [Fact]
    public void Dc_BatchVsStreaming_Match()
    {
        var dStream = new Dc(10);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TBarSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
            dStream.Update(bar, isNew: true);
        }

        double expectedMid = dStream.Last.Value;
        double expectedUp = dStream.Upper.Value;
        double expectedLo = dStream.Lower.Value;

        var (midBatch, upBatch, loBatch) = Dc.Batch(series, 10);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-10);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Dc_SpanBatch_Validates()
    {
        double[] high = [110, 115, 120];
        double[] low = [90, 95, 100];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] highShort = [110, 115];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentException>(() => Dc.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Dc.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() => Dc.Batch(highShort.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Dc.Batch(high.AsSpan(), low.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Dc_SpanBatch_ComputesCorrectly()
    {
        double[] high = [110, 115, 120, 125];
        double[] low = [90, 95, 100, 105];
        double[] middle = new double[4];
        double[] upper = new double[4];
        double[] lower = new double[4];

        Dc.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        // Period=3: index 2 is first valid (indices 0,1,2)
        // H=[110,115,120], L=[90,95,100] → Upper=120, Lower=90, Middle=105
        Assert.Equal(120.0, upper[2], 1e-10);
        Assert.Equal(90.0, lower[2], 1e-10);
        Assert.Equal(105.0, middle[2], 1e-10);

        // Index 3: H=[115,120,125], L=[95,100,105] → Upper=125, Lower=95, Middle=110
        Assert.Equal(125.0, upper[3], 1e-10);
        Assert.Equal(95.0, lower[3], 1e-10);
        Assert.Equal(110.0, middle[3], 1e-10);
    }

    [Fact]
    public void Dc_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);

        var ((mid, up, lo), ind) = Dc.Calculate(series, 2);

        Assert.True(ind.IsHot);
        // Period=2: last 2 bars H=[115,120], L=[95,100] → Upper=120, Lower=95, Middle=107.5
        Assert.Equal(120.0, up.Last.Value, 1e-10);
        Assert.Equal(95.0, lo.Last.Value, 1e-10);
        Assert.Equal(107.5, mid.Last.Value, 1e-10);

        ind.Update(new TBar(DateTime.UtcNow, 120, 130, 110, 120, 1000));
        // Period=2: last 2 bars H=[120,130], L=[100,110] → Upper=130, Lower=100, Middle=115
        Assert.Equal(130.0, ind.Upper.Value, 1e-10);
        Assert.Equal(100.0, ind.Lower.Value, 1e-10);
        Assert.Equal(115.0, ind.Last.Value, 1e-10);
    }

    [Fact]
    public void Dc_Event_Publishes()
    {
        var src = new TBarSeries();
        var d = new Dc(src, 2);
        bool fired = false;
        d.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(fired);
    }
}
