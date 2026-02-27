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
        var ema = new Ema(period: 10);
        
        // Feed valid value
        ema.Update(new TValue(DateTime.Today.AddDays(1), 100));
        
        // Feed NaN, which should corrupt state
        ema.Update(new TValue(DateTime.Today.AddDays(2), double.NaN));

        // Let's actually ensure it is corrupted depending on implementation
        // Some robust implementations might discard NaN internally, so we don't assert it strictly
        // We just ensure it recovers properly.

        // Reset should clear the corrupted state
        ema.Reset();
        
        // Feed valid value again
        ema.Update(new TValue(DateTime.Today.AddDays(3), 100));
        
        // Wait for Warmup
        for (int i = 4; i < 3 + ema.WarmupPeriod; i++) {
             ema.Update(new TValue(DateTime.Today.AddDays(i), 100));
        }
        
        // Verify recovery after warmup
        Assert.False(double.IsNaN(ema.Last.Value));
        Assert.Equal(100, Math.Round(ema.Last.Value, 5));
    }
}
