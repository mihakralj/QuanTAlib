using Xunit;
using System;

namespace QuanTAlib.Tests;

public class RsiTests
{
    [Fact]
    public void BasicCalculation()
    {
        var rsi = new Rsi(14);
        // RSI requires a period of data to be valid
        Assert.False(rsi.IsHot);
    }

    [Fact]
    public void BatchMatchesStreaming()
    {
        var rsi = new Rsi(5);
        var series = new TSeries();
        // Generate some data
        for (int i = 0; i < 20; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + Math.Sin(i) * 10));
        }

        var batchResult = rsi.Update(series);
        
        rsi.Reset();
        var streamResults = new System.Collections.Generic.List<double>();
        foreach (var item in series)
        {
            streamResults.Add(rsi.Update(item).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResults[i], 8);
        }
    }
    
    [Fact]
    public void SpanMatchesBatch()
    {
        var rsi = new Rsi(5);
        var series = new TSeries();
        // Generate some data
        for (int i = 0; i < 20; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + Math.Sin(i) * 10));
        }
        
        var batchResult = rsi.Update(series);
        
        var output = new double[series.Count];
        Rsi.Calculate(series.Values, output, 5);
        
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 8);
        }
    }

    [Fact]
    public void HandlesFlatLine()
    {
        var rsi = new Rsi(5);
        var series = new TSeries();
        for (int i = 0; i < 20; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100));
        }

        var result = rsi.Update(series);
        // Flat line means no gains or losses, RSI should be 50 (or 0/100 depending on implementation details, but typically 50 or 0 if no moves)
        // Actually, if AvgGain=0 and AvgLoss=0, RSI is typically defined as 50 or 0.
        // Our implementation: RS = 0/0 -> NaN?
        // Let's check implementation.
        // If AvgLoss is 0, RSI is 100.
        // If AvgGain is 0, RSI is 0.
        // If both are 0?
        // In Rma: if all inputs are 0, Rma is 0.
        // So AvgGain=0, AvgLoss=0.
        // RS = 0/0 = NaN.
        // RSI = 100 - 100/(1+NaN) = NaN.
        // Let's see what happens.
        
        // Actually, standard behavior for flat line is often 50 or 0.
        // Let's verify what our implementation does.
        // If we look at Rsi.cs:
        // if (avgLoss == 0) return avgGain == 0 ? 50 : 100;
        
        Assert.Equal(50, result.Last.Value);
    }
}
