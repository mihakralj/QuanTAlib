using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class PcTests
{
    [Fact]
    public void Pc_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Pc(0));
        Assert.Throws<ArgumentException>(() => new Pc(-5));

        var pc = new Pc(10);
        Assert.Equal(10, pc.WarmupPeriod);
        Assert.Contains("Pc", pc.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pc_InitialState_Defaults()
    {
        var pc = new Pc(5);

        Assert.Equal(0, pc.Last.Value);
        Assert.Equal(0, pc.Upper.Value);
        Assert.Equal(0, pc.Lower.Value);
        Assert.False(pc.IsHot);
    }

    [Fact]
    public void Pc_CalculatesBands()
    {
        var pc = new Pc(3);

        pc.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        pc.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 110, 1000));
        pc.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 115, 1000));

        // Highest High = 120, Lowest Low = 90, Middle = 105
        Assert.Equal(120.0, pc.Upper.Value, 1e-10);
        Assert.Equal(90.0, pc.Lower.Value, 1e-10);
        Assert.Equal(105.0, pc.Last.Value, 1e-10);
        Assert.True(pc.IsHot);
    }

    [Fact]
    public void Pc_SlidingWindow_Updates()
    {
        var pc = new Pc(2);

        pc.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        pc.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));
        double lo1 = pc.Lower.Value;

        pc.Update(new TBar(DateTime.UtcNow, 102, 109, 95, 102, 1000));

        // Period=2: last 2 bars have H=[111,109], L=[91,95]
        // Upper=111, Lower=91, Middle=101
        Assert.Equal(111.0, pc.Upper.Value, 1e-10);
        Assert.Equal(91.0, pc.Lower.Value, 1e-10);
        Assert.Equal(101.0, pc.Last.Value, 1e-10);

        Assert.NotEqual(lo1, pc.Lower.Value);
    }

    [Fact]
    public void Pc_IsHot_TurnsTrueAfterWarmup()
    {
        var pc = new Pc(4);

        for (int i = 0; i < 3; i++)
        {
            pc.Update(new TBar(DateTime.UtcNow, 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(pc.IsHot);
        }

        pc.Update(new TBar(DateTime.UtcNow, 200, 201, 199, 200, 1000));
        Assert.True(pc.IsHot);
    }

    [Fact]
    public void Pc_IsNewFalse_RebuildsState()
    {
        var pc = new Pc(3);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TBar remembered = default;
        for (int i = 0; i < 6; i++)
        {
            remembered = gbm.Next(isNew: true);
            pc.Update(remembered, isNew: true);
        }

        double mid = pc.Last.Value;
        double up = pc.Upper.Value;
        double lo = pc.Lower.Value;

        for (int i = 0; i < 3; i++)
        {
            var corrected = gbm.Next(isNew: false);
            pc.Update(corrected, isNew: false);
        }

        pc.Update(remembered, isNew: false);

        Assert.Equal(mid, pc.Last.Value, 1e-10);
        Assert.Equal(up, pc.Upper.Value, 1e-10);
        Assert.Equal(lo, pc.Lower.Value, 1e-10);
    }

    [Fact]
    public void Pc_NaN_UsesLastValid()
    {
        var pc = new Pc(3);

        pc.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        pc.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 106, 1000));

        var result = pc.Update(new TBar(DateTime.UtcNow, 102, double.NaN, 92, 107, 1000));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(pc.Upper.Value));
        Assert.True(double.IsFinite(pc.Lower.Value));

        var result2 = pc.Update(new TBar(DateTime.UtcNow, 103, 113, double.PositiveInfinity, 108, 1000));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Pc_Reset_Clears()
    {
        var pc = new Pc(3);
        pc.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        pc.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));

        pc.Reset();

        Assert.Equal(0, pc.Last.Value);
        Assert.Equal(0, pc.Upper.Value);
        Assert.Equal(0, pc.Lower.Value);
        Assert.False(pc.IsHot);

        pc.Update(new TBar(DateTime.UtcNow, 50, 60, 40, 55, 1000));
        Assert.NotEqual(0, pc.Last.Value);
    }

    [Fact]
    public void Pc_BatchVsStreaming_Match()
    {
        var pcStream = new Pc(10);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TBarSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
            pcStream.Update(bar, isNew: true);
        }

        double expectedMid = pcStream.Last.Value;
        double expectedUp = pcStream.Upper.Value;
        double expectedLo = pcStream.Lower.Value;

        var (midBatch, upBatch, loBatch) = Pc.Batch(series, 10);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-10);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Pc_SpanBatch_Validates()
    {
        double[] high = [110, 115, 120];
        double[] low = [90, 95, 100];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] highShort = [110, 115];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentException>(() => Pc.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Pc.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() => Pc.Batch(highShort.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Pc.Batch(high.AsSpan(), low.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Pc_SpanBatch_ComputesCorrectly()
    {
        double[] high = [110, 115, 120, 125];
        double[] low = [90, 95, 100, 105];
        double[] middle = new double[4];
        double[] upper = new double[4];
        double[] lower = new double[4];

        Pc.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

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
    public void Pc_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);

        var ((mid, up, lo), ind) = Pc.Calculate(series, 2);

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
    public void Pc_Event_Publishes()
    {
        var src = new TBarSeries();
        var pc = new Pc(src, 2);
        bool fired = false;
        pc.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Pc_MiddleIsMidpoint()
    {
        var pc = new Pc(3);
        pc.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        pc.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 110, 1000));
        pc.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 115, 1000));

        double expectedMiddle = (pc.Upper.Value + pc.Lower.Value) / 2.0;
        Assert.Equal(expectedMiddle, pc.Last.Value, 1e-10);
    }

    [Fact]
    public void Pc_UpperGreaterOrEqualLower()
    {
        var pc = new Pc(5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 123);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            pc.Update(bar);
            Assert.True(pc.Upper.Value >= pc.Lower.Value,
                $"Bar {i}: Upper ({pc.Upper.Value}) should be >= Lower ({pc.Lower.Value})");
        }
    }
}
