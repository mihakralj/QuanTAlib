using System;
using Xunit;

namespace QuanTAlib;

public class BilateralTests
{
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
    public void TSeries_Update_Matches_Iterative()
    {
        var indicator = new Bilateral(5);
        var series = new TSeries();
        for (int i = 0; i < 20; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i));
        }
        
        var resultSeries = indicator.Update(series);
        
        var indicatorIterative = new Bilateral(5);
        for (int i = 0; i < 20; i++)
        {
            indicatorIterative.Update(series[i]);
            Assert.Equal(indicatorIterative.Last.Value, resultSeries[i].Value);
        }
    }
}
