using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class HtitTests
{
    private readonly GBM _gbm;

    public HtitTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmup()
    {
        var htit = new Htit();
        for (int i = 0; i < 12; i++)
        {
            Assert.False(htit.IsHot);
            htit.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        }
        Assert.True(htit.IsHot);
    }

    [Fact]
    public void Update_Matches_Calculate()
    {
        var htit = new Htit();
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var series = data;

        var resultSeries = htit.Update(series);
        
        // Reset and calculate streaming
        htit.Reset();
        var streamingResults = new List<double>();
        foreach (var item in data)
        {
            streamingResults.Add(htit.Update(item).Value);
        }

        for (int i = 0; i < resultSeries.Count; i++)
        {
            Assert.Equal(resultSeries.Values[i], streamingResults[i], 1e-9);
        }
    }

    [Fact]
    public void Calculate_Span_Matches_Update()
    {
        var htit = new Htit();
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var series = data;

        var resultSeries = htit.Update(series);
        
        var spanInput = data.Values.ToArray();
        var spanOutput = new double[spanInput.Length];
        
        Htit.Calculate(spanInput, spanOutput);

        for (int i = 0; i < resultSeries.Count; i++)
        {
            Assert.Equal(resultSeries.Values[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Handles_NaN()
    {
        var htit = new Htit();
        htit.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        htit.Update(new TValue(DateTime.UtcNow.Ticks, double.NaN));
        
        Assert.Equal(100.0, htit.Last.Value);
    }
}
