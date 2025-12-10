using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class LsmaTests
{
    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Lsma(0));
        Assert.Throws<ArgumentException>(() => new Lsma(-1));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        var lsma = new Lsma(14, 0);
        Assert.Equal("Lsma(14)", lsma.Name);
        Assert.False(lsma.IsHot);
    }

    [Fact]
    public void Update_SingleValue_ReturnsSameValue()
    {
        var lsma = new Lsma(14);
        var result = lsma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Update_LinearTrend_ReturnsExactValue()
    {
        // For a perfect linear trend y = x, LSMA should return x
        int period = 10;
        var lsma = new Lsma(period);
        
        for (int i = 0; i < period * 2; i++)
        {
            var result = lsma.Update(new TValue(DateTime.UtcNow, i));
            if (i >= period) // After warmup
            {
                Assert.Equal(i, result.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Update_ConstantValue_ReturnsSameValue()
    {
        int period = 10;
        var lsma = new Lsma(period);
        double value = 123.45;
        
        for (int i = 0; i < period * 2; i++)
        {
            var result = lsma.Update(new TValue(DateTime.UtcNow, value));
            Assert.Equal(value, result.Value, 1e-9);
        }
    }

    [Fact]
    public void Update_WithOffset_ProjectsCorrectly()
    {
        // y = 2x + 1
        // At x=10, y=21. Slope=2, Intercept=1
        // LSMA(offset=1) should project to x=11 -> y=23
        
        int period = 5;
        int offset = 1;
        var lsma = new Lsma(period, offset);
        
        for (int i = 0; i < 20; i++)
        {
            double y = 2 * i + 1;
            var result = lsma.Update(new TValue(DateTime.UtcNow, y));
            
            if (i >= period)
            {
                double expected = 2 * (i + offset) + 1;
                Assert.Equal(expected, result.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Update_BarCorrection_UpdatesCorrectly()
    {
        var lsma = new Lsma(5);
        
        // Fill buffer
        for (int i = 0; i < 5; i++)
        {
            lsma.Update(new TValue(DateTime.UtcNow, i));
        }
        
        // New bar
        var result1 = lsma.Update(new TValue(DateTime.UtcNow, 10));
        
        // Update same bar with different value
        var result2 = lsma.Update(new TValue(DateTime.UtcNow, 20), isNew: false);
        
        Assert.NotEqual(result1.Value, result2.Value);
        
        // Verify internal state by adding next bar
        // If state was corrupted, this would fail
        var result3 = lsma.Update(new TValue(DateTime.UtcNow, 30));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Update_NaN_HandlesGracefully()
    {
        var lsma = new Lsma(5);
        
        lsma.Update(new TValue(DateTime.UtcNow, 1));
        lsma.Update(new TValue(DateTime.UtcNow, 2));
        var result = lsma.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        // Input sequence becomes: 1, 2, 2 (NaN replaced by last valid 2)
        // Regression on (2,1), (1,2), (0,2)
        // Result should be 2.166666667
        Assert.Equal(2.1666666666666665, result.Value, 1e-9);
    }

    [Fact]
    public void Calculate_StaticMethod_MatchesObjectInstance()
    {
        int period = 10;
        int count = 100;
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);
        
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }
        
        var lsma = new Lsma(period);
        var series1 = lsma.Update(source);
        var series2 = Lsma.Calculate(source, period);
        
        Assert.Equal(series1.Count, series2.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(series1[i].Value, series2[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Calculate_Span_MatchesSeries()
    {
        int period = 10;
        int count = 100;
        var values = new double[count];
        var output = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);
        
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            values[i] = bar.Close;
        }
        
        Lsma.Calculate(values, output, period);
        
        var lsma = new Lsma(period);
        for (int i = 0; i < count; i++)
        {
            var result = lsma.Update(new TValue(DateTime.UtcNow, values[i]));
            Assert.Equal(result.Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var lsma = new Lsma(5);
        for (int i = 0; i < 10; i++)
        {
            lsma.Update(new TValue(DateTime.UtcNow, i));
        }
        
        Assert.True(lsma.IsHot);
        
        lsma.Reset();
        
        Assert.False(lsma.IsHot);
        Assert.Equal(0, lsma.Last.Value);
        
        // Should behave like new instance
        var result = lsma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }
}
