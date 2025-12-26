namespace QuanTAlib.Tests;

#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class EmaTests
{
    [Fact]
    public void Ema_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Ema(0));
        Assert.Throws<ArgumentException>(() => new Ema(-1));

        var ema = new Ema(10);
        Assert.NotNull(ema);
    }

    [Fact]
    public void Ema_Constructor_Alpha_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Ema(0.0));
        Assert.Throws<ArgumentException>(() => new Ema(-0.1));
        Assert.Throws<ArgumentException>(() => new Ema(1.1));

        var ema = new Ema(0.5);
        Assert.NotNull(ema);
    }

    [Fact]
    public void Ema_Calc_ReturnsValue()
    {
        var ema = new Ema(10);

        Assert.Equal(0, ema.Last.Value);

        TValue result = ema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, ema.Last.Value);
    }

    [Fact]
    public void Ema_Calc_IsNew_AcceptsParameter()
    {
        var ema = new Ema(10);

        ema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = ema.Last.Value;

        ema.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = ema.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Ema_Calc_IsNew_False_UpdatesValue()
    {
        var ema = new Ema(10);

        ema.Update(new TValue(DateTime.UtcNow, 100));
        ema.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = ema.Last.Value;

        ema.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = ema.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Ema_Reset_ClearsState()
    {
        var ema = new Ema(10);

        ema.Update(new TValue(DateTime.UtcNow, 100));
        ema.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = ema.Last.Value;

        ema.Reset();

        Assert.Equal(0, ema.Last.Value);

        // After reset, should accept new values
        ema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, ema.Last.Value);
        Assert.NotEqual(valueBefore, ema.Last.Value);
    }

    [Fact]
    public void Ema_Properties_Accessible()
    {
        var ema = new Ema(10);

        Assert.Equal(0, ema.Last.Value);
        Assert.False(ema.IsHot);

        ema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, ema.Last.Value);
    }

    [Fact]
    public void Ema_IsHot_BecomesTrueAt95PercentCoverage()
    {
        var ema = new Ema(10);

        // Initially IsHot should be false
        Assert.False(ema.IsHot);

        // IsHot triggers at 95% coverage (E <= 0.05)
        // E = (1 - alpha)^N where alpha = 2 / (period + 1)
        // For period 10: alpha = 2/11 ≈ 0.1818, (1-alpha) ≈ 0.8182
        // N = ln(0.05) / ln(0.8182) ≈ 14.93, so ~15 bars

        int steps = 0;
        while (!ema.IsHot && steps < 1000)
        {
            ema.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(ema.IsHot);
        Assert.True(steps > 0);
        // For period 10, should become hot around 15 bars
        Assert.InRange(steps, 14, 16);
    }

    [Fact]
    public void Ema_IsHot_IsPeriodDependent()
    {
        // Test that different periods result in different warmup times
        // Formula: N = ln(0.05) / ln((p-1)/(p+1))

        int[] periods = [10, 20, 50, 100];
        int[] expectedSteps = new int[periods.Length];

        for (int i = 0; i < periods.Length; i++)
        {
            int period = periods[i];
            var ema = new Ema(period);

            int steps = 0;
            while (!ema.IsHot && steps < 500)
            {
                ema.Update(new TValue(DateTime.UtcNow, 100));
                steps++;
            }

            expectedSteps[i] = steps;
        }

        // Verify warmup times increase with period
        // Period 10 → ~15 bars, Period 20 → ~30 bars, Period 50 → ~75 bars, Period 100 → ~150 bars
        Assert.True(expectedSteps[0] < expectedSteps[1], $"Period 10 ({expectedSteps[0]}) should be less than Period 20 ({expectedSteps[1]})");
        Assert.True(expectedSteps[1] < expectedSteps[2], $"Period 20 ({expectedSteps[1]}) should be less than Period 50 ({expectedSteps[2]})");
        Assert.True(expectedSteps[2] < expectedSteps[3], $"Period 50 ({expectedSteps[2]}) should be less than Period 100 ({expectedSteps[3]})");

        // Verify approximate expected values (N ≈ 1.5 * period for 95% coverage)
        Assert.InRange(expectedSteps[0], 14, 17);  // Period 10 → ~15
        Assert.InRange(expectedSteps[1], 28, 32);  // Period 20 → ~30
        Assert.InRange(expectedSteps[2], 73, 78);  // Period 50 → ~75
        Assert.InRange(expectedSteps[3], 147, 153); // Period 100 → ~150
    }

    [Fact]
    public void Ema_PeriodEquivalence_BothConstructorsWork()
    {
        int period = 20;
        double alpha = 2.0 / (period + 1);

        var emaPeriod = new Ema(period);
        var emaAlpha = new Ema(alpha);

        // Both should accept Calc calls and produce same result
        TValue result1 = emaPeriod.Update(new TValue(DateTime.UtcNow, 100));
        TValue result2 = emaAlpha.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(result1.Value, result2.Value, 1e-10);
    }

    [Fact]
    public void Ema_IterativeCorrections_RestoreToOriginalState()
    {
        var ema = new Ema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            ema.Update(tenthInput, isNew: true);
        }

        // Remember EMA state after 10 values
        double emaAfterTen = ema.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            ema.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalEma = ema.Update(tenthInput, isNew: false);

        // EMA should match the original state after 10 values
        Assert.Equal(emaAfterTen, finalEma.Value, 1e-10);
    }

    [Fact]
    public void Ema_BatchCalc_MatchesIterativeCalc()
    {
        var emaIterative = new Ema(10);
        var emaBatch = new Ema(10);
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
            iterativeResults.Add(emaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = emaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Ema_Result_ImplicitConversionToDouble()
    {
        var ema = new Ema(10);
        ema.Update(new TValue(DateTime.UtcNow, 100));

        // This should compile and work because TValue has implicit conversion to double
        double result = ema.Last.Value;

        Assert.Equal(100.0, result, 1e-10);
    }

    [Fact]
    public void Ema_NaN_Input_UsesLastValidValue()
    {
        var ema = new Ema(10);

        // Feed some valid values
        ema.Update(new TValue(DateTime.UtcNow, 100));
        ema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = ema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        // EMA should continue to evolve (may differ slightly due to substitution)
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Ema_Infinity_Input_UsesLastValidValue()
    {
        var ema = new Ema(10);

        // Feed some valid values
        ema.Update(new TValue(DateTime.UtcNow, 100));
        ema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = ema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = ema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Ema_MultipleNaN_ContinuesWithLastValid()
    {
        var ema = new Ema(10);

        // Feed valid values
        ema.Update(new TValue(DateTime.UtcNow, 100));
        ema.Update(new TValue(DateTime.UtcNow, 110));
        ema.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = ema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = ema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = ema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));

        // EMA should converge toward last valid value (120) with repeated substitution
        // Values should be getting closer to 120
        Assert.True(r3.Value > r1.Value || Math.Abs(r3.Value - 120) < Math.Abs(r1.Value - 120));
    }

    [Fact]
    public void Ema_BatchCalc_HandlesNaN()
    {
        var ema = new Ema(10);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = ema.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Ema_Reset_ClearsLastValidValue()
    {
        var ema = new Ema(10);

        // Feed values including NaN
        ema.Update(new TValue(DateTime.UtcNow, 100));
        ema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        ema.Reset();

        // After reset, first valid value should establish new baseline
        var result = ema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Ema_SpanBatch_Period_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() => Ema.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Ema.Batch(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Ema.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Ema_SpanBatch_Alpha_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        // Alpha must be > 0 and <= 1
        Assert.Throws<ArgumentOutOfRangeException>(() => Ema.Batch(source.AsSpan(), output.AsSpan(), 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ema.Batch(source.AsSpan(), output.AsSpan(), -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ema.Batch(source.AsSpan(), output.AsSpan(), 1.1));
    }

    [Fact]
    public void Ema_SpanBatch_MatchesTSeriesBatch()
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
        var tseriesResult = Ema.Batch(series, 10);

        // Calculate with Span API
        Ema.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results - allow small tolerance due to bias correction differences
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Ema_SpanBatch_PeriodAndAlphaEquivalent()
    {
        double[] source = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        double[] outputPeriod = new double[10];
        double[] outputAlpha = new double[10];

        int period = 5;
        double alpha = 2.0 / (period + 1);

        Ema.Batch(source.AsSpan(), outputPeriod.AsSpan(), period);
        Ema.Batch(source.AsSpan(), outputAlpha.AsSpan(), alpha);

        // Results should be identical
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(outputPeriod[i], outputAlpha[i], 1e-10);
        }
    }

    [Fact]
    public void Ema_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
            source[i] = gbm.Next().Close;

        // Warm up
        Ema.Batch(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Ema_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Ema.Batch(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Ema_SpanBatch_BiasCorrection_Works()
    {
        double[] source = [100, 100, 100, 100, 100];
        double[] output = new double[5];

        Ema.Batch(source.AsSpan(), output.AsSpan(), 3);

        // With bias correction, first value should equal input
        Assert.Equal(100.0, output[0], 1e-10);

        // All values should converge to 100 since input is constant
        foreach (var val in output)
        {
            Assert.Equal(100.0, val, 1e-9);
        }
    }

    [Fact]
    public void Ema_SpanBatch_Alpha_DirectUsage()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        // Use alpha = 0.5 directly
        Ema.Batch(source.AsSpan(), output.AsSpan(), 0.5);

        // Results should be finite and reasonable
        Assert.True(double.IsFinite(output[^1]));
        Assert.True(output[^1] > 10 && output[^1] <= 50);
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var ema = new Ema(source, 10);
        
        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, ema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_SetsStateCorrectly()
    {
        var ema = new Ema(5);
        double[] history = [10, 20, 30, 40, 50]; 
        
        ema.Prime(history);

        // EMA(5) of 10,20,30,40,50
        // Alpha = 2/6 = 1/3
        // 10 -> 10
        // 20 -> 10 + 1/3(10) = 13.33...
        // ...
        // We can verify against a fresh EMA fed with same data
        var verifyEma = new Ema(5);
        foreach (var val in history) verifyEma.Update(new TValue(DateTime.UtcNow, val));

        Assert.Equal(verifyEma.Last.Value, ema.Last.Value, 1e-10);
        Assert.Equal(verifyEma.IsHot, ema.IsHot);
        
        // Verify it continues correctly
        ema.Update(new TValue(DateTime.UtcNow, 60));
        verifyEma.Update(new TValue(DateTime.UtcNow, 60));
        Assert.Equal(verifyEma.Last.Value, ema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_HandlesNaN_InHistory()
    {
        var ema = new Ema(5);
        double[] history = [10, 20, double.NaN, 40, 50]; 
        
        ema.Prime(history);
        
        var verifyEma = new Ema(5);
        foreach (var val in history) verifyEma.Update(new TValue(DateTime.UtcNow, val));

        Assert.Equal(verifyEma.Last.Value, ema.Last.Value, 1e-10);
    }

    [Fact]
    public void Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 20; i++) series.Add(DateTime.UtcNow, i * 10);
        
        // EMA(5)
        var (results, indicator) = Ema.Calculate(series, 5);

        // Check results
        Assert.Equal(20, results.Count);
        
        // Verify against standard calculation
        var verifyEma = new Ema(5);
        var verifyResults = verifyEma.Update(series);
        
        Assert.Equal(verifyResults.Last.Value, results.Last.Value, 1e-10);
        Assert.Equal(verifyEma.Last.Value, indicator.Last.Value, 1e-10);

        // Check indicator state
        Assert.True(indicator.IsHot);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 210)); 
        verifyEma.Update(new TValue(DateTime.UtcNow, 210));
        Assert.Equal(verifyEma.Last.Value, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Ema_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = Ema.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray(); // Need array for Span modification safety if any
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Ema.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Ema(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Ema(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        // Precision 9 due to potential accumulation differences in loop vs batch optimizations
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
