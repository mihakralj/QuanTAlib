using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib.Tests;

public class UsfTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var usf = new Usf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            usf.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.True(double.IsFinite(usf.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var usf = new Usf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            usf.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Update with 100th point (isNew=true)
        usf.Update(new TValue(bars[99].Time, bars[99].Close), true);

        // Update with modified 100th point (isNew=false)
        var val2 = usf.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), false);

        // Create new instance and feed up to modified
        var usf_2 = new Usf(10);
        for (int i = 0; i < 99; i++)
        {
            usf_2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        var val3 = usf_2.Update(new TValue(bars[99].Time, bars[99].Close + 1.0), true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var usf = new Usf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            usf.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        usf.Reset();
        Assert.Equal(0, usf.Last.Value);
        Assert.False(usf.IsHot);
        
        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            usf.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        
        Assert.True(double.IsFinite(usf.Last.Value));
    }

    [Fact]
    public void TSeries_Update_Matches_Streaming()
    {
        var usf = new Usf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(usf.Update(series[i]).Value);
        }

        var usf_2 = new Usf(10);
        var seriesResults = usf_2.Update(series);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
        }
    }
    
    [Fact]
    public void BatchCalculate_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        var usf = new Usf(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(usf.Update(series[i]).Value);
        }
        
        var batchResults = Usf.Calculate(series, 10).Results;
        
        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void BatchCalculateSpan_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        var usf = new Usf(10);
        var streamingResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamingResults.Add(usf.Update(series[i]).Value);
        }
        
        var spanResults = new double[series.Count];
        Usf.Calculate(series.Values, spanResults, 10);
        
        for (int i = 0; i < spanResults.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var usf = new Usf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // Test TSeries chain
        var result = usf.Update(series);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);
        
        // Test TValue chain
        var result2 = usf.Update(series[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Usf(0));
        Assert.Throws<ArgumentException>(() => new Usf(-1));
    }
}
