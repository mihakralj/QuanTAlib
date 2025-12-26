using System;
using Xunit;

namespace QuanTAlib.Tests;

public class StdDevTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StdDev(1));
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
        // Population StdDev: Sqrt(4) = 2
        // Sample Variance (N-1=7): 32 / 7 = 4.571428...
        // Sample StdDev: Sqrt(4.571428...) = 2.1380899...

        var data = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        
        // Test Population StdDev
        var popStd = new StdDev(8, isPopulation: true);
        foreach (var val in data)
        {
            popStd.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(2.0, popStd.Last.Value, precision: 6);

        // Test Sample StdDev
        var sampStd = new StdDev(8, isPopulation: false);
        foreach (var val in data)
        {
            sampStd.Update(new TValue(DateTime.UtcNow, val));
        }
        Assert.Equal(Math.Sqrt(32.0 / 7.0), sampStd.Last.Value, precision: 6);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterPeriod()
    {
        int period = 5;
        var stdDev = new StdDev(period);
        
        for (int i = 0; i < period; i++)
        {
            Assert.False(stdDev.IsHot);
            stdDev.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(stdDev.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var stdDev = new StdDev(5);
        for (int i = 0; i < 10; i++)
        {
            stdDev.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(stdDev.IsHot);

        stdDev.Reset();
        Assert.False(stdDev.IsHot);
        Assert.Equal(0, stdDev.Last.Value);
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
        var stdDev = new StdDev(period);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            stdDev.Update(new TValue(DateTime.UtcNow, data[i]));
            iterativeResults[i] = stdDev.Last.Value;
        }

        // Batch
        var batchResults = new double[count];
        StdDev.Batch(data, batchResults, period);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i], precision: 6);
        }
    }
    
    [Fact]
    public void Update_TSeries_Matches_Iterative()
    {
        int period = 10;
        int count = 1000;
        var data = new TSeries();
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            data.Add(new TValue(bar.Time, bar.Close));
        }

        // Iterative
        var stdDev = new StdDev(period);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            stdDev.Update(data[i]);
            iterativeResults[i] = stdDev.Last.Value;
        }

        // TSeries Batch
        var stdDevBatch = new StdDev(period);
        var batchSeries = stdDevBatch.Update(data);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchSeries[i].Value, precision: 6);
        }
    }
}
