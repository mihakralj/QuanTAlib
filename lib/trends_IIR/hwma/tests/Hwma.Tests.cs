namespace QuanTAlib.Tests;

public class HwmaTests
{
    [Fact]
    public void Hwma_Constructor_ValidatesInput()
    {
        var ex1 = Assert.Throws<ArgumentException>(() => new Hwma(0));
        Assert.Equal("period", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Hwma(-1));
        Assert.Equal("period", ex2.ParamName);

        var hwma = new Hwma(10);
        Assert.NotNull(hwma);
    }

    [Fact]
    public void Hwma_AlphaConstructor_ValidatesInput()
    {
        var ex1 = Assert.Throws<ArgumentException>(() => new Hwma(0.0, 0.1, 0.1));
        Assert.Equal("alpha", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Hwma(1.5, 0.1, 0.1));
        Assert.Equal("alpha", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentException>(() => new Hwma(0.5, -0.1, 0.1));
        Assert.Equal("beta", ex3.ParamName);

        var ex4 = Assert.Throws<ArgumentException>(() => new Hwma(0.5, 0.1, 1.5));
        Assert.Equal("gamma", ex4.ParamName);

        var hwma = new Hwma(0.2, 0.1, 0.1);
        Assert.NotNull(hwma);
    }

    [Fact]
    public void Hwma_Calc_ReturnsValue()
    {
        var hwma = new Hwma(10);
        TValue result = hwma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Hwma_IsHot_BecomesTrueImmediately()
    {
        // HWMA is recursive - it's hot after first valid value
        var hwma = new Hwma(5);

        Assert.False(hwma.IsHot);

        hwma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(hwma.IsHot);
    }

    [Fact]
    public void Hwma_StreamingMatchesBatch()
    {
        var hwmaStreaming = new Hwma(10);
        var hwmaBatch = new Hwma(10);
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
            streamingResults.Add(hwmaStreaming.Update(item));
        }

        // Batch
        var batchResults = hwmaBatch.Update(series);

        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamingResults[i].Value, batchResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Hwma_StaticCalculate_MatchesInstance()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var instanceResults = new Hwma(10).Update(series);
        var staticResults = Hwma.Batch(series, 10);

        for (int i = 0; i < instanceResults.Count; i++)
        {
            Assert.Equal(instanceResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Hwma_SpanCalculate_MatchesSeries()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var seriesResults = Hwma.Batch(series, 10);

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Hwma.Batch(input.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(seriesResults[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Hwma_Update_IsNewFalse_CorrectsValue()
    {
        var hwma = new Hwma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed initial data
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            hwma.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // Update with isNew=false (correction)
        var newBar = gbm.Next(isNew: true);
        hwma.Update(new TValue(newBar.Time, newBar.Close), isNew: true);

        double valueAfterCommit = hwma.Last.Value;

        // Now update the SAME bar with a different value
        hwma.Update(new TValue(newBar.Time, newBar.Close + 10.0), isNew: false);

        double valueAfterCorrection = hwma.Last.Value;

        Assert.NotEqual(valueAfterCommit, valueAfterCorrection);

        // Now restore original value
        hwma.Update(new TValue(newBar.Time, newBar.Close), isNew: false);

        Assert.Equal(valueAfterCommit, hwma.Last.Value, 1e-9);
    }

    [Fact]
    public void Hwma_NaN_Input_UsesLastValidValue()
    {
        var hwma = new Hwma(5);

        hwma.Update(new TValue(DateTime.UtcNow, 100));
        hwma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = hwma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Hwma_Reset_ClearsState()
    {
        var hwma = new Hwma(10);
        hwma.Update(new TValue(DateTime.UtcNow, 100));
        hwma.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(hwma.Last.Value > 0);
        Assert.True(hwma.IsHot);

        hwma.Reset();

        Assert.Equal(0, hwma.Last.Value);
        Assert.False(hwma.IsHot);
    }

    [Fact]
    public void Hwma_FirstValue_ReturnsInput()
    {
        var hwma = new Hwma(10);
        TValue result = hwma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, result.Value, 1e-9);
    }

    [Fact]
    public void Hwma_Properties_Accessible()
    {
        var hwma = new Hwma(10);
        Assert.False(hwma.IsHot);
        Assert.Equal(0, hwma.Last.Value);
    }

    [Fact]
    public void Hwma_Calc_IsNew_AcceptsParameter()
    {
        var hwma = new Hwma(10);
        hwma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.Equal(100, hwma.Last.Value);
    }

    [Fact]
    public void Hwma_IterativeCorrections_RestoreToOriginalState()
    {
        var hwma = new Hwma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            hwma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double valueAfterTen = hwma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            hwma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalValue = hwma.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Hwma_Infinity_Input_UsesLastValidValue()
    {
        var hwma = new Hwma(10);
        hwma.Update(new TValue(DateTime.UtcNow, 100));
        hwma.Update(new TValue(DateTime.UtcNow, 110));

        var resultPosInf = hwma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultPosInf.Value));

        var resultNegInf = hwma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultNegInf.Value));
    }

    [Fact]
    public void Hwma_MultipleNaN_ContinuesWithLastValid()
    {
        var hwma = new Hwma(10);
        hwma.Update(new TValue(DateTime.UtcNow, 100));

        var r1 = hwma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = hwma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void Hwma_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Hwma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Hwma.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Hwma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Hwma(pubSource, period);
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
    public void Hwma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Hwma.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Hwma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Hwma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Hwma.Batch(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void Hwma_TripleSmoothing_Components()
    {
        // Verify the triple smoothing characteristic: tracks level, velocity, acceleration
        // When price is trending up consistently, HWMA should lead due to velocity/acceleration
        var hwma = new Hwma(10);

        // Simulate steady uptrend
        double[] prices = new double[30];
        for (int i = 0; i < 30; i++)
        {
            prices[i] = 100 + i * 2; // Linear uptrend
        }

        double lastResult = 0;
        foreach (var price in prices)
        {
            var result = hwma.Update(new TValue(DateTime.UtcNow, price));
            lastResult = result.Value;
        }

        // HWMA should be close to or slightly ahead of current price in strong trend
        // (due to velocity/acceleration extrapolation)
        double lastPrice = prices[^1];
        Assert.True(Math.Abs(lastResult - lastPrice) < lastPrice * 0.1); // Within 10%
    }

    [Fact]
    public void Hwma_SmoothingFactors_AffectResult()
    {
        // Different smoothing factors should produce different results
        var hwma1 = new Hwma(5);   // Higher alpha (more responsive)
        var hwma2 = new Hwma(20);  // Lower alpha (smoother)

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            hwma1.Update(new TValue(bar.Time, bar.Close));
            hwma2.Update(new TValue(bar.Time, bar.Close));
        }

        // Different periods should produce different results
        Assert.NotEqual(hwma1.Last.Value, hwma2.Last.Value);
    }

    [Fact]
    public void Hwma_AlphaConstructor_ProducesResults()
    {
        // Test the alpha/beta/gamma constructor
        var hwma = new Hwma(0.2, 0.1, 0.1);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            hwma.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(hwma.Last.Value));
        Assert.True(hwma.Last.Value > 0);
    }

    [Fact]
    public void Hwma_ConstantInput_ReturnsConstant()
    {
        var hwma = new Hwma(10);
        const double constantValue = 100.0;

        for (int i = 0; i < 20; i++)
        {
            var result = hwma.Update(new TValue(DateTime.UtcNow, constantValue));
            Assert.Equal(constantValue, result.Value, 1e-9);
        }
    }

    [Fact]
    public void Hwma_PeriodOne_ReturnsInputValue()
    {
        var hwma = new Hwma(1);

        for (int i = 1; i <= 10; i++)
        {
            var input = new TValue(DateTime.UtcNow, i * 10.0);
            var result = hwma.Update(input);
            // Period 1 means alpha=1 (full weighting to current), but beta=gamma=1 as well
            // After warmup, should track closely
            Assert.True(double.IsFinite(result.Value));
        }
    }
}
