using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class HmaTests
{
    [Fact]
    public void Hma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Hma(0));
        Assert.Throws<ArgumentException>(() => new Hma(1)); // HMA requires period > 1 for sqrt(period) >= 1

        var hma = new Hma(10);
        Assert.NotNull(hma);
    }

    [Fact]
    public void Hma_Calc_ReturnsValue()
    {
        var hma = new Hma(10);
        TValue result = hma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Hma_IsHot_BecomesTrue()
    {
        var hma = new Hma(9); // sqrt(9) = 3
        // Full WMA needs 9
        // Half WMA needs 4
        // Sqrt WMA needs 3
        // Pipeline: 
        // 1. Full/Half produce valid values immediately (but with warmup ramp)
        // 2. Sqrt consumes them.
        // IsHot is defined as Full.IsHot && Sqrt.IsHot.
        // Full becomes hot after 9 updates.
        // Sqrt becomes hot after 3 updates.
        // So HMA should be hot after 9 + 3 - 1 = 11 updates.

        for (int i = 0; i < 10; i++)
        {
            hma.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(hma.IsHot);
        }

        hma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(hma.IsHot);
    }

    [Fact]
    public void Hma_StreamingMatchesBatch()
    {
        var hmaStreaming = new Hma(14);
        var hmaBatch = new Hma(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }
        Assert.Equal(100, series.Count);

        // Streaming
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(hmaStreaming.Update(item));
        }

        // Batch
        var batchResults = hmaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i].Value, batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Hma_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Hma(14).Update(series);
        var staticResults = Hma.Calculate(series, 14);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Hma_SpanCalculate_MatchesSeries()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var seriesResults = Hma.Calculate(series, 14);

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Hma.Calculate(input.AsSpan(), output.AsSpan(), 14);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(seriesResults[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Hma_Update_IsNewFalse_CorrectsValue()
    {
        var hma = new Hma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            hma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Update with isNew=false (correction)
        var newBar = gbm.Next(isNew: true); // Generate a new value
        hma.Update(new TValue(newBar.Time, newBar.Close), isNew: true); // Commit it

        double valueAfterCommit = hma.Last.Value;

        // Now update the SAME bar with a different value
        hma.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);

        double valueAfterCorrection = hma.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Now restore original value
        hma.Update(new TValue(newBar.Time, newBar.Close), isNew: false);

        Assert.Equal(valueAfterCommit, hma.Last.Value, 1e-9);
    }
}
