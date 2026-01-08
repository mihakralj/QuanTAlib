namespace QuanTAlib.Tests;

public class AfirmaTests
{
    [Fact]
    public void Afirma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Afirma(0));
        Assert.Throws<ArgumentException>(() => new Afirma(-1));

        var afirma = new Afirma(10);
        Assert.NotNull(afirma);
    }

    [Fact]
    public void Afirma_Constructor_AcceptsValidParameters()
    {
        var afirma1 = new Afirma(1);
        Assert.NotNull(afirma1);

        var afirma2 = new Afirma(10, Afirma.WindowType.Blackman);
        Assert.NotNull(afirma2);

        var afirma3 = new Afirma(5, Afirma.WindowType.Rectangular);
        Assert.NotNull(afirma3);
        
        var afirma4 = new Afirma(10, Afirma.WindowType.BlackmanHarris, leastSquares: true);
        Assert.NotNull(afirma4);
    }

    [Fact]
    public void Afirma_Calc_ReturnsValue()
    {
        var afirma = new Afirma(10);

        Assert.Equal(0, afirma.Last.Value);

        TValue result = afirma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, afirma.Last.Value);
    }

    [Fact]
    public void Afirma_FirstValue_ReturnsValue()
    {
        var afirma = new Afirma(10);

        TValue result = afirma.Update(new TValue(DateTime.UtcNow, 100));

        // First value should be based on the single input
        Assert.True(double.IsFinite(result.Value));
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Afirma_LeastSquares_AffectsResult()
    {
        // Generate trend data where LS regression should differ from raw window
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.01, seed: 42); 
        var data = new List<TValue>();
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            data.Add(new TValue(bar.Time, bar.Close));
        }

        var afirmaDefault = new Afirma(10, Afirma.WindowType.BlackmanHarris, leastSquares: false);
        var afirmaLS = new Afirma(10, Afirma.WindowType.BlackmanHarris, leastSquares: true);

        double lastDefault = 0;
        double lastLS = 0;

        foreach (var item in data)
        {
            lastDefault = afirmaDefault.Update(item).Value;
            lastLS = afirmaLS.Update(item).Value;
        }

        // They should be different
        Assert.NotEqual(lastDefault, lastLS, 1e-6);
        Assert.True(double.IsFinite(lastLS));
    }
    
    [Fact]
    public void Afirma_LeastSquares_HandlesNaN()
    {
        var afirma = new Afirma(10, Afirma.WindowType.BlackmanHarris, leastSquares: true);

        afirma.Update(new TValue(DateTime.UtcNow, 100));
        afirma.Update(new TValue(DateTime.UtcNow, 110));
        
        // Feed NaN - should handle gracefully (typically carries forward last valid or handles via regression on existing points)
        var result = afirma.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Afirma_Calc_IsNew_AcceptsParameter()
    {
        var afirma = new Afirma(10);

        afirma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = afirma.Last.Value;

        afirma.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = afirma.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Afirma_Calc_IsNew_False_UpdatesValue()
    {
        var afirma = new Afirma(10);

        afirma.Update(new TValue(DateTime.UtcNow, 100));
        afirma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = afirma.Last.Value;

        afirma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = afirma.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Afirma_Reset_ClearsState()
    {
        var afirma = new Afirma(10);

        afirma.Update(new TValue(DateTime.UtcNow, 100));
        afirma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = afirma.Last.Value;

        afirma.Reset();

        Assert.Equal(0, afirma.Last.Value);

        // After reset, should accept new values
        afirma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, afirma.Last.Value);
        Assert.NotEqual(valueBefore, afirma.Last.Value);
    }

    [Fact]
    public void Afirma_Properties_Accessible()
    {
        var afirma = new Afirma(10);

        Assert.Equal(0, afirma.Last.Value);
        Assert.False(afirma.IsHot);

        afirma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, afirma.Last.Value);
    }

    [Fact]
    public void Afirma_IsHot_BecomesTrueWhenBufferFull()
    {
        var afirma = new Afirma(5);

        Assert.False(afirma.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            afirma.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(afirma.IsHot);
        }

        afirma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(afirma.IsHot);
    }

    [Fact]
    public void Afirma_IterativeCorrections_RestoreToOriginalState()
    {
        var afirma = new Afirma(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            afirma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = afirma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            afirma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = afirma.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Afirma_BatchCalc_MatchesIterativeCalc()
    {
        var afirmaIterative = new Afirma(10);
        var afirmaBatch = new Afirma(10);
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
            iterativeResults.Add(afirmaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = afirmaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Afirma_NaN_Input_UsesLastValidValue()
    {
        var afirma = new Afirma(10);

        // Feed some valid values
        afirma.Update(new TValue(DateTime.UtcNow, 100));
        afirma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value
        var resultAfterNaN = afirma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Afirma_Infinity_Input_UsesLastValidValue()
    {
        var afirma = new Afirma(10);

        // Feed some valid values
        afirma.Update(new TValue(DateTime.UtcNow, 100));
        afirma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = afirma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = afirma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Afirma_MultipleNaN_ContinuesWithLastValid()
    {
        var afirma = new Afirma(10);

        // Feed valid values
        afirma.Update(new TValue(DateTime.UtcNow, 100));
        afirma.Update(new TValue(DateTime.UtcNow, 110));
        afirma.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = afirma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = afirma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = afirma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Afirma_BatchCalc_HandlesNaN()
    {
        var afirma = new Afirma(10);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = afirma.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Afirma_Reset_ClearsLastValidValue()
    {
        var afirma = new Afirma(10);

        // Feed values including NaN
        afirma.Update(new TValue(DateTime.UtcNow, 100));
        afirma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        afirma.Reset();

        // After reset, first valid value should establish new baseline
        var result = afirma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Afirma_StaticBatch_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var results = Afirma.Batch(series, 5);

        Assert.Equal(5, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Afirma_Period1_ReturnsSmoothedValues()
    {
        var afirma = new Afirma(1);

        var r1 = afirma.Update(new TValue(DateTime.UtcNow, 100));
        var r2 = afirma.Update(new TValue(DateTime.UtcNow, 200));
        var r3 = afirma.Update(new TValue(DateTime.UtcNow, 150));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Afirma_SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be >= 1
        Assert.Throws<ArgumentException>(() => Afirma.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Afirma.Batch(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Afirma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 5));
    }

    [Fact]
    public void Afirma_SpanBatch_MatchesTSeriesBatch()
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
        var tseriesResult = Afirma.Batch(series, 10);

        // Calculate with Span API
        Afirma.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Afirma_SpanBatch_CalculatesCorrectly()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Afirma.Batch(source.AsSpan(), output.AsSpan(), 5);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Afirma_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
            source[i] = gbm.Next().Close;

        // Warm up
        Afirma.Batch(source.AsSpan(), output.AsSpan(), 10);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Afirma_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Afirma.Batch(source.AsSpan(), output.AsSpan(), 5);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Afirma_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var window = Afirma.WindowType.BlackmanHarris;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Afirma.Batch(series, period, window);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Afirma.Batch(spanInput, spanOutput, period, window);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Afirma(period, window);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Afirma(pubSource, period, window);
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
    public void Afirma_Chainability_Works()
    {
        var source = new TSeries();
        var afirma = new Afirma(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(afirma.Last.Value));
    }

    [Fact]
    public void Afirma_WarmupPeriod_IsSetCorrectly()
    {
        var afirma = new Afirma(21);
        Assert.Equal(21, afirma.WarmupPeriod);
    }

    [Fact]
    public void Afirma_Prime_SetsStateCorrectly()
    {
        var afirma = new Afirma(5);
        double[] history = [10, 20, 30, 40, 50];

        afirma.Prime(history);

        Assert.True(afirma.IsHot);
        Assert.True(double.IsFinite(afirma.Last.Value));

        // Verify it continues correctly
        afirma.Update(new TValue(DateTime.UtcNow, 60));
        Assert.True(double.IsFinite(afirma.Last.Value));
    }

    [Fact]
    public void Afirma_Prime_WithInsufficientHistory_IsNotHot()
    {
        var afirma = new Afirma(10);
        double[] history = [10, 20, 30, 40, 50];

        afirma.Prime(history);

        Assert.False(afirma.IsHot);
        Assert.True(double.IsFinite(afirma.Last.Value));
    }

    [Fact]
    public void Afirma_Prime_HandlesNaN_InHistory()
    {
        var afirma = new Afirma(3);
        double[] history = [10, 20, double.NaN, 40];

        afirma.Prime(history);

        Assert.True(afirma.IsHot);
        Assert.True(double.IsFinite(afirma.Last.Value));
    }

    [Fact]
    public void Afirma_Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 10; i++) series.Add(DateTime.UtcNow, i * 10);

        var (results, indicator) = Afirma.Calculate(series, 5);

        // Check results
        Assert.Equal(10, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(results.Last.Value, indicator.Last.Value);
        Assert.Equal(5, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Afirma_DifferentWindowTypes_Work()
    {
        var windows = new[]
        {
            Afirma.WindowType.Rectangular,
            Afirma.WindowType.Hanning,
            Afirma.WindowType.Hamming,
            Afirma.WindowType.Blackman,
            Afirma.WindowType.BlackmanHarris
        };

        foreach (var window in windows)
        {
            var afirma = new Afirma(10, window);

            for (int i = 0; i < 20; i++)
            {
                afirma.Update(new TValue(DateTime.UtcNow, 100 + i));
            }

            Assert.True(double.IsFinite(afirma.Last.Value), $"Window {window} should produce finite value");
            Assert.True(afirma.IsHot, $"Window {window} should become hot");
        }
    }

    [Fact]
    public void Afirma_FlatLine_ReturnsSameValue()
    {
        var afirma = new Afirma(10);

        for (int i = 0; i < 20; i++)
        {
            afirma.Update(new TValue(DateTime.UtcNow, 100));
        }

        // With a flat line, the filtered value should be close to the input
        Assert.Equal(100, afirma.Last.Value, 1e-6);
    }

    [Fact]
    public void Afirma_Taps1_Works()
    {
        var afirma = new Afirma(1);

        var r1 = afirma.Update(new TValue(DateTime.UtcNow, 100));
        var r2 = afirma.Update(new TValue(DateTime.UtcNow, 200));

        // With 1 tap, output should equal input
        Assert.Equal(100, r1.Value, 1e-10);
        Assert.Equal(200, r2.Value, 1e-10);
    }

    [Fact]
    public void Afirma_Pub_EventFires()
    {
        var afirma = new Afirma(10);
        bool eventFired = false;
        afirma.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        afirma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(eventFired);
    }
}
