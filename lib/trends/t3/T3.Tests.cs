using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class T3Tests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var t3 = new T3(5, 0.7);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            t3.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(t3.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var t3 = new T3(5, 0.7);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            t3.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Update with 100th point (isNew=true)
        t3.Update(new TValue(bars[99].Time, bars[99].Close), true);

        // Update with modified 100th point (isNew=false)
        var val2 = t3.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), false);

        // Create new instance and feed up to modified
        var t3_2 = new T3(5, 0.7);
        for (int i = 0; i < 99; i++)
        {
            t3_2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        var val3 = t3_2.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var t3 = new T3(5, 0.7);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            t3.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        t3.Reset();
        Assert.Equal(0, t3.Last.Value);
        Assert.False(t3.IsHot);
        
        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            t3.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        
        Assert.True(double.IsFinite(t3.Last.Value));
    }

    [Fact]
    public void TSeries_Update_Matches_Streaming()
    {
        var t3 = new T3(5, 0.7);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(t3.Update(series[i]).Value);
        }

        var t3_2 = new T3(5, 0.7);
        var seriesResults = t3_2.Update(series);

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
        var series = bars.Close;
        
        var t3 = new T3(5, 0.7);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(t3.Update(series[i]).Value);
        }
        
        var staticResults = T3.Calculate(series, 5, 0.7);
        
        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void StaticCalculateSpan_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        var t3 = new T3(5, 0.7);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(t3.Update(series[i]).Value);
        }
        
        var spanResults = new double[series.Count];
        T3.Calculate(series.Values, spanResults, 5, 0.7);
        
        for (int i = 0; i < spanResults.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var t3 = new T3(5, 0.7);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // Test TSeries chain
        var result = t3.Update(series);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);
        
        // Test TValue chain
        var result2 = t3.Update(series[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new T3(0));
        Assert.Throws<ArgumentException>(() => new T3(-1));
    }
}
