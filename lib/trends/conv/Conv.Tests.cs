using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QuanTAlib;

public class ConvTests
{
    [Fact]
    public void Constructor_EmptyKernel_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Conv(Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => new Conv(null!));
    }

    [Fact]
    public void BasicCalculation_MatchesExpected()
    {
        // Kernel: [0.5, 1.0]
        // Data: [1, 2, 3, 4]
        // 1: 1*1.0 = 1.0 (partial)
        // 2: 1*0.5 + 2*1.0 = 2.5
        // 3: 2*0.5 + 3*1.0 = 4.0
        // 4: 3*0.5 + 4*1.0 = 5.5
        
        var kernel = new double[] { 0.5, 1.0 };
        var conv = new Conv(kernel);
        
        var result1 = conv.Update(new TValue(DateTime.UtcNow, 1));
        Assert.Equal(1.0, result1.Value);
        
        var result2 = conv.Update(new TValue(DateTime.UtcNow, 2));
        Assert.Equal(2.5, result2.Value);
        
        var result3 = conv.Update(new TValue(DateTime.UtcNow, 3));
        Assert.Equal(4.0, result3.Value);
        
        var result4 = conv.Update(new TValue(DateTime.UtcNow, 4));
        Assert.Equal(5.5, result4.Value);
    }

    [Fact]
    public void BarCorrection_UpdatesCorrectly()
    {
        var kernel = new double[] { 0.5, 1.0 };
        var conv = new Conv(kernel);
        
        // 1
        conv.Update(new TValue(DateTime.UtcNow, 1));
        
        // 2 (isNew=true) -> 2.5
        var res1 = conv.Update(new TValue(DateTime.UtcNow, 2), isNew: true);
        Assert.Equal(2.5, res1.Value);
        
        // Update 2 to 3 (isNew=false)
        // Buffer was [1, 2]. Now [1, 3].
        // 1*0.5 + 3*1.0 = 3.5
        var res2 = conv.Update(new TValue(DateTime.UtcNow, 3), isNew: false);
        Assert.Equal(3.5, res2.Value);
        
        // New bar 4 (isNew=true)
        // Buffer was [1, 3]. New bar 4. Buffer becomes [3, 4].
        // 3*0.5 + 4*1.0 = 1.5 + 4 = 5.5
        var res3 = conv.Update(new TValue(DateTime.UtcNow, 4), isNew: true);
        Assert.Equal(5.5, res3.Value);
    }

    [Fact]
    public void NanHandling_UsesLastValid()
    {
        var kernel = new double[] { 1.0, 1.0 }; // Sum of last 2
        var conv = new Conv(kernel);
        
        // 1 -> 1
        conv.Update(new TValue(DateTime.UtcNow, 1));
        
        // NaN -> treated as 1. Buffer: [1, 1]. Result: 2.
        var res = conv.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(2.0, res.Value);
        
        // 2 -> Buffer: [1, 2]. Result: 3.
        res = conv.Update(new TValue(DateTime.UtcNow, 2));
        Assert.Equal(3.0, res.Value);
    }

    [Fact]
    public void StaticCalculate_MatchesObjectApi()
    {
        var kernel = new double[] { 0.5, 1.0 };
        var source = new TSeries();
        source.Add(new TValue(DateTime.UtcNow, 1));
        source.Add(new TValue(DateTime.UtcNow, 2));
        source.Add(new TValue(DateTime.UtcNow, 3));
        source.Add(new TValue(DateTime.UtcNow, 4));
        
        var result = Conv.Calculate(source, kernel);
        
        Assert.Equal(1.0, result.Values[0]);
        Assert.Equal(2.5, result.Values[1]);
        Assert.Equal(4.0, result.Values[2]);
        Assert.Equal(5.5, result.Values[3]);
    }
    
    [Fact]
    public void Reset_ClearsState()
    {
        var kernel = new double[] { 1.0, 1.0 };
        var conv = new Conv(kernel);
        
        conv.Update(new TValue(DateTime.UtcNow, 1));
        conv.Update(new TValue(DateTime.UtcNow, 2));
        Assert.True(conv.IsHot);
        
        conv.Reset();
        Assert.False(conv.IsHot);
        Assert.Equal(0, conv.Last.Value);
        
        // Should behave as new
        var res = conv.Update(new TValue(DateTime.UtcNow, 1));
        Assert.Equal(1.0, res.Value);
    }
}
