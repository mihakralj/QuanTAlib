using System;
using Xunit;

namespace QuanTAlib;

public class BilateralTests
{
    private readonly GBM _gbm;

    public BilateralTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Bilateral(0));
        Assert.Throws<ArgumentException>(() => new Bilateral(-1));
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var indicator = new Bilateral(3);
        
        indicator.Update(new TValue(DateTime.UtcNow, 1));
        Assert.False(indicator.IsHot);
        
        indicator.Update(new TValue(DateTime.UtcNow, 2));
        Assert.False(indicator.IsHot);
        
        indicator.Update(new TValue(DateTime.UtcNow, 3));
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_CalculatesCorrectly_SimpleCase()
    {
        // Period 3, sigmaS=100 (flat spatial), sigmaR=100 (flat range) -> roughly SMA
        // Actually, Bilateral with very high sigmas approaches Gaussian blur (if range is high) or just mean?
        // If sigma_r is high, range weights are ~1.
        // If sigma_s is high, spatial weights are ~1.
        // Then it becomes a simple average.
        
        var indicator = new Bilateral(3, sigmaSRatio: 100, sigmaRMult: 100);
        
        indicator.Update(new TValue(DateTime.UtcNow, 1));
        indicator.Update(new TValue(DateTime.UtcNow, 2));
        var result = indicator.Update(new TValue(DateTime.UtcNow, 3));
        
        // Expected: (1+2+3)/3 = 2
        Assert.Equal(2.0, result.Value, 1);
    }

    [Fact]
    public void Update_HandlesNaN()
    {
        var indicator = new Bilateral(3);
        
        indicator.Update(new TValue(DateTime.UtcNow, 1));
        indicator.Update(new TValue(DateTime.UtcNow, double.NaN)); // Should use 1
        var result = indicator.Update(new TValue(DateTime.UtcNow, 3));
        
        // Buffer: [1, 1, 3]
        // StDev of [1, 1, 3]: Mean=1.66, Var=((1-1.66)^2 + (1-1.66)^2 + (3-1.66)^2)/3 = (0.44 + 0.44 + 1.77)/3 = 0.88. StDev ~ 0.94
        // Calculation will proceed with these values.
        // Just checking it doesn't crash and returns finite value.
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_IsNew_False_UpdatesCorrectly()
    {
        var indicator = new Bilateral(3);
        
        indicator.Update(new TValue(DateTime.UtcNow, 1));
        indicator.Update(new TValue(DateTime.UtcNow, 2));
        
        // Update with 3, isNew=true
        indicator.Update(new TValue(DateTime.UtcNow, 3), isNew: true);
        
        // Update with 4, isNew=false (correction)
        var res2 = indicator.Update(new TValue(DateTime.UtcNow, 4), isNew: false);
        
        // Verify state was updated
        // If we had updated with 4 directly: [1, 2, 4]
        var indicator2 = new Bilateral(3);
        indicator2.Update(new TValue(DateTime.UtcNow, 1));
        indicator2.Update(new TValue(DateTime.UtcNow, 2));
        var resExpected = indicator2.Update(new TValue(DateTime.UtcNow, 4));
        
        Assert.Equal(resExpected.Value, res2.Value);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Bilateral(3);
        indicator.Update(new TValue(DateTime.UtcNow, 1));
        indicator.Update(new TValue(DateTime.UtcNow, 2));
        indicator.Update(new TValue(DateTime.UtcNow, 3));
        
        indicator.Reset();
        
        Assert.False(indicator.IsHot);
        Assert.Equal(1, indicator.Update(new TValue(DateTime.UtcNow, 1)).Value); // Center val 1, weights 0? No, center val is returned if weights 0.
    }

    [Fact]
    public void Update_IsNew_False_OnEmptyBuffer_DoesNotCrash()
    {
        // Test edge case: calling Update with isNew:false before any isNew:true
        var indicator = new Bilateral(3);
        
        // This should not crash - buffer is empty, so we treat it as first value
        var result = indicator.Update(new TValue(DateTime.UtcNow, 5.0), isNew: false);
        
        // Should have added the value to the buffer
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(5.0, result.Value); // Single value, so result is that value
    }

    [Fact]
    public void Update_IsNew_False_AfterReset_DoesNotCrash()
    {
        // Test edge case: calling Update with isNew:false after Reset
        var indicator = new Bilateral(3);
        
        indicator.Update(new TValue(DateTime.UtcNow, 1));
        indicator.Update(new TValue(DateTime.UtcNow, 2));
        indicator.Reset();
        
        // Buffer is now empty, isNew:false should not crash
        var result = indicator.Update(new TValue(DateTime.UtcNow, 7.0), isNew: false);
        
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(7.0, result.Value);
    }
    
    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        var bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = new Bilateral(period).Update(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Bilateral.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Bilateral(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Bilateral(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }
}
