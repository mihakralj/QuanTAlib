using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class AdxrTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            adxr.Update(bars[i]);
        }

        Assert.True(double.IsFinite(adxr.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            adxr.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        adxr.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = adxr.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var adxr2 = new Adxr(14);
        for (int i = 0; i < 99; i++)
        {
            adxr2.Update(bars[i]);
        }
        var val3 = adxr2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            adxr.Update(bars[i]);
        }

        adxr.Reset();
        Assert.Equal(0, adxr.Last.Value);
        Assert.False(adxr.IsHot);
        
        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            adxr.Update(bars[i]);
        }
        
        Assert.True(double.IsFinite(adxr.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var adxr = new Adxr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(adxr.Update(bars[i]).Value);
        }

        var adxr2 = new Adxr(14);
        var seriesResults = adxr2.Update(bars);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }
    
    [Fact]
    public void StaticCalculate_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        var adxr = new Adxr(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(adxr.Update(bars[i]).Value);
        }
        
        var staticResults = Adxr.Batch(bars, 14);
        
        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Adxr(0));
        Assert.Throws<ArgumentException>(() => new Adxr(-1));
    }
}
