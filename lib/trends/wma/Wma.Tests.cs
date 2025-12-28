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

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var wma = new Wma(source, 10);
        
        // Verify subscription works
        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, wma.Last.Value);
        
        // Dispose
        wma.Dispose();
        
        // Verify unsubscription
        source.Add(new TValue(DateTime.UtcNow, 200));
        // Last value should remain unchanged if unsubscribed
        Assert.Equal(100, wma.Last.Value);
    }

    [Fact]
    public void DefaultLastValidValue_IsNaN()
    {
        var wma = new Wma(10);
        Assert.True(double.IsNaN(wma.DefaultLastValidValue));
    }

    [Fact]
    public void InitialNaNs_ResultInNaN()
    {
        var wma = new Wma(5);
        wma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(wma.Last.Value));

        wma.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(wma.Last.Value));
    }

    [Fact]
    public void RecoveryFromNaN_Works()
    {
        var wma = new Wma(3);

        // Feed NaNs
        wma.Update(new TValue(DateTime.UtcNow, double.NaN)); // [NaN]
        Assert.True(double.IsNaN(wma.Last.Value));

        wma.Update(new TValue(DateTime.UtcNow, double.NaN)); // [NaN, NaN]
        Assert.True(double.IsNaN(wma.Last.Value));

        // Feed valid values
        wma.Update(new TValue(DateTime.UtcNow, 1.0)); // [NaN, NaN, 1] -> Sum is NaN
        Assert.True(double.IsNaN(wma.Last.Value));

        wma.Update(new TValue(DateTime.UtcNow, 2.0)); // [NaN, 1, 2] -> Sum is NaN
        Assert.True(double.IsNaN(wma.Last.Value));

        wma.Update(new TValue(DateTime.UtcNow, 3.0)); // [1, 2, 3] -> Sum should recover!
        // WMA(3) of [1, 2, 3] = (1*1 + 2*2 + 3*3) / 6 = (1+4+9)/6 = 14/6 = 2.333...
        Assert.Equal(2.333333333, wma.Last.Value, 1e-6);
    }

    [Fact]
    public void ConfigurableDefault_Works()
    {
        var wma = new Wma(3) { DefaultLastValidValue = 0 };

        // Feed NaN
        wma.Update(new TValue(DateTime.UtcNow, double.NaN)); // Treated as 0 -> [0]
        // WMA(3) of [0] -> (1*0)/1 = 0
        Assert.Equal(0, wma.Last.Value);

        wma.Update(new TValue(DateTime.UtcNow, 3.0)); // [0, 3]
        // WMA(3) of [0, 3] -> (1*0 + 2*3) / 3 = 6/3 = 2
        Assert.Equal(2, wma.Last.Value);
    }

    [Fact]
    public void Update_IsNewFalse_OnEmptyBuffer_ThrowsInvalidOperationException()
    {
        var wma = new Wma(10);
        
        // Calling Update with isNew=false on an empty buffer should throw
        var exception = Assert.Throws<InvalidOperationException>(() =>
            wma.Update(new TValue(DateTime.UtcNow, 100.0), isNew: false));
        
        Assert.Contains("isNew=false", exception.Message, StringComparison.Ordinal);
        Assert.Contains("buffer is empty", exception.Message, StringComparison.Ordinal);
        Assert.Contains("isNew=true", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_IsNewFalse_AfterReset_ThrowsInvalidOperationException()
    {
        var wma = new Wma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        // Feed some data
        for (int i = 0; i < 10; i++)
        {
            wma.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        
        // Reset clears the buffer
        wma.Reset();
        
        // Calling Update with isNew=false after reset should throw
        var exception = Assert.Throws<InvalidOperationException>(() =>
            wma.Update(new TValue(bars[10].Time, bars[10].Close), isNew: false));
        
        Assert.Contains("isNew=false", exception.Message, StringComparison.Ordinal);
        Assert.Contains("buffer is empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_IsNewFalse_WithData_WorksCorrectly()
    {
        var wma = new Wma(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        // Feed some data first
        for (int i = 0; i < 5; i++)
        {
            wma.Update(new TValue(bars[i].Time, bars[i].Close), isNew: true);
        }
        
        // Now isNew=false should work (buffer has data)
        var result = wma.Update(new TValue(bars[4].Time, bars[4].Close + 10), isNew: false);
        
        // Should not throw and should return a finite value
        Assert.True(double.IsFinite(result.Value));
    }
}
