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

    [Fact]
    public void Update_CalculatesCorrectly_Population()
    {
        // Test data: 1, 2, 3
        // Mean = 2
        // Variance (Pop) = ((1-2)^2 + (2-2)^2 + (3-2)^2) / 3 = 2/3
        // StdDev (Pop) = sqrt(2/3)
        // M3 (Pop) = ((1-2)^3 + (2-2)^3 + (3-2)^3) / 3 = 0
        // Skew (Pop) = 0
        
        var skew = new Skew(3, isPopulation: true);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        var result = skew.Update(new TValue(DateTime.UtcNow, 3));

        Assert.Equal(0, result.Value, precision: 10);
    }

    [Fact]
    public void Update_HandlesConstantValues_ZeroVariance()
    {
        var skew = new Skew(5);
        for (int i = 0; i < 5; i++)
        {
            var result = skew.Update(new TValue(DateTime.UtcNow, 10));
            Assert.Equal(0, result.Value); // Skew is undefined or 0 for constant values
        }
    }

    [Fact]
    public void Update_HandlesNaN()
    {
        var skew = new Skew(5);
        skew.Update(new TValue(DateTime.UtcNow, 1));
        skew.Update(new TValue(DateTime.UtcNow, 2));
        skew.Update(new TValue(DateTime.UtcNow, double.NaN)); // Should be treated as 0 or handled gracefully
        
        var result = skew.Last.Value;
        Assert.True(double.IsNaN(result) || result == 0);
    }

    [Fact]
    public void Resync_DoesNotDrift()
    {
        // Run for > 1000 updates to trigger Resync
        var skew = new Skew(10);
        var random = new Random(123);
        
        for (int i = 0; i < 1100; i++)
        {
            skew.Update(new TValue(DateTime.UtcNow, random.NextDouble() * 100));
        }
        
        Assert.True(double.IsFinite(skew.Last.Value));
    }

    [Fact]
    public void Batch_LargeDataset_Simd()
    {
        // Create large dataset to trigger SIMD path (>= 256)
        int count = 1000;
        var data = new double[count];
        for (int i = 0; i < count; i++) data[i] = (double)i;

        var series = new TSeries(new System.Collections.Generic.List<long>(new long[count]), new System.Collections.Generic.List<double>(data));
        
        // Batch calculation
        var batchResult = Skew.Calculate(series, 10);
        
        // Verify last value against streaming
        var skew = new Skew(10);
        double lastStreaming = 0;
        foreach (var val in data)
        {
            lastStreaming = skew.Update(new TValue(DateTime.UtcNow, val)).Value;
        }

        Assert.Equal(lastStreaming, batchResult.Last.Value, precision: 10);
    }
}
