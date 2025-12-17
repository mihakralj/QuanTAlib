using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class KamaTests
{
    [Fact]
    public void Kama_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Kama(0));
        Assert.Throws<ArgumentException>(() => new Kama(10, fastPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Kama(10, slowPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Kama(10, fastPeriod: 10, slowPeriod: 5));

        var kama = new Kama(10);
        Assert.NotNull(kama);
    }

    [Fact]
    public void Kama_Calc_ReturnsValue()
    {
        var kama = new Kama(10);
        TValue result = kama.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Kama_IsHot_BecomesTrueWhenBufferFull()
    {
        // Buffer size is period + 1
        var kama = new Kama(5);

        Assert.False(kama.IsHot);

        for (int i = 0; i < 5; i++)
        {
            kama.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(kama.IsHot);
        }

        kama.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(kama.IsHot);
    }

    [Fact]
    public void Kama_StreamingMatchesBatch()
    {
        var kamaStreaming = new Kama(10);
        var kamaBatch = new Kama(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        // Streaming
        var streamingResults = new TSeries();
        Assert.True(series.Count > 0);
        foreach (var item in series)
        {
            streamingResults.Add(kamaStreaming.Update(item));
        }

        // Batch
        var batchResults = kamaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        foreach (var (stream, batch) in streamingResults.Zip(batchResults))
        {
            Assert.Equal(stream.Value, batch.Value, 1e-9);
        }
    }

    [Fact]
    public void Kama_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Kama(10).Update(series);
        var staticResults = new double[series.Count];
        Kama.Calculate(series.Values.ToArray().AsSpan(), staticResults.AsSpan(), 10);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i], 1e-9);
        }
    }

    [Fact]
    public void Kama_Update_IsNewFalse_CorrectsValue()
    {
        var kama = new Kama(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            kama.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Update with isNew=false (correction)
        var newBar = gbm.Next(isNew: true);
        kama.Update(new TValue(newBar.Time, newBar.Close), isNew: true);

        double valueAfterCommit = kama.Last.Value;

        // Now update the SAME bar with a different value
        kama.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);

        double valueAfterCorrection = kama.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Now restore original value
        kama.Update(new TValue(newBar.Time, newBar.Close), isNew: false);

        Assert.Equal(valueAfterCommit, kama.Last.Value, 1e-9);
    }

    [Fact]
    public void Kama_NaN_Input_UsesLastValidValue()
    {
        var kama = new Kama(5);

        kama.Update(new TValue(DateTime.UtcNow, 100));
        kama.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = kama.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Kama_Reset_ClearsState()
    {
        var kama = new Kama(10);
        kama.Update(new TValue(DateTime.UtcNow, 100));
        kama.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(kama.Last.Value > 0);

        kama.Reset();

        Assert.Equal(0, kama.Last.Value);
        Assert.False(kama.IsHot);
    }
    
    [Fact]
    public void Kama_FlatLine_ReturnsSameValue()
    {
        var kama = new Kama(10);
        for (int i = 0; i < 20; i++)
        {
            kama.Update(new TValue(DateTime.UtcNow, 100));
        }
        
        Assert.Equal(100, kama.Last.Value);
    }

    [Fact]
    public void Kama_Calc_IsNew_AcceptsParameter()
    {
        var kama = new Kama(10);
        kama.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, kama.Last.Value);
    }

    [Fact]
    public void Kama_IterativeCorrections_RestoreToOriginalState()
    {
        var kama = new Kama(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 20 new values (enough to fill buffer and stabilize)
        TValue lastInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            kama.Update(lastInput, isNew: true);
        }

        // Remember state
        double valueAfter = kama.Last.Value;

        // Generate 5 corrections with isNew=false (different values)
        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next(isNew: false);
            kama.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered last input again with isNew=false
        TValue finalValue = kama.Update(lastInput, isNew: false);

        // Should match the original state
        Assert.Equal(valueAfter, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Kama_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Kama.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Kama.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Kama_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Kama.Calculate(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Kama_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = Kama.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Kama.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Kama(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Kama(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
