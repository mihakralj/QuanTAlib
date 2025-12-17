using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class WmaTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var wma = new Wma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            wma.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(wma.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var wma = new Wma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            wma.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Update with 100th point (isNew=true)
        wma.Update(new TValue(bars[99].Time, bars[99].Close), true);

        // Update with modified 100th point (isNew=false)
        var val2 = wma.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), false);

        // Create new instance and feed up to modified
        var wma2 = new Wma(10);
        for (int i = 0; i < 99; i++)
        {
            wma2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        var val3 = wma2.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var wma = new Wma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            wma.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        wma.Reset();
        Assert.Equal(0, wma.Last.Value);
        Assert.False(wma.IsHot);
        
        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            wma.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        
        Assert.True(double.IsFinite(wma.Last.Value));
    }

    [Fact]
    public void TSeries_Update_Matches_Streaming()
    {
        var wma = new Wma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(wma.Update(series[i]).Value);
        }

        var wma2 = new Wma(10);
        var seriesResults = wma2.Update(series);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }
    
    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        var wma = new Wma(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(wma.Update(series[i]).Value);
        }
        
        var staticResults = Wma.Batch(series, 10);
        
        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatchSpan_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        var wma = new Wma(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(wma.Update(series[i]).Value);
        }
        
        var spanResults = new double[series.Count];
        Wma.Batch(series.Values, spanResults, 10);
        
        for (int i = 0; i < spanResults.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var wma = new Wma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // Test TSeries chain
        var result = wma.Update(series);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);
        
        // Test TValue chain
        var result2 = wma.Update(series[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Wma(0));
        Assert.Throws<ArgumentException>(() => new Wma(-1));
    }
}
