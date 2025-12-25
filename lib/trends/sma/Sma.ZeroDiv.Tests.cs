using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class SmaZeroDivTests
{
    [Fact]
    public void Sma_Update_WithIsNewFalse_OnEmptyBuffer_DoesNotThrow()
    {
        var sma = new Sma(10);
        
        // Buffer is empty initially.
        // Calling Update with isNew=false should not cause division by zero.
        // It should return NaN or 0 or Last, but definitely not throw or return Infinity.
        
        var result = sma.Update(new TValue(DateTime.UtcNow, 100), isNew: false);
        
        // Since buffer count is 0, we expect NaN based on our fix.
        Assert.True(double.IsNaN(result.Value), $"Expected NaN but got {result.Value}");
    }

    [Fact]
    public void Sma_Update_WithIsNewFalse_AfterReset_DoesNotThrow()
    {
        var sma = new Sma(10);
        sma.Update(new TValue(DateTime.UtcNow, 100));
        sma.Reset();
        
        // Buffer is empty after Reset.
        var result = sma.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        
        Assert.True(double.IsNaN(result.Value), $"Expected NaN but got {result.Value}");
    }
}
