using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class MgdiTests
{
    private readonly GBM _gbm;

    public MgdiTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterPeriod()
    {
        var mgdi = new Mgdi(14);
        for (int i = 0; i < 14; i++)
        {
            Assert.False(mgdi.IsHot);
            mgdi.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        }
        Assert.True(mgdi.IsHot);
    }

    [Fact]
    public void Update_Matches_Calculate()
    {
        var mgdi = new Mgdi(14);
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var series = data;

        var resultSeries = Mgdi.Batch(series);
        
        // Reset and calculate streaming
        mgdi.Reset();
        var streamingResults = new List<double>();
        foreach (var item in data)
        {
            streamingResults.Add(mgdi.Update(item).Value);
        }

        for (int i = 0; i < resultSeries.Count; i++)
        {
            Assert.Equal(resultSeries.Values[i], streamingResults[i], 1e-9);
        }
    }

    [Fact]
    public void Calculate_Span_Matches_Update()
    {
        var mgdi = new Mgdi(14);
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var series = data;

        var resultSeries = mgdi.Update(series);
        
        var spanInput = data.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        
        Mgdi.Calculate(spanInput, spanOutput, 14);

        for (int i = 0; i < resultSeries.Count; i++)
        {
            Assert.Equal(resultSeries.Values[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Handles_NaN()
    {
        var mgdi = new Mgdi(14);
        mgdi.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        mgdi.Update(new TValue(DateTime.UtcNow.Ticks, double.NaN));
        
        // Should use last valid value (100.0) for calculation
        // MGDI = 100 + (100 - 100) / ... = 100
        Assert.Equal(100.0, mgdi.Last.Value);
    }

    [Fact]
    public void Constructor_Throws_On_Invalid_Period()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(0));
    }

    [Fact]
    public void Constructor_Throws_On_Invalid_K()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mgdi(14, double.PositiveInfinity));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mgdi = new Mgdi(14);
        for (int i = 0; i < 20; i++)
        {
            mgdi.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.True(mgdi.IsHot);
        
        mgdi.Reset();
        
        Assert.False(mgdi.IsHot);
        Assert.Equal(0, mgdi.Last.Value);
    }

    [Fact]
    public void Update_BarCorrection_UpdatesCorrectly()
    {
        var mgdi = new Mgdi(14);
        
        // Warmup
        for (int i = 0; i < 20; i++)
        {
            mgdi.Update(new TValue(DateTime.UtcNow, 100));
        }
        
        // New bar
        var result1 = mgdi.Update(new TValue(DateTime.UtcNow, 110));
        
        // Update same bar with different value
        var result2 = mgdi.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        
        Assert.NotEqual(result1.Value, result2.Value);
        
        // Verify internal state by adding next bar
        var result3 = mgdi.Update(new TValue(DateTime.UtcNow, 130));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var mgdi = new Mgdi(source, 14);
        
        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, mgdi.Last.Value);
    }
}
