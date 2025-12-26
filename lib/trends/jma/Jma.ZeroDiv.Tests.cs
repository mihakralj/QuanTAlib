using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class JmaZeroDivTests
{
    [Fact]
    public void Period1_DoesNotProduceInfinityOrNaN()
    {
        // Arrange
        var jma = new Jma(period: 1);
        double[] values = { 100, 101, 102, 101, 100 };

        // Act & Assert
        foreach (var v in values)
        {
            var result = jma.Update(new TValue(DateTime.UtcNow, v));
            Assert.False(double.IsNaN(result.Value), $"JMA(1) produced NaN for input {v}");
            Assert.False(double.IsInfinity(result.Value), $"JMA(1) produced Infinity for input {v}");
            // For period 1, JMA should ideally track price very closely
            Assert.Equal(v, result.Value, precision: 1);
        }
    }

    [Fact]
    public void Period1_LogValuesAreFinite()
    {
        // This test inspects private fields via reflection or just checks behavior
        // Since we can't easily access private fields, we'll rely on the calculation logic check
        // If the fix is applied, we shouldn't see -Infinity in internal calculations if we could see them.
        // But we can check if the output is exactly the input, which implies adapt=0 (if logic holds).
        
        var jma = new Jma(period: 1);
        var result = jma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
        
        result = jma.Update(new TValue(DateTime.UtcNow, 200));
        // If adapt is 0 (due to -Infinity log), bands snap to price.
        // If JMA(1) is identity, result should be 200.
        // With clamping, adapt is slightly non-zero (approx 1e-12), so result is very close to 200.
        Assert.Equal(200, result.Value, precision: 8);
    }
}
