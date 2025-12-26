using System;
using Xunit;

namespace QuanTAlib.Tests;

public class VarianceTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Variance(1));
    }

    [Fact]
    public void Calculation_KnownValues()
    {
        // Data: 2, 4, 4, 4, 5, 5, 7, 9
        // Mean: 5
        // Deviations: -3, -1, -1, -1, 0, 0, 2, 4
        // Sq Devs: 9, 1, 1, 1, 0, 0, 4, 16
        // Sum Sq Devs: 32
        // Population Variance (N=8): 32 / 8 = 4
        // Sample Variance (N-1=7): 32 / 7 = 4.571428...

        var data = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        
        // Test Population Variance
        var popVar = new Variance(8, isPopulation: true);
        foreach (var val in data)
        {
            popVar.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(4.0, popVar.Last.Value, precision: 6);

        // Test Sample Variance
        var sampVar = new Variance(8, isPopulation: false);
        foreach (var val in data)
        {
            sampVar.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(32.0 / 7.0, sampVar.Last.Value, precision: 6);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterPeriod()
    {
        int period = 5;
        var variance = new Variance(period);
        
        for (int i = 0; i < period; i++)
        {
            Assert.False(variance.IsHot);
            variance.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(variance.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var variance = new Variance(5);
        for (int i = 0; i < 10; i++)
        {
            variance.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(variance.IsHot);

        variance.Reset();
        Assert.False(variance.IsHot);
        Assert.Equal(0, variance.Last.Value);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCorrectly()
    {
        // Test differential update
        var variance = new Variance(3, isPopulation: true);
        
        // Add 1, 2, 3. Mean=2. Var = ((1-2)^2 + (2-2)^2 + (3-2)^2)/3 = (1+0+1)/3 = 2/3 = 0.666...
        variance.Update(new TValue(DateTime.UtcNow, 1));
        variance.Update(new TValue(DateTime.UtcNow, 2));
        variance.Update(new TValue(DateTime.UtcNow, 3));
        
        Assert.Equal(2.0/3.0, variance.Last.Value, precision: 6);

        // Update last value from 3 to 6.
        // Data: 1, 2, 6. Mean=3. Var = ((1-3)^2 + (2-3)^2 + (6-3)^2)/3 = (4+1+9)/3 = 14/3 = 4.666...
        variance.Update(new TValue(DateTime.UtcNow, 6), isNew: false);
        
        Assert.Equal(14.0/3.0, variance.Last.Value, precision: 6);
    }

    [Fact]
    public void Batch_Matches_Iterative()
    {
        int period = 10;
        int count = 1000;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        
        for (int i = 0; i < count; i++)
        {
            data[i] = gbm.Next().Close;
        }

        // Iterative
        var variance = new Variance(period);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            variance.Update(new TValue(DateTime.UtcNow, data[i]));
            iterativeResults[i] = variance.Last.Value;
        }

        // Batch
        var batchResults = new double[count];
        Variance.Batch(data, batchResults, period);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i], precision: 7);
        }
    }

    [Fact]
    public void Update_HandlesConstantValues_ZeroVariance()
    {
        var variance = new Variance(5);
        for (int i = 0; i < 5; i++)
        {
            var result = variance.Update(new TValue(DateTime.UtcNow, 10));
            if (i >= 1) // Variance defined for N >= 2
            {
                Assert.Equal(0, result.Value);
            }
        }
    }

    [Fact]
    public void Update_HandlesNaN()
    {
        var variance = new Variance(5);
        variance.Update(new TValue(DateTime.UtcNow, 1));
        variance.Update(new TValue(DateTime.UtcNow, 2));
        variance.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        var result = variance.Last.Value;
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void Resync_DoesNotDrift()
    {
        // Run for > 1000 updates to trigger Resync
        var variance = new Variance(10);
        var random = new Random(123);
        
        for (int i = 0; i < 1100; i++)
        {
            variance.Update(new TValue(DateTime.UtcNow, random.NextDouble() * 100));
        }
        
        Assert.True(double.IsFinite(variance.Last.Value));
        Assert.True(variance.Last.Value >= 0);
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
        var batchResult = Variance.Calculate(series, 10);
        
        // Verify last value against streaming
        var variance = new Variance(10);
        double lastStreaming = 0;
        foreach (var val in data)
        {
            lastStreaming = variance.Update(new TValue(DateTime.UtcNow, val)).Value;
        }

        Assert.Equal(lastStreaming, batchResult.Last.Value, precision: 10);
    }
}
