using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class MmchannelTests
{
    [Fact]
    public void Mmchannel_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mmchannel(0));
        Assert.Throws<ArgumentException>(() => new Mmchannel(-5));

        var mm = new Mmchannel(10);
        Assert.Equal(10, mm.WarmupPeriod);
        Assert.Contains("Mmchannel", mm.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mmchannel_InitialState_Defaults()
    {
        var mm = new Mmchannel(5);

        Assert.Equal(0, mm.Last.Value);
        Assert.Equal(0, mm.Upper.Value);
        Assert.Equal(0, mm.Lower.Value);
        Assert.False(mm.IsHot);
    }

    [Fact]
    public void Mmchannel_CalculatesBands()
    {
        var mm = new Mmchannel(3);

        mm.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        mm.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 110, 1000));
        mm.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 115, 1000));

        // Highest High = 120, Lowest Low = 90
        Assert.Equal(120.0, mm.Upper.Value, 1e-10);
        Assert.Equal(90.0, mm.Lower.Value, 1e-10);
        Assert.True(mm.IsHot);
    }

    [Fact]
    public void Mmchannel_SlidingWindow_Updates()
    {
        var mm = new Mmchannel(2);

        mm.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        mm.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));
        double lo1 = mm.Lower.Value;

        // Period=2: after 3 bars, oldest bar drops; new max/min calculated
        mm.Update(new TBar(DateTime.UtcNow, 102, 109, 95, 102, 1000));

        // Period=2: last 2 bars have H=[111,109], L=[91,95]
        // Upper=111, Lower=91
        Assert.Equal(111.0, mm.Upper.Value, 1e-10);
        Assert.Equal(91.0, mm.Lower.Value, 1e-10);

        // Lower changed from 90 to 91 after first bar dropped
        Assert.NotEqual(lo1, mm.Lower.Value);
    }

    [Fact]
    public void Mmchannel_IsHot_TurnsTrueAfterWarmup()
    {
        var mm = new Mmchannel(4);

        for (int i = 0; i < 3; i++)
        {
            mm.Update(new TBar(DateTime.UtcNow, 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(mm.IsHot);
        }

        mm.Update(new TBar(DateTime.UtcNow, 200, 201, 199, 200, 1000));
        Assert.True(mm.IsHot);
    }

    [Fact]
    public void Mmchannel_IsNewFalse_RebuildsState()
    {
        var mm = new Mmchannel(3);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        TBar remembered = default;
        for (int i = 0; i < 6; i++)
        {
            remembered = gbm.Next(isNew: true);
            mm.Update(remembered, isNew: true);
        }

        double up = mm.Upper.Value;
        double lo = mm.Lower.Value;

        for (int i = 0; i < 3; i++)
        {
            var corrected = gbm.Next(isNew: false);
            mm.Update(corrected, isNew: false);
        }

        mm.Update(remembered, isNew: false);

        Assert.Equal(up, mm.Upper.Value, 1e-10);
        Assert.Equal(lo, mm.Lower.Value, 1e-10);
    }

    [Fact]
    public void Mmchannel_NaN_UsesLastValid()
    {
        var mm = new Mmchannel(3);

        mm.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        mm.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 106, 1000));

        var result = mm.Update(new TBar(DateTime.UtcNow, 102, double.NaN, 92, 107, 1000));
        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(mm.Upper.Value));
        Assert.True(double.IsFinite(mm.Lower.Value));

        var result2 = mm.Update(new TBar(DateTime.UtcNow, 103, 113, double.PositiveInfinity, 108, 1000));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Mmchannel_Reset_Clears()
    {
        var mm = new Mmchannel(3);
        mm.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        mm.Update(new TBar(DateTime.UtcNow, 101, 111, 91, 101, 1000));

        mm.Reset();

        Assert.Equal(0, mm.Last.Value);
        Assert.Equal(0, mm.Upper.Value);
        Assert.Equal(0, mm.Lower.Value);
        Assert.False(mm.IsHot);

        mm.Update(new TBar(DateTime.UtcNow, 50, 60, 40, 55, 1000));
        Assert.NotEqual(0, mm.Last.Value);
    }

    [Fact]
    public void Mmchannel_BatchVsStreaming_Match()
    {
        var mmStream = new Mmchannel(10);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TBarSeries();

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
            mmStream.Update(bar, isNew: true);
        }

        double expectedUp = mmStream.Upper.Value;
        double expectedLo = mmStream.Lower.Value;

        var (upBatch, loBatch) = Mmchannel.Batch(series, 10);

        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Mmchannel_SpanBatch_Validates()
    {
        double[] high = [110, 115, 120];
        double[] low = [90, 95, 100];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] highShort = [110, 115];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentException>(() => Mmchannel.Batch(high.AsSpan(), low.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Mmchannel.Batch(high.AsSpan(), low.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() => Mmchannel.Batch(highShort.AsSpan(), low.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Mmchannel.Batch(high.AsSpan(), low.AsSpan(), smallOut.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Mmchannel_SpanBatch_ComputesCorrectly()
    {
        double[] high = [110, 115, 120, 125];
        double[] low = [90, 95, 100, 105];
        double[] upper = new double[4];
        double[] lower = new double[4];

        Mmchannel.Batch(high.AsSpan(), low.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        // Period=3: index 2 is first valid (indices 0,1,2)
        // H=[110,115,120], L=[90,95,100] → Upper=120, Lower=90
        Assert.Equal(120.0, upper[2], 1e-10);
        Assert.Equal(90.0, lower[2], 1e-10);

        // Index 3: H=[115,120,125], L=[95,100,105] → Upper=125, Lower=95
        Assert.Equal(125.0, upper[3], 1e-10);
        Assert.Equal(95.0, lower[3], 1e-10);
    }

    [Fact]
    public void Mmchannel_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);

        var ((up, lo), ind) = Mmchannel.Calculate(series, 2);

        Assert.True(ind.IsHot);
        // Period=2: last 2 bars H=[115,120], L=[95,100] → Upper=120, Lower=95
        Assert.Equal(120.0, up.Last.Value, 1e-10);
        Assert.Equal(95.0, lo.Last.Value, 1e-10);

        ind.Update(new TBar(DateTime.UtcNow, 120, 130, 110, 120, 1000));
        // Period=2: last 2 bars H=[120,130], L=[100,110] → Upper=130, Lower=100
        Assert.Equal(130.0, ind.Upper.Value, 1e-10);
        Assert.Equal(100.0, ind.Lower.Value, 1e-10);
    }

    [Fact]
    public void Mmchannel_Event_Publishes()
    {
        var src = new TBarSeries();
        var mm = new Mmchannel(src, 2);
        bool fired = false;
        mm.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Mmchannel_UpperEqualsLastValue()
    {
        var mm = new Mmchannel(3);
        mm.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        mm.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 110, 1000));
        mm.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 115, 1000));

        // Last should equal Upper for single-value compatibility
        Assert.Equal(mm.Upper.Value, mm.Last.Value);
    }

    [Fact]
    public void Mmchannel_UpperGreaterOrEqualLower()
    {
        var mm = new Mmchannel(5);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.2, seed: 123);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            mm.Update(bar);
            Assert.True(mm.Upper.Value >= mm.Lower.Value,
                $"Bar {i}: Upper ({mm.Upper.Value}) should be >= Lower ({mm.Lower.Value})");
        }
    }
}
