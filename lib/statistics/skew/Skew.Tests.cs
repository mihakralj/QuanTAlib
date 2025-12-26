using System;
using Xunit;

namespace QuanTAlib.Tests;

public class SkewTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Skew(2));
        var skew = new Skew(3);
        Assert.NotNull(skew);
    }

    [Fact]
    public void Update_CalculatesCorrectly_Sample()
    {
        // Test data: 1, 2, 3, 4, 5
        // Mean = 3
        // Variance (Sample) = 2.5
        // StdDev (Sample) = 1.58113883
        // Skewness (Sample) = 0 (Symmetric)
        
        var skew = new Skew(5, isPopulation: false);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        skew.Update(new TValue(DateTime.UtcNow, 3));
        skew.Update(new TValue(DateTime.UtcNow, 4));
        var result = skew.Update(new TValue(DateTime.UtcNow, 5));

        Assert.Equal(0, result.Value, precision: 10);
    }

    [Fact]
    public void Update_CalculatesCorrectly_PositiveSkew()
    {
        // Test data: 1, 1, 1, 10
        // Mean = 3.25
        // Skewness should be positive (right tail)
        
        var skew = new Skew(4, isPopulation: false);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 1));
        var result = skew.Update(new TValue(DateTime.UtcNow, 10));

        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Update_CalculatesCorrectly_NegativeSkew()
    {
        // Test data: 10, 10, 10, 1
        // Mean = 7.75
        // Skewness should be negative (left tail)
        
        var skew = new Skew(4, isPopulation: false);
        skew.Update(new TValue(DateTime.UtcNow, 10));
        skew.Update(new TValue(DateTime.UtcNow, 10));
        skew.Update(new TValue(DateTime.UtcNow, 10));
        var result = skew.Update(new TValue(DateTime.UtcNow, 1));

        Assert.True(result.Value < 0);
    }

    [Fact]
    public void Update_HandlesUpdates_IsNewFalse()
    {
        var skew = new Skew(5);
        
        // 1, 2, 3, 4
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        skew.Update(new TValue(DateTime.UtcNow, 3));
        skew.Update(new TValue(DateTime.UtcNow, 4));
        
        // Add 5
        skew.Update(new TValue(DateTime.UtcNow, 5), isNew: true);
        
        // Update 5 to 10
        var res2 = skew.Update(new TValue(DateTime.UtcNow, 10), isNew: false);

        // Expected: Skew of 1, 2, 3, 4, 10
        var expectedSkew = new Skew(5);
        expectedSkew.Update(new TValue(DateTime.UtcNow, 1));
        expectedSkew.Update(new TValue(DateTime.UtcNow, 2));
        expectedSkew.Update(new TValue(DateTime.UtcNow, 3));
        expectedSkew.Update(new TValue(DateTime.UtcNow, 4));
        var expected = expectedSkew.Update(new TValue(DateTime.UtcNow, 10));

        Assert.Equal(expected.Value, res2.Value, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var skew = new Skew(5);
        for (int i = 0; i < 5; i++) skew.Update(new TValue(DateTime.UtcNow, i));
        
        skew.Reset();
        Assert.False(skew.IsHot);
        
        // Should behave like new
        skew.Update(new TValue(DateTime.UtcNow, 1));
        Assert.Equal(0, skew.Last.Value); // Not enough data
    }

    [Fact]
    public void Batch_Matches_Streaming()
    {
        var data = new double[] { 1, 2, 3, 4, 5, 10, 1, 2, 3 };
        int period = 5;
        
        // Streaming
        var skew = new Skew(period);
        var streamingResults = new System.Collections.Generic.List<double>();
        foreach (var val in data)
        {
            streamingResults.Add(skew.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        // Batch
        var series = new TSeries(new System.Collections.Generic.List<long>(new long[data.Length]), new System.Collections.Generic.List<double>(data));
        var batchResult = Skew.Calculate(series, period);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], precision: 10);
        }
    }
}
