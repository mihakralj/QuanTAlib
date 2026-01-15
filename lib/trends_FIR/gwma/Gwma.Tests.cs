namespace QuanTAlib.Tests;

public class GwmaTests
{
    [Fact]
    public void Gwma_Constructor_ValidatesInput()
    {
        var ex1 = Assert.Throws<ArgumentException>(() => new Gwma(0));
        Assert.Equal("period", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Gwma(10, sigma: 0));
        Assert.Equal("sigma", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentException>(() => new Gwma(10, sigma: -0.1));
        Assert.Equal("sigma", ex3.ParamName);

        var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => new Gwma(10, sigma: 1.1));
        Assert.Equal("sigma", ex4.ParamName);

        var gwma = new Gwma(10);
        Assert.NotNull(gwma);
    }

    [Fact]
    public void Gwma_Calc_ReturnsValue()
    {
        var gwma = new Gwma(10);
        TValue result = gwma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Gwma_IsHot_BecomesTrueWhenBufferFull()
    {
        var gwma = new Gwma(5);

        Assert.False(gwma.IsHot);

        for (int i = 0; i < 4; i++)
        {
            gwma.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(gwma.IsHot);
        }

        gwma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(gwma.IsHot);
    }

    [Fact]
    public void Gwma_StreamingMatchesBatch()
    {
        var gwmaStreaming = new Gwma(10);
        var gwmaBatch = new Gwma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streamingResults = new TSeries();
        Assert.True(series.Count > 0);
        foreach (var item in series)
        {
            streamingResults.Add(gwmaStreaming.Update(item));
        }

        // Batch
        var batchResults = gwmaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i].Value, batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Gwma_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Gwma(10).Update(series);
        var staticResults = Gwma.Batch(series, 10);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Gwma_SpanCalculate_MatchesSeries()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var seriesResults = Gwma.Batch(series, 10);

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Gwma.Calculate(input.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(seriesResults[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Gwma_Update_IsNewFalse_CorrectsValue()
    {
        var gwma = new Gwma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            gwma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Update with isNew=false (correction)
        var newBar = gbm.Next(isNew: true);
        gwma.Update(new TValue(newBar.Time, newBar.Close), isNew: true);

        double valueAfterCommit = gwma.Last.Value;

        // Now update the SAME bar with a different value
        gwma.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);

        double valueAfterCorrection = gwma.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Now restore original value
        gwma.Update(new TValue(newBar.Time, newBar.Close), isNew: false);

        Assert.Equal(valueAfterCommit, gwma.Last.Value, 1e-9);
    }

    [Fact]
    public void Gwma_NaN_Input_UsesLastValidValue()
    {
        var gwma = new Gwma(5);

        gwma.Update(new TValue(DateTime.UtcNow, 100));
        gwma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = gwma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Gwma_Reset_ClearsState()
    {
        var gwma = new Gwma(10);
        gwma.Update(new TValue(DateTime.UtcNow, 100));
        gwma.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(gwma.Last.Value > 0);

        gwma.Reset();

        Assert.Equal(0, gwma.Last.Value);
        Assert.False(gwma.IsHot);
    }

    [Fact]
    public void Gwma_FirstValue_ReturnsExpected()
    {
        var gwma = new Gwma(10);
        TValue result = gwma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, result.Value, 1e-9);
    }

    [Fact]
    public void Gwma_Properties_Accessible()
    {
        var gwma = new Gwma(10);
        Assert.False(gwma.IsHot);
        Assert.Equal(0, gwma.Last.Value);
    }

    [Fact]
    public void Gwma_Calc_IsNew_AcceptsParameter()
    {
        var gwma = new Gwma(10);
        gwma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, gwma.Last.Value);
    }

    [Fact]
    public void Gwma_IterativeCorrections_RestoreToOriginalState()
    {
        var gwma = new Gwma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            gwma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = gwma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            gwma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = gwma.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Gwma_Infinity_Input_UsesLastValidValue()
    {
        var gwma = new Gwma(10);
        gwma.Update(new TValue(DateTime.UtcNow, 100));
        gwma.Update(new TValue(DateTime.UtcNow, 110));

        var resultPosInf = gwma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = gwma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void Gwma_MultipleNaN_ContinuesWithLastValid()
    {
        var gwma = new Gwma(10);
        gwma.Update(new TValue(DateTime.UtcNow, 100));

        var r1 = gwma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = gwma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void Gwma_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Gwma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Gwma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Gwma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Gwma(pubSource, period);
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
    public void Gwma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Gwma.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Gwma.Calculate(source.AsSpan(), output.AsSpan(), 3, sigma: 0));
        Assert.Throws<ArgumentException>(() => Gwma.Calculate(source.AsSpan(), output.AsSpan(), 3, sigma: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Gwma.Calculate(source.AsSpan(), output.AsSpan(), 3, sigma: 1.1));
        Assert.Throws<ArgumentException>(() => Gwma.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Gwma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Gwma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Gwma_DifferentSigmaValues_ProduceDifferentResults()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var gwmaNarrow = new Gwma(10, sigma: 0.2);
        var gwmaWide = new Gwma(10, sigma: 0.8);

        TSeries resultNarrow = gwmaNarrow.Update(series);
        TSeries resultWide = gwmaWide.Update(series);

        // Different sigma should produce different results
        Assert.NotEqual(resultNarrow.Last.Value, resultWide.Last.Value);
    }

    [Fact]
    public void Gwma_Warmup_SecondValue_IsAverageForP2()
    {
        // For p=2, the centered Gaussian is symmetric, so both coefficients are equal and the result is the mean.
        var gwma = new Gwma(10, sigma: 0.4);
        var t = DateTime.UtcNow;

        Assert.Equal(1.0, gwma.Update(new TValue(t, 1.0)).Value, 1e-9);
        Assert.Equal(1.5, gwma.Update(new TValue(t, 2.0)).Value, 1e-9);
    }

    [Fact]
    public void Gwma_TSeries_Update_Matches_Streaming_WithNaNAtReplayStart()
    {
        const int period = 5;
        var series = new TSeries();
        var start = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double v = i == 5 ? double.NaN : 100.0 + i;
            series.Add(new TValue(start.AddMinutes(i), v));
        }

        var gwmaStreaming = new Gwma(period);
        var streaming = new List<double>(series.Count);
        foreach (var item in series)
        {
            streaming.Add(gwmaStreaming.Update(item).Value);
        }

        var gwmaBatch = new Gwma(period);
        var batch = gwmaBatch.Update(series);

        Assert.Equal(streaming.Count, batch.Count);
        for (int i = 0; i < batch.Count; i++)
        {
            Assert.Equal(streaming[i], batch.Values[i], 1e-9);
        }
    }
}
