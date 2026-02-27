using System;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests.Core;

/// <summary>
/// Tests verifying the structural stability of indicators across the repository.
/// </summary>
public class IndicatorPropertiesTests
{
    [Fact]
    public void Sma_ShouldNotProduceNaN_WithValidInputs()
    {
        var sma = new Sma(period: 10);
        var random = new Random(42);
        
        for (int i = 0; i < 100; i++)
        {
            double price = (random.Next(1, 1000000) / 10.0);
            sma.Update(new TValue(DateTime.Today.AddDays(i), price));
            
            // Only check for NaN after warmup
            if (i >= sma.WarmupPeriod && double.IsNaN(sma.Last.Value))
            {
                Assert.Fail($"Produced NaN at index {i}");
            }
        }
    }

    [Fact]
    public void Ema_ShouldNotProduceNaN_WithValidInputs()
    {
        var ema = new Ema(period: 10);
        var random = new Random(42);
        
        for (int i = 0; i < 100; i++)
        {
            double price = (random.Next(1, 1000000) / 10.0);
            ema.Update(new TValue(DateTime.Today.AddDays(i), price));
            
            // Only check for NaN after warmup
            if (i >= ema.WarmupPeriod && double.IsNaN(ema.Last.Value))
            {
                Assert.Fail($"Produced NaN at index {i}");
            }
        }
    }

    [Fact]
    public void Indicator_ShouldRecoverFromNaN_WhenReset()
    {
        var sma = new Sma(period: 10);

        // Feed valid value
        sma.Update(new TValue(DateTime.Today.AddDays(1), 100));

        // Feed NaN, which should corrupt state
        sma.Update(new TValue(DateTime.Today.AddDays(2), double.NaN));

        // Reset should clear the corrupted state
        sma.Reset();

        // Feed valid value again
        sma.Update(new TValue(DateTime.Today.AddDays(3), 100));

        // Wait for Warmup
        for (int i = 4; i < 3 + sma.WarmupPeriod; i++) {
             sma.Update(new TValue(DateTime.Today.AddDays(i), 100));
        }

        // Verify recovery after warmup
        Assert.False(double.IsNaN(sma.Last.Value));
        Assert.Equal(100, Math.Round(sma.Last.Value, 5));
    }
}
