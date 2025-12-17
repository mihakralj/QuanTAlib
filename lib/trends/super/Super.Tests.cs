using System;
using System.Collections.Generic;
using Xunit;

namespace QuanTAlib;

public class SuperTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var super = new Super(10, 3.0);
        var gbm = new GBM();
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            super.Update(bars[i]);
        }

        Assert.True(double.IsFinite(super.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var super = new Super(10, 3.0);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            super.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        super.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 1.0, bars[99].Low - 1.0, bars[99].Close, bars[99].Volume);
        var val2 = super.Update(modifiedBar, false);

        // Create new instance and feed up to modified
        var super2 = new Super(10, 3.0);
        for (int i = 0; i < 99; i++)
        {
            super2.Update(bars[i]);
        }
        var val3 = super2.Update(modifiedBar, true);

        Assert.Equal(val3.Value, val2.Value, 1e-9);
        Assert.Equal(super2.UpperBand.Value, super.UpperBand.Value, 1e-9);
        Assert.Equal(super2.LowerBand.Value, super.LowerBand.Value, 1e-9);
        Assert.Equal(super2.IsBullish, super.IsBullish);
    }

    [Fact]
    public void Reset_Works()
    {
        var super = new Super(10, 3.0);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Count; i++)
        {
            super.Update(bars[i]);
        }

        super.Reset();
        Assert.Equal(0, super.Last.Value);
        Assert.False(super.IsHot);
        
        // Feed again
        for (int i = 0; i < bars.Count; i++)
        {
            super.Update(bars[i]);
        }
        
        Assert.True(double.IsFinite(super.Last.Value));
    }

    [Fact]
    public void TBarSeries_Update_Matches_Streaming()
    {
        var super = new Super(10, 3.0);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(super.Update(bars[i]).Value);
        }

        var super2 = new Super(10, 3.0);
        var seriesResults = super2.Update(bars);

        Assert.Equal(streamingResults.Count, seriesResults.Count);
        for (int i = 0; i < seriesResults.Count; i++)
        {
            // Handle NaN comparison
            if (double.IsNaN(streamingResults[i]))
            {
                Assert.True(double.IsNaN(seriesResults.Values[i]));
            }
            else
            {
                Assert.Equal(streamingResults[i], seriesResults.Values[i], 1e-9);
            }
        }
    }
    
    [Fact]
    public void Warmup_Handling()
    {
        var super = new Super(10, 3.0);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        // First 10 bars should be NaN
        for (int i = 0; i < 10; i++)
        {
            var result = super.Update(bars[i]);
            Assert.True(double.IsNaN(result.Value), $"Bar {i} should be NaN");
            Assert.False(super.IsHot);
        }
        
        // 11th bar (index 10) should be valid
        var result11 = super.Update(bars[10]);
        Assert.True(double.IsFinite(result11.Value), "Bar 10 should be finite");
        Assert.True(super.IsHot);
    }

    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Super(0, 3.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Super(-1, 3.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Super(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Super(10, -1.0));
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        var super = new Super(10, 3.0);
        var streamingResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults.Add(super.Update(bars[i]).Value);
        }
        
        var staticResults = Super.Batch(bars, 10, 3.0);
        
        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < staticResults.Count; i++)
        {
            if (double.IsNaN(streamingResults[i]))
            {
                Assert.True(double.IsNaN(staticResults.Values[i]));
            }
            else
            {
                Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
            }
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var super = new Super(10, 3.0);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        // Test TBarSeries chain
        var result = super.Update(bars);
        Assert.NotNull(result);
        Assert.IsType<TSeries>(result);
        
        // Test TBar chain (returns TValue)
        var result2 = super.Update(bars[0]);
        Assert.IsType<TValue>(result2);
    }
}
