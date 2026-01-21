using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests;

public class DecaychannelTests
{
    [Fact]
    public void Decaychannel_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Decaychannel(0));
        Assert.Throws<ArgumentException>(() => new Decaychannel(-5));

        var d = new Decaychannel(10);
        Assert.Equal(10, d.WarmupPeriod);
        Assert.Contains("Decaychannel", d.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decaychannel_InitialState_Defaults()
    {
        var d = new Decaychannel(5);

        Assert.Equal(0, d.Last.Value);
        Assert.Equal(0, d.Upper.Value);
        Assert.Equal(0, d.Lower.Value);
        Assert.False(d.IsHot);
    }

    [Fact]
    public void Decaychannel_CalculatesBands()
    {
        var d = new Decaychannel(3);

        d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        d.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 110, 1000));
        d.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 115, 1000));

        // Bands should be within valid range
        Assert.True(double.IsFinite(d.Upper.Value));
        Assert.True(double.IsFinite(d.Lower.Value));
        Assert.True(double.IsFinite(d.Last.Value));

        // Upper >= Lower, Middle in between
        Assert.True(d.Upper.Value >= d.Lower.Value);
        Assert.True(d.Last.Value >= d.Lower.Value);
        Assert.True(d.Last.Value <= d.Upper.Value);

        Assert.True(d.IsHot);
    }

    [Fact]
    public void Decaychannel_DecaysBands_OverTime()
    {
        var d = new Decaychannel(10);

        // Establish initial extremes
        d.Update(new TBar(DateTime.UtcNow, 100, 120, 80, 100, 1000));

        double initialUpper = d.Upper.Value;
        double initialLower = d.Lower.Value;
        double initialWidth = initialUpper - initialLower;

        // Feed flat bars - no new extremes
        for (int i = 0; i < 5; i++)
        {
            d.Update(new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000));
        }

        double laterWidth = d.Upper.Value - d.Lower.Value;

        // Channel should have contracted (decayed toward midpoint)
        Assert.True(laterWidth <= initialWidth, $"Channel should decay. Initial width: {initialWidth}, Later width: {laterWidth}");
    }

    [Fact]
    public void Decaychannel_ExpandsOnNewExtreme()
    {
        var d = new Decaychannel(5);

        // Initial bars
        for (int i = 0; i < 5; i++)
        {
            d.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        }

        double widthBefore = d.Upper.Value - d.Lower.Value;

        // New extreme high
        d.Update(new TBar(DateTime.UtcNow, 100, 130, 95, 100, 1000));

        double widthAfter = d.Upper.Value - d.Lower.Value;

        // Channel should have expanded
        Assert.True(widthAfter >= widthBefore);
    }

    [Fact]
    public void Decaychannel_IsHot_TurnsTrueAfterWarmup()
    {
        var d = new Decaychannel(4);

        for (int i = 0; i < 3; i++)
        {
            d.Update(new TBar(DateTime.UtcNow, 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(d.IsHot);
        }

        d.Update(new TBar(DateTime.UtcNow, 200, 201, 199, 200, 1000));
        Assert.True(d.IsHot);
    }

    [Fact]
    public void Decaychannel_IsNewFalse_RestoresState()
    {
        var d = new Decaychannel(3);
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

        // Several corrections
        for (int i = 0; i < 3; i++)
        {
            var corrected = gbm.Next(isNew: false);
            d.Update(corrected, isNew: false);
        }

        // Restore to remembered bar
        d.Update(remembered, isNew: false);

        Assert.Equal(mid, d.Last.Value, 1e-10);
        Assert.Equal(up, d.Upper.Value, 1e-10);
        Assert.Equal(lo, d.Lower.Value, 1e-10);
    }

    [Fact]
    public void Decaychannel_NaN_UsesLastValid()
    {
        var d = new Decaychannel(3);

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
    public void Decaychannel_Reset_Clears()
    {
        var d = new Decaychannel(3);
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
    public void Decaychannel_BatchVsStreaming_Match()
    {
        var dStream = new Decaychannel(10);
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

        var (midBatch, upBatch, loBatch) = Decaychannel.Batch(series, 10);

        Assert.Equal(expectedMid, midBatch.Last.Value, 1e-10);
        Assert.Equal(expectedUp, upBatch.Last.Value, 1e-10);
        Assert.Equal(expectedLo, loBatch.Last.Value, 1e-10);
    }

    [Fact]
    public void Decaychannel_SpanBatch_Validates()
    {
        double[] high = [110, 115, 120];
        double[] low = [90, 95, 100];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] highShort = [110, 115];
        double[] smallOut = new double[1];

        Assert.Throws<ArgumentException>(() => Decaychannel.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Decaychannel.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() => Decaychannel.Batch(highShort.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
        Assert.Throws<ArgumentException>(() => Decaychannel.Batch(high.AsSpan(), low.AsSpan(), smallOut.AsSpan(), upper.AsSpan(), lower.AsSpan(), 2));
    }

    [Fact]
    public void Decaychannel_SpanBatch_ComputesFiniteValues()
    {
        double[] high = [110, 115, 120, 125, 115, 110, 108];
        double[] low = [90, 95, 100, 105, 95, 90, 88];
        double[] middle = new double[7];
        double[] upper = new double[7];
        double[] lower = new double[7];

        Decaychannel.Batch(high.AsSpan(), low.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        for (int i = 0; i < 7; i++)
        {
            Assert.True(double.IsFinite(middle[i]), $"middle[{i}] should be finite");
            Assert.True(double.IsFinite(upper[i]), $"upper[{i}] should be finite");
            Assert.True(double.IsFinite(lower[i]), $"lower[{i}] should be finite");
            Assert.True(upper[i] >= lower[i], $"upper[{i}] >= lower[{i}]");
        }
    }

    [Fact]
    public void Decaychannel_Calculate_ReturnsIndicatorAndResults()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);

        var ((mid, up, lo), ind) = Decaychannel.Calculate(series, 2);

        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(up.Last.Value));
        Assert.True(double.IsFinite(lo.Last.Value));
        Assert.True(double.IsFinite(mid.Last.Value));

        ind.Update(new TBar(DateTime.UtcNow, 120, 130, 110, 120, 1000));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Decaychannel_Event_Publishes()
    {
        var src = new TBarSeries();
        var d = new Decaychannel(src, 2);
        bool fired = false;
        d.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        src.Add(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Decaychannel_HalfLife_DecayBehavior()
    {
        // Test that decay follows half-life: 50% convergence over period bars
        int period = 10;
        var d = new Decaychannel(period);

        // Establish extreme values
        d.Update(new TBar(DateTime.UtcNow, 100, 200, 0, 100, 1000)); // Upper=200, Lower=0, Mid=100

        double initialUpper = d.Upper.Value;
        double initialLower = d.Lower.Value;
        double initialMid = (initialUpper + initialLower) * 0.5;

        // Feed midpoint values for 'period' bars - no new extremes
        for (int i = 0; i < period; i++)
        {
            d.Update(new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000));
        }

        // After period bars, should be approximately 50% convergence toward midpoint
        double upperDistance = d.Upper.Value - initialMid;
        double lowerDistance = initialMid - d.Lower.Value;

        // The channel should have contracted significantly (roughly 50%)
        double initialUpperDistance = initialUpper - initialMid;
        double initialLowerDistance = initialMid - initialLower;

        // Allow some tolerance for algorithm differences
        Assert.True(upperDistance < initialUpperDistance * 0.7, "Upper should decay toward midpoint");
        Assert.True(lowerDistance < initialLowerDistance * 0.7, "Lower should decay toward midpoint");
    }

    [Fact]
    public void Decaychannel_Bands_MaintainOrder()
    {
        var d = new Decaychannel(5);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            d.Update(bar, isNew: true);

            Assert.True(d.Upper.Value >= d.Lower.Value, $"Bar {i}: Upper ({d.Upper.Value}) >= Lower ({d.Lower.Value})");
            Assert.True(d.Last.Value >= d.Lower.Value - 1e-10, $"Bar {i}: Middle ({d.Last.Value}) >= Lower ({d.Lower.Value})");
            Assert.True(d.Last.Value <= d.Upper.Value + 1e-10, $"Bar {i}: Middle ({d.Last.Value}) <= Upper ({d.Upper.Value})");
        }
    }
}
