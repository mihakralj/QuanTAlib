using System;
using Xunit;

namespace QuanTAlib;

public class RsxTests
{
    private readonly GBM _gbm;

    public RsxTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Rsx(0));
        Assert.Throws<ArgumentException>(() => new Rsx(-1));
    }

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var rsx = new Rsx(14);
        var result = rsx.Update(new TValue(DateTime.UtcNow, 100));
        Assert.InRange(result.Value, 0, 100);
    }

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var rsx = new Rsx(14);
        rsx.Update(new TValue(DateTime.UtcNow, 100));
        var result = rsx.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        // Should not be NaN
        Assert.False(double.IsNaN(result.Value));
        Assert.InRange(result.Value, 0, 100);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var rsx = new Rsx(14);
        var time = DateTime.UtcNow;
        
        // Update with isNew=true
        var val1 = rsx.Update(new TValue(time, 100), true);
        
        // Update with isNew=false (same time, different value)
        rsx.Update(new TValue(time, 105), false);
        
        // Update with isNew=false (same time, original value) - should match val1 if state rollback works
        var val3 = rsx.Update(new TValue(time, 100), false);

        Assert.Equal(val1.Value, val3.Value, 1e-9);
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        int period = 14;
        int count = 100;
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var rsx = new Rsx(period);
        
        var streamingResults = new List<double>();
        for (int i = 0; i < count; i++)
        {
            streamingResults.Add(rsx.Update(new TValue(series.Times[i], series.Values[i])).Value);
        }
        
        var staticResults = Rsx.Batch(series, period);
        
        Assert.Equal(streamingResults.Count, staticResults.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], staticResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_Matches_Streaming()
    {
        int period = 14;
        int count = 100;
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var rsx = new Rsx(period);
        
        var streamingResults = new List<double>();
        for (int i = 0; i < count; i++)
        {
            streamingResults.Add(rsx.Update(new TValue(series.Times[i], series.Values[i])).Value);
        }
        
        var spanInput = series.Values.ToArray();
        var spanOutput = new double[count];
        Rsx.Batch(spanInput, spanOutput, period);
        
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Reset_Works()
    {
        var rsx = new Rsx(14);
        rsx.Update(new TValue(DateTime.UtcNow, 100));
        rsx.Reset();
        
        // After reset, it should behave like a new instance
        var val1 = rsx.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(50.0, val1.Value); // Neutral start
    }
    
    [Fact]
    public void Chainability_Works()
    {
        var rsx = new Rsx(14);
        var rsx2 = new Rsx(rsx, 14);
        
        var result = rsx2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(double.IsNaN(result.Value));
    }
}
