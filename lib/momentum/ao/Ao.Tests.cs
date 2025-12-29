using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class AoTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            ao.Update(bars[i]);
        }

        Assert.True(double.IsFinite(ao.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            ao.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        ao.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = ao.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var ao2 = new Ao(5, 34);
        for (int i = 0; i < 99; i++)
        {
            ao2.Update(bars[i]);
        }
        var val3 = ao2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            ao.Update(bars[i]);
        }

        ao.Reset();
        Assert.Equal(0, ao.Last.Value);
        Assert.False(ao.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            ao.Update(bars[i]);
        }

        Assert.True(double.IsFinite(ao.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(ao.Update(bars[i]).Value);
        }

        var ao2 = new Ao(5, 34);
        var seriesResults = ao2.Update(bars);

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

        var ao = new Ao(5, 34);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(ao.Update(bars[i]).Value);
        }

        var staticResults = Ao.Batch(bars, 5, 34);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var ao = new Ao(5, 34);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TBarSeries chain
        var result = ao.Update(bars);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TBar chain (returns TValue)
        var result2 = ao.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Ao(0, 34));
        Assert.Throws<ArgumentException>(() => new Ao(5, 0));
        Assert.Throws<ArgumentException>(() => new Ao(34, 5)); // Fast >= Slow
    }
}
