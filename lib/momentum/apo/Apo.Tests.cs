using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class ApoTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            apo.Update(bars[i]);
        }

        Assert.True(double.IsFinite(apo.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            apo.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        apo.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = apo.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var apo2 = new Apo(12, 26);
        for (int i = 0; i < 99; i++)
        {
            apo2.Update(bars[i]);
        }
        var val3 = apo2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            apo.Update(bars[i]);
        }

        apo.Reset();
        Assert.Equal(0, apo.Last.Value);
        Assert.False(apo.IsHot);

        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            apo.Update(bars[i]);
        }

        Assert.True(double.IsFinite(apo.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(apo.Update(bars[i]).Value);
        }

        var apo2 = new Apo(12, 26);
        var seriesResults = apo2.Update(bars.Close);

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

        var apo = new Apo(12, 26);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(apo.Update(bars[i]).Value);
        }

        var staticResults = Apo.Batch(bars.Close, 12, 26);

        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var apo = new Apo(12, 26);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test TBarSeries chain
        var result = apo.Update(bars.Close);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);

        // Test TBar chain (returns TValue)
        var result2 = apo.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Apo(0, 26));
        Assert.Throws<ArgumentException>(() => new Apo(12, 0));
        Assert.Throws<ArgumentException>(() => new Apo(26, 12)); // Fast >= Slow
    }
}
