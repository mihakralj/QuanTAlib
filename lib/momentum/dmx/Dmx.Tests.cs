using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class DmxTests
{
    [Fact]
    public void Constructor_InvalidParameters_ThrowsException()
    {
        // Dmx delegates to Jma which throws ArgumentOutOfRangeException (subclass of ArgumentException)
        var ex1 = Assert.ThrowsAny<ArgumentException>(() => new Dmx(0));
        Assert.Contains("period", ex1.Message, StringComparison.OrdinalIgnoreCase);

        var ex2 = Assert.ThrowsAny<ArgumentException>(() => new Dmx(-1));
        Assert.Contains("period", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var dmx = new Dmx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmx.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dmx.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var dmx = new Dmx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            dmx.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        dmx.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = dmx.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var dmx2 = new Dmx(14);
        for (int i = 0; i < 99; i++)
        {
            dmx2.Update(bars[i]);
        }
        var val3 = dmx2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var dmx = new Dmx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 50; i++)
            dmx.Update(bars[i]);

        var originalValue = dmx.Last;

        for (int m = 0; m < 5; m++)
        {
            var modified = new TBar(bars[49].Time, bars[49].Open, bars[49].High + m, bars[49].Low - m, bars[49].Close, bars[49].Volume);
            dmx.Update(modified, isNew: false);
        }

        var restored = dmx.Update(bars[49], isNew: false);
        Assert.Equal(originalValue.Value, restored.Value, 9);
    }

    [Fact]
    public void Reset_Works()
    {
        var dmx = new Dmx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmx.Update(bars[i]);
        }

        dmx.Reset();
        Assert.Equal(0, dmx.Last.Value);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            dmx.Update(bars[i]);
        }

        Assert.True(double.IsFinite(dmx.Last.Value));
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var dmx = new Dmx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 30; i++)
            dmx.Update(bars[i]);

        var nanBar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 100);
        var result = dmx.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var dmx = new Dmx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 30; i++)
            dmx.Update(bars[i]);

        var infBar = new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 0, 100, 100);
        var result = dmx.Update(infBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var gbm = new GBM(seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var batchResult = Dmx.Batch(bars, 14);
        double expected = batchResult.Last.Value;

        // 2. Streaming Mode
        var streamDmx = new Dmx(14);
        for (int i = 0; i < bars.Count; i++)
            streamDmx.Update(bars[i]);
        double streamResult = streamDmx.Last.Value;

        Assert.Equal(expected, streamResult, 9);
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var dmx = new Dmx(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(dmx.Update(bars[i]).Value);
        }

        var dmx2 = new Dmx(14);
        var seriesResults = dmx2.Update(bars);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void FirstBar_Handling()
    {
        var dmx = new Dmx(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        // First bar should produce 0 DMX because DM+ and DM- are 0
        var result = dmx.Update(bar);

        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dmx = new Dmx(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(dmx.Update(bars[i]).Value);
        }

        var staticResults = Dmx.Batch(bars, 14);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var dmx = new Dmx(14);
        var sma = new Sma(dmx, 10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            dmx.Update(bars[i]);
        }

        Assert.True(sma.Last.Value != 0);
    }
}
