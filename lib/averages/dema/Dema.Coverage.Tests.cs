using System;
using Xunit;

namespace QuanTAlib.Tests;

public class DemaCoverageTests
{
    [Fact]
    public void Dema_CompensationLogic_IsTriggered()
    {
        // Compensation happens when E <= 1e-10
        // E starts at 1.0 and decays by (1-alpha) each step.
        // We need enough steps to reach 1e-10.
        // If period=10, alpha ~ 0.18, decay ~ 0.81
        // 0.81^n <= 1e-10 => n * log(0.81) <= -10
        // n * -0.09 <= -10 => n >= 111
        
        int count = 200;
        int period = 10;
        var dema = new Dema(period);
        
        for (int i = 0; i < count; i++)
        {
            dema.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        
        // Just ensuring no exception and value is correct
        Assert.Equal(100.0, dema.Last.Value, 1e-9);
    }

    [Fact]
    public void Dema_IsHot_Logic()
    {
        // IsHot happens when E <= 0.05
        // 0.81^n <= 0.05 => n >= 14
        
        int period = 10;
        var dema = new Dema(period);
        
        Assert.False(dema.IsHot);
        
        for (int i = 0; i < 50; i++)
        {
            dema.Update(new TValue(DateTime.UtcNow, 100.0));
            if (i > 30) // Should be hot by now
            {
                Assert.True(dema.IsHot);
            }
        }
    }

    [Fact]
    public void Dema_StaticCalculate_Alpha_Coverage()
    {
        double[] source = new double[100];
        double[] output = new double[100];
        for(int i=0; i<100; i++) source[i] = 100.0;
        
        // Use alpha overload
        Dema.Calculate(source.AsSpan(), output.AsSpan(), 0.1);
        
        Assert.Equal(100.0, output[^1], 1e-9);
    }

    [Fact]
    public void Dema_Reset_ClearsState()
    {
        var dema = new Dema(10);
        dema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(0, dema.Last.Value);
        
        dema.Reset();
        
        Assert.Equal(0, dema.Last.Value);
        Assert.False(dema.IsHot);
    }
    
    [Fact]
    public void Dema_Constructor_Alpha_Validation()
    {
        Assert.Throws<ArgumentException>(() => new Dema(0.0));
        Assert.Throws<ArgumentException>(() => new Dema(1.1));
        
        var dema = new Dema(0.5);
        Assert.NotNull(dema);
    }
}
