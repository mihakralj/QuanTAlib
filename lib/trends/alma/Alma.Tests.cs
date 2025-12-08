using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class AlmaTests
{
    [Fact]
    public void Alma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Alma(0));
        Assert.Throws<ArgumentException>(() => new Alma(10, sigma: 0));

        var alma = new Alma(10);
        Assert.NotNull(alma);
    }

    [Fact]
    public void Alma_Calc_ReturnsValue()
    {
        var alma = new Alma(10);
        TValue result = alma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Alma_IsHot_BecomesTrueWhenBufferFull()
    {
        var alma = new Alma(5);

        Assert.False(alma.IsHot);

        for (int i = 0; i < 4; i++)
        {
            alma.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(alma.IsHot);
        }

        alma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(alma.IsHot);
    }

    [Fact]
    public void Alma_StreamingMatchesBatch()
    {
        var almaStreaming = new Alma(10);
        var almaBatch = new Alma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        // Streaming
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(almaStreaming.Update(item));
        }

        // Batch
        var batchResults = almaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i].Value, batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Alma_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Alma(10).Update(series);
        var staticResults = Alma.Calculate(series, 10);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Alma_SpanCalculate_MatchesSeries()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var seriesResults = Alma.Calculate(series, 10);

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Alma.Calculate(input.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(seriesResults[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Alma_Update_IsNewFalse_CorrectsValue()
    {
        var alma = new Alma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            alma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Update with isNew=false (correction)
        var newBar = gbm.Next(isNew: true);
        alma.Update(new TValue(newBar.Time, newBar.Close), isNew: true);

        double valueAfterCommit = alma.Last.Value;

        // Now update the SAME bar with a different value
        alma.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);

        double valueAfterCorrection = alma.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Now restore original value
        alma.Update(new TValue(newBar.Time, newBar.Close), isNew: false);

        Assert.Equal(valueAfterCommit, alma.Last.Value, 1e-9);
    }

    [Fact]
    public void Alma_NaN_Input_UsesLastValidValue()
    {
        var alma = new Alma(5);

        alma.Update(new TValue(DateTime.UtcNow, 100));
        alma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = alma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Alma_Reset_ClearsState()
    {
        var alma = new Alma(10);
        alma.Update(new TValue(DateTime.UtcNow, 100));
        alma.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(alma.Last.Value > 0);

        alma.Reset();

        Assert.Equal(0, alma.Last.Value);
        Assert.False(alma.IsHot);
    }
}
