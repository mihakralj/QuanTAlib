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
        Assert.True(series.Count > 0);
        foreach (var item in series)
        {
            streamingResults.Add(hmaStreaming.Update(item));
        }

        // Batch
        var batchResults = hmaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < series.Count; i++)
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
        var staticResults = Hma.Batch(series, 14);

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

        var seriesResults = Hma.Batch(series, 14);

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

    [Fact]
    public void Hma_Reset_ClearsState()
    {
        var hma = new Hma(10);
        hma.Update(new TValue(DateTime.UtcNow, 100));
        hma.Update(new TValue(DateTime.UtcNow, 110));
        
        hma.Reset();
        
        Assert.Equal(0, hma.Last.Value);
        Assert.False(hma.IsHot);
    }

    [Fact]
    public void Hma_IterativeCorrections_RestoreToOriginalState()
    {
        var hma = new Hma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            hma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = hma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            hma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = hma.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Hma_NaN_Input_UsesLastValidValue()
    {
        var hma = new Hma(5);
        hma.Update(new TValue(DateTime.UtcNow, 100));
        hma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = hma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Hma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Hma.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Hma.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Hma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Hma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Hma_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = Hma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Hma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Hma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Hma(pubSource, period);
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
