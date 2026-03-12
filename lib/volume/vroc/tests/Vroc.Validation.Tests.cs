// Vroc: Mathematical property validation tests
// Volume Rate of Change measures volume momentum. No standard external library
// equivalents with matching implementation. Validation uses mathematical property testing.

namespace QuanTAlib.Tests;

using Xunit;

public class VrocValidationTests
{
    private const int DefaultPeriod = 12;
    private const int TestDataLength = 500;

    [Fact]
    public void Vroc_Output_IsFiniteForGbmData()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var vroc = new Vroc(DefaultPeriod, usePercent: true);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = vroc.Update(bars[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"Vroc output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Vroc_ConstantVolume_ZeroRateOfChange()
    {
        var vroc = new Vroc(DefaultPeriod, usePercent: true);

        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        // Constant volume → VROC = ((V - V_prev) / V_prev) * 100 = 0
        Assert.Equal(0.0, vroc.Last.Value, precision: 8);
    }

    [Fact]
    public void Vroc_DoublingVolume_Returns100Percent()
    {
        var vroc = new Vroc(period: 1, usePercent: true);

        // First bar: volume = 1000
        var bar1 = new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000);
        vroc.Update(bar1, isNew: true);

        // Second bar: volume = 2000 (doubled)
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 101, 99, 100, 2000);
        var result = vroc.Update(bar2, isNew: true);

        // VROC = ((2000 - 1000) / 1000) * 100 = 100%
        Assert.Equal(100.0, result.Value, precision: 8);
    }

    [Fact]
    public void Vroc_HalvingVolume_ReturnsMinus50Percent()
    {
        var vroc = new Vroc(period: 1, usePercent: true);

        // First bar: volume = 2000
        var bar1 = new TBar(DateTime.UtcNow, 100, 101, 99, 100, 2000);
        vroc.Update(bar1, isNew: true);

        // Second bar: volume = 1000 (halved)
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 101, 99, 100, 1000);
        var result = vroc.Update(bar2, isNew: true);

        // VROC = ((1000 - 2000) / 2000) * 100 = -50%
        Assert.Equal(-50.0, result.Value, precision: 8);
    }

    [Fact]
    public void Vroc_PointMode_ReturnsAbsoluteDifference()
    {
        var vroc = new Vroc(period: 1, usePercent: false);

        var bar1 = new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000);
        vroc.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 101, 99, 100, 3000);
        var result = vroc.Update(bar2, isNew: true);

        // Point mode: VROC = 3000 - 1000 = 2000
        Assert.Equal(2000.0, result.Value, precision: 8);
    }

    [Fact]
    public void Vroc_BeforeWarmup_ReturnsZero()
    {
        var vroc = new Vroc(DefaultPeriod, usePercent: true);

        // Before enough bars to compare
        for (int i = 0; i < DefaultPeriod; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, 1000 + (i * 100));
            var result = vroc.Update(bar, isNew: true);
            Assert.Equal(0.0, result.Value, precision: 10);
        }
    }

    [Fact]
    public void Vroc_IncreasingVolume_PositiveRoc()
    {
        var vroc = new Vroc(DefaultPeriod, usePercent: true);

        // Feed steadily increasing volume
        for (int i = 0; i < 50; i++)
        {
            double volume = 1000 + (i * 200); // increases by 200 each bar
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, volume);
            vroc.Update(bar, isNew: true);
        }

        // After warmup, VROC should be positive
        Assert.True(vroc.IsHot);
        Assert.True(vroc.Last.Value > 0,
            $"VROC should be positive with increasing volume, got {vroc.Last.Value}");
    }

    [Fact]
    public void Vroc_DecreasingVolume_NegativeRoc()
    {
        var vroc = new Vroc(DefaultPeriod, usePercent: true);

        // Feed steadily decreasing volume
        for (int i = 0; i < 50; i++)
        {
            double volume = 20000 - (i * 200); // decreases by 200 each bar
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100, 101, 99, 100, volume);
            vroc.Update(bar, isNew: true);
        }

        Assert.True(vroc.IsHot);
        Assert.True(vroc.Last.Value < 0,
            $"VROC should be negative with decreasing volume, got {vroc.Last.Value}");
    }

    [Fact]
    public void Vroc_BatchAndStreaming_ProduceSameResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch
        var batchResults = Vroc.Batch(bars, DefaultPeriod, usePercent: true);

        // Streaming
        var streamVroc = new Vroc(DefaultPeriod, usePercent: true);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamVroc.Update(bars[i], isNew: true);
            streamResults[i] = result.Value;
        }

        Assert.Equal(batchResults.Count, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 8);
        }
    }

    [Fact]
    public void Vroc_PercentAndPointMode_ProduceDifferentResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vrocPct = new Vroc(DefaultPeriod, usePercent: true);
        var vrocPt = new Vroc(DefaultPeriod, usePercent: false);

        for (int i = 0; i < bars.Count; i++)
        {
            vrocPct.Update(bars[i], isNew: true);
            vrocPt.Update(bars[i], isNew: true);
        }

        // Percent and point modes should produce different final values
        Assert.NotEqual(vrocPct.Last.Value, vrocPt.Last.Value);
    }

    [Fact]
    public void Vroc_BarCorrection_IsNewFalse_RestoresState()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var vroc = new Vroc(DefaultPeriod);

        for (int i = 0; i < 30; i++)
        {
            vroc.Update(bars[i], isNew: true);
        }

        vroc.Update(bars[30], isNew: true);
        double afterNew = vroc.Last.Value;

        vroc.Update(bars[30], isNew: false);
        double afterCorrection = vroc.Last.Value;

        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }

    [Fact]
    public void Vroc_IsHot_AfterWarmupPeriod()
    {
        var vroc = new Vroc(DefaultPeriod);

        for (int i = 0; i <= DefaultPeriod; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 101, 99, 100, 1000);
            vroc.Update(bar, isNew: true);
        }

        // IsHot should be true after period + 1 bars (Index > period)
        Assert.True(vroc.IsHot);
    }
}
