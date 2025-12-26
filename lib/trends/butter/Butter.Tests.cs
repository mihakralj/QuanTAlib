using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class ButterTests
{
    private readonly GBM _gbm;

    public ButterTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Butter(1));
    }

    [Fact]
    public void Calculate_ThrowsWhenDestinationTooSmall()
    {
        var source = new double[10];
        var destination = new double[5];
        Assert.Throws<ArgumentOutOfRangeException>(() => Butter.Calculate(source, destination, 5, double.NaN));
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var butter = new Butter(10);
        Assert.False(butter.IsHot);
        butter.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(butter.IsHot);
        butter.Update(new TValue(DateTime.UtcNow, 101));
        Assert.True(butter.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var butter = new Butter(10);
        butter.Update(new TValue(DateTime.UtcNow, 100));
        butter.Update(new TValue(DateTime.UtcNow, 101));
        Assert.True(butter.IsHot);
        
        butter.Reset();
        Assert.False(butter.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var butter = new Butter(10);
        butter.Update(new TValue(DateTime.UtcNow, 100));
        var result = butter.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Initial_NaN_Input_ReturnsNaN()
    {
        var butter = new Butter(10);
        var result = butter.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        var bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = new Butter(period).Update(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Butter.Calculate(spanInput, spanOutput, period, double.NaN);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Butter(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Butter(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }
    
    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        int period = 10;
        var butter = new Butter(period);
        
        // Feed 10 values
        for (int i = 0; i < 10; i++)
        {
            butter.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        
        double expected = butter.Last.Value;
        
        // Feed 5 updates with isNew=false
        for (int i = 0; i < 5; i++)
        {
            butter.Update(new TValue(DateTime.UtcNow, 200 + i), isNew: false);
        }
        
        // Feed original 10th value again with isNew=false
        var result = butter.Update(new TValue(DateTime.UtcNow, 109), isNew: false);
        
        Assert.Equal(expected, result.Value, 1e-9);
    }
}
