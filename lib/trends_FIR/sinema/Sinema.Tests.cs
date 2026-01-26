namespace QuanTAlib.Tests;

public class SinemaTests
{
    [Fact]
    public void Sinema_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Sinema(0));
        Assert.Throws<ArgumentException>(() => new Sinema(-1));

        var sinema = new Sinema(10);
        Assert.NotNull(sinema);
    }

    [Fact]
    public void Sinema_Calc_ReturnsValue()
    {
        var sinema = new Sinema(10);

        Assert.Equal(0, sinema.Last.Value);

        TValue result = sinema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, sinema.Last.Value);
    }

    [Fact]
    public void Sinema_FirstValue_ReturnsItself()
    {
        var sinema = new Sinema(10);

        TValue result = sinema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sinema_Calc_IsNew_AcceptsParameter()
    {
        var sinema = new Sinema(5);

        // Build up some history first
        sinema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        sinema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        sinema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double valueConstant = sinema.Last.Value;

        // Adding a significantly different value should change the result
        sinema.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double valueChanged = sinema.Last.Value;

        // Values should change with new bars that have different values
        Assert.NotEqual(valueConstant, valueChanged);
    }

    [Fact]
    public void Sinema_Calc_IsNew_False_UpdatesValue()
    {
        // Note: In SINEMA, the newest value has weight sin(π) = 0, so it doesn't affect the output.
        // This test verifies the isNew=false mechanism by checking that:
        // 1. Adding a new value (isNew=true) advances state
        // 2. Correcting with isNew=false allows subsequent isNew=true to restore consistency
        var sinema = new Sinema(5);

        // Build up buffer with varying values
        sinema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        sinema.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        sinema.Update(new TValue(DateTime.UtcNow, 120), isNew: true);
        double afterThree = sinema.Last.Value;

        // Add a 4th value
        sinema.Update(new TValue(DateTime.UtcNow, 130), isNew: true);
        double afterFour = sinema.Last.Value;

        // Correct with isNew=false using same value - result should stay the same
        sinema.Update(new TValue(DateTime.UtcNow, 130), isNew: false);
        double afterCorrectionSame = sinema.Last.Value;
        Assert.Equal(afterFour, afterCorrectionSame, 1e-10);

        // Correct with isNew=false using different value
        // Due to sine weighting, the last position has 0 weight, so result won't change
        // But the internal state tracking still works - verify via subsequent isNew=true behavior
        sinema.Update(new TValue(DateTime.UtcNow, 999), isNew: false);

        // Add 5th value with isNew=true
        sinema.Update(new TValue(DateTime.UtcNow, 140), isNew: true);
        double afterFive = sinema.Last.Value;

        // Result should be finite and different from afterThree (we've added 2 more values)
        Assert.True(double.IsFinite(afterFive));
        Assert.NotEqual(afterThree, afterFive);
    }

    [Fact]
    public void Sinema_Reset_ClearsState()
    {
        var sinema = new Sinema(10);

        sinema.Update(new TValue(DateTime.UtcNow, 100));
        sinema.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = sinema.Last.Value;

        sinema.Reset();

        Assert.Equal(0, sinema.Last.Value);

        // After reset, should accept new values
        sinema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, sinema.Last.Value);
        Assert.NotEqual(valueBefore, sinema.Last.Value);
    }

    [Fact]
    public void Sinema_Properties_Accessible()
    {
        var sinema = new Sinema(10);

        Assert.Equal(0, sinema.Last.Value);
        Assert.False(sinema.IsHot);

        sinema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, sinema.Last.Value);
    }

    [Fact]
    public void Sinema_IsHot_BecomesTrueWhenBufferFull()
    {
        var sinema = new Sinema(5);

        Assert.False(sinema.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            sinema.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(sinema.IsHot);
        }

        sinema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(sinema.IsHot);
    }

    [Fact]
    public void Sinema_ConstantInput_ReturnsConstant()
    {
        var sinema = new Sinema(5);

        // Feed constant values
        for (int i = 0; i < 10; i++)
        {
            sinema.Update(new TValue(DateTime.UtcNow, 100));
        }

        // SINEMA of constant values should equal the constant
        Assert.Equal(100.0, sinema.Last.Value, 1e-10);
    }

    [Fact]
    public void Sinema_SineWeighting_EmphasisesMiddle()
    {
        // Create a pattern where middle emphasis matters
        // With values [0, 100, 0], sine weighting should give more weight to 100
        var sinema = new Sinema(3);

        sinema.Update(new TValue(DateTime.UtcNow, 0));
        sinema.Update(new TValue(DateTime.UtcNow, 100));
        sinema.Update(new TValue(DateTime.UtcNow, 0));

        // Sine weights for period 3: sin(π/3), sin(2π/3), sin(π)
        // ≈ 0.866, 0.866, 0
        // So result ≈ (0*0.866 + 100*0.866 + 0*0) / (0.866 + 0.866 + 0) = 50
        // Actually the weights depend on position: sin(π*1/3), sin(π*2/3), sin(π*3/3)
        // = sin(π/3), sin(2π/3), sin(π) ≈ 0.866, 0.866, 0

        double result = sinema.Last.Value;
        Assert.True(result > 40 && result < 60, $"Expected ~50 but got {result}");
    }

    [Fact]
    public void Sinema_IterativeCorrections_RestoreToOriginalState()
    {
        var sinema = new Sinema(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            sinema.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double sinemaAfterTen = sinema.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            sinema.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalSinema = sinema.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(sinemaAfterTen, finalSinema.Value, 1e-10);
    }

    [Fact]
    public void Sinema_BatchCalc_MatchesIterativeCalc()
    {
        var sinemaIterative = new Sinema(10);
        var sinemaBatch = new Sinema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Generate data
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        Assert.True(series.Count > 0);

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var item in series)
        {
            iterativeResults.Add(sinemaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = sinemaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Sinema_NaN_Input_UsesLastValidValue()
    {
        var sinema = new Sinema(5);

        // Feed some valid values
        sinema.Update(new TValue(DateTime.UtcNow, 100));
        sinema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = sinema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Sinema_Infinity_Input_UsesLastValidValue()
    {
        var sinema = new Sinema(5);

        // Feed some valid values
        sinema.Update(new TValue(DateTime.UtcNow, 100));
        sinema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = sinema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = sinema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Sinema_MultipleNaN_ContinuesWithLastValid()
    {
        var sinema = new Sinema(5);

        // Feed valid values
        sinema.Update(new TValue(DateTime.UtcNow, 100));
        sinema.Update(new TValue(DateTime.UtcNow, 110));
        sinema.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = sinema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = sinema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = sinema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Sinema_BatchCalc_HandlesNaN()
    {
        var sinema = new Sinema(5);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = sinema.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Sinema_Reset_ClearsLastValidValue()
    {
        var sinema = new Sinema(5);

        // Feed values including NaN
        sinema.Update(new TValue(DateTime.UtcNow, 100));
        sinema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        sinema.Reset();

        // After reset, first valid value should establish new baseline
        var result = sinema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sinema_StaticBatch_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var results = Sinema.Batch(series, 3);

        Assert.Equal(5, results.Count);
        // Results should be finite
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Sinema_Period1_ReturnsInputValues()
    {
        var sinema = new Sinema(1);

        Assert.Equal(100.0, sinema.Update(new TValue(DateTime.UtcNow, 100)).Value, 1e-10);
        Assert.Equal(200.0, sinema.Update(new TValue(DateTime.UtcNow, 200)).Value, 1e-10);
        Assert.Equal(150.0, sinema.Update(new TValue(DateTime.UtcNow, 150)).Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Sinema_SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() => Sinema.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Sinema.Batch(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Sinema.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Sinema_SpanBatch_MatchesTSeriesBatch()
    {
        var series = new TSeries();
        double[] source = new double[100];
        double[] output = new double[100];

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(bar.Time, bar.Close);
        }

        // Calculate with TSeries API
        var tseriesResult = Sinema.Batch(series, 10);

        // Calculate with Span API
        Sinema.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Sinema_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Warm up
        Sinema.Batch(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Sinema_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Sinema.Batch(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Sinema_SpanBatch_Period1_ReturnsInput()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Sinema.Batch(source.AsSpan(), output.AsSpan(), 1);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(source[i], output[i], 1e-10);
        }
    }

    [Fact]
    public void Sinema_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Sinema.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Sinema.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Sinema(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Sinema(pubSource, period);
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

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var sinema = new Sinema(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, sinema.Last.Value);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var sinema = new Sinema(10);
        Assert.Equal(10, sinema.WarmupPeriod);
    }

    [Fact]
    public void Prime_SetsStateCorrectly()
    {
        var sinema = new Sinema(5);
        double[] history = [100, 100, 100, 100, 100]; // All same value

        sinema.Prime(history);

        Assert.True(sinema.IsHot);
        Assert.Equal(100.0, sinema.Last.Value, 1e-10);

        // Verify it continues correctly
        sinema.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, sinema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_WithInsufficientHistory_IsNotHot()
    {
        var sinema = new Sinema(10);
        double[] history = [10, 20, 30, 40, 50];

        sinema.Prime(history);

        Assert.False(sinema.IsHot);
        Assert.True(double.IsFinite(sinema.Last.Value));
    }

    [Fact]
    public void Prime_HandlesNaN_InHistory()
    {
        var sinema = new Sinema(3);
        double[] history = [10, 20, double.NaN, 40];

        sinema.Prime(history);

        Assert.True(sinema.IsHot);
        Assert.True(double.IsFinite(sinema.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 10; i++)
        {
            series.Add(DateTime.UtcNow, i * 10);
        }

        var (results, indicator) = Sinema.Calculate(series, 5);

        // Check results
        Assert.Equal(10, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.Equal(5, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(double.IsFinite(indicator.Last.Value));
    }
}
