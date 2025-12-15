using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class DmxTests
{
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
}
