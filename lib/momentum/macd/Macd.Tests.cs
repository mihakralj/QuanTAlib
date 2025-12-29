using Xunit;
using System;

namespace QuanTAlib.Tests;

public class MacdTests
{
    [Fact]
    public void BasicCalculation()
    {
        var macd = new Macd(12, 26, 9);
        Assert.False(macd.IsHot);
    }

    [Fact]
    public void BatchMatchesStreaming()
    {
        var macd = new Macd(12, 26, 9);
        var series = new TSeries();
        // Generate some data
        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + Math.Sin(i * 0.1) * 10));
        }

        var batchResult = macd.Update(series);

        macd.Reset();
        var streamResults = new System.Collections.Generic.List<double>();
        foreach (var item in series)
        {
            macd.Update(item);
            streamResults.Add(macd.Last.Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResults[i], 8);
        }
    }

    [Fact]
    public void SpanMatchesBatch()
    {
        var macd = new Macd(12, 26, 9);
        var series = new TSeries();
        // Generate some data
        for (int i = 0; i < 100; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + Math.Sin(i * 0.1) * 10));
        }

        var batchResult = macd.Update(series);

        var output = new double[series.Count];
        Macd.Calculate(series.Values, output, 12, 26);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 8);
        }
    }
}
