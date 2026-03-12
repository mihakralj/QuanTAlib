namespace QuanTAlib.Tests;

public class HammaTests
{
    [Fact]
    public void Hamma_Constructor_ValidatesInput()
    {
        var ex1 = Assert.Throws<ArgumentException>(() => new Hamma(0));
        Assert.Equal("period", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Hamma(-1));
        Assert.Equal("period", ex2.ParamName);

        var hamma = new Hamma(10);
        Assert.NotNull(hamma);
    }

    [Fact]
    public void Hamma_Calc_ReturnsValue()
    {
        var hamma = new Hamma(10);
        TValue result = hamma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Hamma_IsHot_BecomesTrueWhenBufferFull()
    {
        var hamma = new Hamma(5);

        Assert.False(hamma.IsHot);

        for (int i = 0; i < 4; i++)
        {
            hamma.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(hamma.IsHot);
        }

        hamma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(hamma.IsHot);
    }

    [Fact]
    public void Hamma_StreamingMatchesBatch()
    {
        var hammaStreaming = new Hamma(10);
        var hammaBatch = new Hamma(10);
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
            streamingResults.Add(hammaStreaming.Update(item));
        }

        // Batch
        var batchResults = hammaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i].Value, batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Hamma_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Hamma(10).Update(series);
        var staticResults = Hamma.Batch(series, 10);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Hamma_SpanCalculate_MatchesSeries()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var seriesResults = Hamma.Batch(series, 10);

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Hamma.Batch(input.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(seriesResults[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Hamma_Update_IsNewFalse_CorrectsValue()
    {
        var hamma = new Hamma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            hamma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Update with isNew=false (correction)
        var newBar = gbm.Next(isNew: true);
        hamma.Update(new TValue(newBar.Time, newBar.Close), isNew: true);

        double valueAfterCommit = hamma.Last.Value;

        // Now update the SAME bar with a different value
        hamma.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);

        double valueAfterCorrection = hamma.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Now restore original value
        hamma.Update(new TValue(newBar.Time, newBar.Close), isNew: false);

        Assert.Equal(valueAfterCommit, hamma.Last.Value, 1e-9);
    }

    [Fact]
    public void Hamma_NaN_Input_UsesLastValidValue()
    {
        var hamma = new Hamma(5);

        hamma.Update(new TValue(DateTime.UtcNow, 100));
        hamma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = hamma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Hamma_Reset_ClearsState()
    {
        var hamma = new Hamma(10);
        hamma.Update(new TValue(DateTime.UtcNow, 100));
        hamma.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(hamma.Last.Value > 0);

        hamma.Reset();

        Assert.Equal(0, hamma.Last.Value);
        Assert.False(hamma.IsHot);
    }

    [Fact]
    public void Hamma_FirstValue_ReturnsExpected()
    {
        var hamma = new Hamma(10);
        TValue result = hamma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, result.Value, 1e-9);
    }

    [Fact]
    public void Hamma_Properties_Accessible()
    {
        var hamma = new Hamma(10);
        Assert.False(hamma.IsHot);
        Assert.Equal(0, hamma.Last.Value);
    }

    [Fact]
    public void Hamma_Calc_IsNew_AcceptsParameter()
    {
        var hamma = new Hamma(10);
        hamma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, hamma.Last.Value);
    }

    [Fact]
    public void Hamma_IterativeCorrections_RestoreToOriginalState()
    {
        var hamma = new Hamma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            hamma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = hamma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            hamma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = hamma.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Hamma_Infinity_Input_UsesLastValidValue()
    {
        var hamma = new Hamma(10);
        hamma.Update(new TValue(DateTime.UtcNow, 100));
        hamma.Update(new TValue(DateTime.UtcNow, 110));

        var resultPosInf = hamma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = hamma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void Hamma_MultipleNaN_ContinuesWithLastValid()
    {
        var hamma = new Hamma(10);
        hamma.Update(new TValue(DateTime.UtcNow, 100));

        var r1 = hamma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = hamma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void Hamma_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Hamma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Hamma.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Hamma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Hamma(pubSource, period);
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
    public void Hamma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Hamma.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Hamma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Hamma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Hamma.Batch(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Hamma_HammingWindow_WeightSymmetry()
    {
        // Hamming window should be symmetric around center
        // w[i] = w[period-1-i] for all i
        int period = 11; // Odd for exact center

        // Verify weight symmetry by checking equal outputs for symmetric inputs
        var hamma1 = new Hamma(period);
        var hamma2 = new Hamma(period);

        // Feed ascending values to hamma1
        double[] ascending = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
        foreach (var v in ascending)
        {
            hamma1.Update(new TValue(DateTime.UtcNow, v));
        }

        // Feed descending values to hamma2
        double[] descending = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1];
        foreach (var v in descending)
        {
            hamma2.Update(new TValue(DateTime.UtcNow, v));
        }

        // Results should be the same (symmetric weights applied to symmetric data)
        Assert.Equal(hamma1.Last.Value, hamma2.Last.Value, 1e-9);
    }

    [Fact]
    public void Hamma_KnownValues_ManualCalculation()
    {
        // Manual verification with known Hamming weights
        // period=5: w[i] = 0.54 - 0.46 * cos(2π*i/4)
        // w[0] = 0.54 - 0.46 * cos(0) = 0.54 - 0.46 = 0.08
        // w[1] = 0.54 - 0.46 * cos(π/2) = 0.54 - 0 = 0.54
        // w[2] = 0.54 - 0.46 * cos(π) = 0.54 + 0.46 = 1.0
        // w[3] = 0.54 - 0.46 * cos(3π/2) = 0.54 - 0 = 0.54
        // w[4] = 0.54 - 0.46 * cos(2π) = 0.54 - 0.46 = 0.08

        int period = 5;
        var hamma = new Hamma(period);

        double[] prices = [100, 102, 104, 103, 101];
        foreach (var price in prices)
        {
            hamma.Update(new TValue(DateTime.UtcNow, price));
        }

        // Calculate expected manually
        double twoPiOverPm1 = 2.0 * Math.PI / (period - 1);
        double[] weights = new double[period];
        double weightSum = 0;
        for (int i = 0; i < period; i++)
        {
            weights[i] = 0.54 - 0.46 * Math.Cos(twoPiOverPm1 * i);
            weightSum += weights[i];
        }

        double expected = 0;
        for (int i = 0; i < period; i++)
        {
            expected += prices[i] * weights[i];
        }
        expected /= weightSum;

        Assert.Equal(expected, hamma.Last.Value, 1e-9);
    }

    [Fact]
    public void Hamma_PeriodOne_ReturnsInputValue()
    {
        var hamma = new Hamma(1);

        for (int i = 1; i <= 10; i++)
        {
            var input = new TValue(DateTime.UtcNow, i * 10.0);
            var result = hamma.Update(input);
            Assert.Equal(i * 10.0, result.Value, 1e-9);
        }
    }
}
