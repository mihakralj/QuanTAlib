using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class AroonOscTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            aroon.Update(bars[i]);
        }

        Assert.True(double.IsFinite(aroon.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            aroon.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        aroon.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        var val2 = aroon.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var aroon2 = new AroonOsc(14);
        for (int i = 0; i < 99; i++)
        {
            aroon2.Update(bars[i]);
        }
        var val3 = aroon2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            aroon.Update(bars[i]);
        }

        aroon.Reset();
        Assert.Equal(0, aroon.Last.Value);
        Assert.False(aroon.IsHot);
        
        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            aroon.Update(bars[i]);
        }
        
        Assert.True(double.IsFinite(aroon.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var aroon = new AroonOsc(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(aroon.Update(bars[i]).Value);
        }

        var aroon2 = new AroonOsc(14);
        var seriesResults = aroon2.Update(bars);

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
        
        var aroon = new AroonOsc(14);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(aroon.Update(bars[i]).Value);
        }
        
        var staticResults = AroonOsc.Batch(bars, 14);
        
        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AroonOsc(0));
        Assert.Throws<ArgumentException>(() => new AroonOsc(-1));
    }

    [Fact]
    public void ManualCalculation_Verify()
    {
        // Simple manual test
        // Period = 2
        // Highs: 10, 12, 11
        // Lows:  8, 9, 7
        
        // T=0: H=10, L=8. Not enough data.
        // T=1: H=12, L=9. Not enough data.
        // T=2: H=11, L=7. 
        // Window Highs: [10, 12, 11]. Max is 12 at index 1 (1 day ago).
        // Window Lows:  [8, 9, 7]. Min is 7 at index 2 (0 days ago).
        
        // Up = ((2 - 1) / 2) * 100 = 50
        // Down = ((2 - 0) / 2) * 100 = 100
        // Osc = 50 - 100 = -50

        var aroon = new AroonOsc(2);
        var time = DateTime.UtcNow;
        
        aroon.Update(new TBar(time, 10, 10, 8, 9, 100));
        aroon.Update(new TBar(time.AddMinutes(1), 11, 12, 9, 10, 100));
        var result = aroon.Update(new TBar(time.AddMinutes(2), 10, 11, 7, 8, 100));

        Assert.Equal(-50.0, result.Value, 1e-9);
    }
}
