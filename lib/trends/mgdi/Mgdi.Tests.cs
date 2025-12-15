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

        var resultSeries = mgdi.Update(series);
        
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
}
