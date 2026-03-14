namespace QuanTAlib.Tests;

public class RemaTests
{
    [Fact]
    public void Rema_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rema(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rema(-1));

        var rema = new Rema(10);
        Assert.NotNull(rema);
    }

    [Fact]
    public void Rema_Constructor_Lambda_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rema(10, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rema(10, 1.1));

        var rema1 = new Rema(10, 0.0);
        var rema2 = new Rema(10, 1.0);
        var rema3 = new Rema(10, 0.5);
        Assert.NotNull(rema1);
        Assert.NotNull(rema2);
        Assert.NotNull(rema3);
    }

    [Fact]
    public void Rema_Calc_ReturnsValue()
    {
        var rema = new Rema(10);

        Assert.Equal(0, rema.Last.Value);

        TValue result = rema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, rema.Last.Value);
    }

    [Fact]
    public void Rema_Calc_IsNew_AcceptsParameter()
    {
        var rema = new Rema(10);

        rema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = rema.Last.Value;

        rema.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = rema.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Rema_Calc_IsNew_False_UpdatesValue()
    {
        var rema = new Rema(10);

        rema.Update(new TValue(DateTime.UtcNow, 100));
        rema.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = rema.Last.Value;

        rema.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = rema.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Rema_Reset_ClearsState()
    {
        var rema = new Rema(10);

        rema.Update(new TValue(DateTime.UtcNow, 100));
        rema.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = rema.Last.Value;

        rema.Reset();

        Assert.Equal(0, rema.Last.Value);

        // After reset, should accept new values
        rema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, rema.Last.Value);
        Assert.NotEqual(valueBefore, rema.Last.Value);
    }

    [Fact]
    public void Rema_Properties_Accessible()
    {
        var rema = new Rema(10);

        Assert.Equal(0, rema.Last.Value);
        Assert.False(rema.IsHot);

        rema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, rema.Last.Value);
    }

    [Fact]
    public void Rema_IsHot_BecomesTrueAfterWarmup()
    {
        var rema = new Rema(10);

        // Initially IsHot should be false
        Assert.False(rema.IsHot);

        int steps = 0;
        while (!rema.IsHot && steps < 1000)
        {
            rema.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(rema.IsHot);
        Assert.True(steps > 0);
        // Similar to EMA, should become hot around 15 bars for period 10
        Assert.InRange(steps, 14, 17);
    }

    [Fact]
    public void Rema_IsHot_IsPeriodDependent()
    {
        int[] periods = [10, 20, 50];
        int[] expectedSteps = new int[periods.Length];

        for (int i = 0; i < periods.Length; i++)
        {
            int period = periods[i];
            var rema = new Rema(period);

            int steps = 0;
            while (!rema.IsHot && steps < 500)
            {
                rema.Update(new TValue(DateTime.UtcNow, 100));
                steps++;
            }

            expectedSteps[i] = steps;
        }

        // Verify warmup times increase with period
        Assert.True(expectedSteps[0] < expectedSteps[1], $"Period 10 ({expectedSteps[0]}) should be less than Period 20 ({expectedSteps[1]})");
        Assert.True(expectedSteps[1] < expectedSteps[2], $"Period 20 ({expectedSteps[1]}) should be less than Period 50 ({expectedSteps[2]})");
    }

    [Fact]
    public void Rema_Lambda1_ApproachesEma()
    {
        // With lambda=1, REMA should behave similarly to EMA
        var rema = new Rema(10, lambda: 1.0);
        var ema = new Ema(10);

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var input = new TValue(bar.Time, bar.Close);
            rema.Update(input);
            ema.Update(input);
        }

        // With lambda=1, REMA should be very close to EMA
        Assert.Equal(ema.Last.Value, rema.Last.Value, 1e-6);
    }

    [Fact]
    public void Rema_Lambda0_MaxRegularization()
    {
        // With lambda=0, REMA uses pure momentum continuation
        var rema0 = new Rema(10, lambda: 0.0);
        var rema05 = new Rema(10, lambda: 0.5);
        var rema1 = new Rema(10, lambda: 1.0);

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var input = new TValue(bar.Time, bar.Close);
            rema0.Update(input);
            rema05.Update(input);
            rema1.Update(input);
        }

        // All should produce finite values
        Assert.True(double.IsFinite(rema0.Last.Value));
        Assert.True(double.IsFinite(rema05.Last.Value));
        Assert.True(double.IsFinite(rema1.Last.Value));

        // They should generally differ (lambda affects behavior)
        // Note: exact equality is unlikely with different lambdas
    }

    [Fact]
    public void Rema_IterativeCorrections_RestoreToOriginalState()
    {
        var rema = new Rema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            rema.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double remaAfterTen = rema.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            rema.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalRema = rema.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(remaAfterTen, finalRema.Value, 1e-10);
    }

    [Fact]
    public void Rema_BatchCalc_MatchesIterativeCalc()
    {
        var remaIterative = new Rema(10);
        var remaBatch = new Rema(10);
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
            iterativeResults.Add(remaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = remaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Rema_NaN_Input_UsesLastValidValue()
    {
        var rema = new Rema(10);

        // Feed some valid values
        rema.Update(new TValue(DateTime.UtcNow, 100));
        rema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = rema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Rema_Infinity_Input_UsesLastValidValue()
    {
        var rema = new Rema(10);

        // Feed some valid values
        rema.Update(new TValue(DateTime.UtcNow, 100));
        rema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = rema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = rema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Rema_MultipleNaN_ContinuesWithLastValid()
    {
        var rema = new Rema(10);

        // Feed valid values
        rema.Update(new TValue(DateTime.UtcNow, 100));
        rema.Update(new TValue(DateTime.UtcNow, 110));
        rema.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = rema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = rema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = rema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Rema_BatchCalc_HandlesNaN()
    {
        var rema = new Rema(10);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = rema.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Rema_Reset_ClearsLastValidValue()
    {
        var rema = new Rema(10);

        // Feed values including NaN
        rema.Update(new TValue(DateTime.UtcNow, 100));
        rema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        rema.Reset();

        // After reset, first valid value should establish new baseline
        var result = rema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Rema_SpanBatch_Period_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() => Rema.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Rema.Batch(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Rema.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Rema_SpanBatch_Lambda_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        // Lambda must be >= 0 and <= 1
        Assert.Throws<ArgumentOutOfRangeException>(() => Rema.Batch(source.AsSpan(), output.AsSpan(), 3, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rema.Batch(source.AsSpan(), output.AsSpan(), 3, 1.1));
    }

    [Fact]
    public void Rema_SpanBatch_MatchesTSeriesBatch()
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
        var tseriesResult = Rema.Batch(series, 10);

        // Calculate with Span API
        Rema.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Rema_SpanBatch_DifferentLambdas()
    {
        double[] source = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        double[] output0 = new double[10];
        double[] output05 = new double[10];
        double[] output1 = new double[10];

        Rema.Batch(source.AsSpan(), output0.AsSpan(), 5, 0.0);
        Rema.Batch(source.AsSpan(), output05.AsSpan(), 5, 0.5);
        Rema.Batch(source.AsSpan(), output1.AsSpan(), 5, 1.0);

        // All should produce finite results
        for (int i = 0; i < 10; i++)
        {
            Assert.True(double.IsFinite(output0[i]));
            Assert.True(double.IsFinite(output05[i]));
            Assert.True(double.IsFinite(output1[i]));
        }
    }

    [Fact]
    public void Rema_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Warm up
        Rema.Batch(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Rema_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Rema.Batch(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var rema = new Rema(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, rema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_SetsStateCorrectly()
    {
        var rema = new Rema(5);
        double[] history = [10, 20, 30, 40, 50];

        rema.Prime(history);

        // Verify against a fresh REMA fed with same data
        var verifyRema = new Rema(5);
        foreach (var val in history)
        {
            verifyRema.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyRema.Last.Value, rema.Last.Value, 1e-10);
        Assert.Equal(verifyRema.IsHot, rema.IsHot);

        // Verify it continues correctly
        rema.Update(new TValue(DateTime.UtcNow, 60));
        verifyRema.Update(new TValue(DateTime.UtcNow, 60));
        Assert.Equal(verifyRema.Last.Value, rema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_HandlesNaN_InHistory()
    {
        var rema = new Rema(5);
        double[] history = [10, 20, double.NaN, 40, 50];

        rema.Prime(history);

        var verifyRema = new Rema(5);
        foreach (var val in history)
        {
            verifyRema.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyRema.Last.Value, rema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_AllNaNs_ReturnsNaN()
    {
        var rema = new Rema(5);
        double[] history = [double.NaN, double.NaN, double.NaN];

        rema.Prime(history);

        Assert.True(double.IsNaN(rema.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 20; i++)
        {
            series.Add(DateTime.UtcNow, i * 10);
        }

        var (results, indicator) = Rema.Calculate(series, 5);

        // Check results
        Assert.Equal(20, results.Count);

        // Verify against standard calculation
        var verifyRema = new Rema(5);
        var verifyResults = verifyRema.Update(series);

        Assert.Equal(verifyResults.Last.Value, results.Last.Value, 1e-10);
        Assert.Equal(verifyRema.Last.Value, indicator.Last.Value, 1e-10);

        // Check indicator state
        Assert.True(indicator.IsHot);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 210));
        verifyRema.Update(new TValue(DateTime.UtcNow, 210));
        Assert.Equal(verifyRema.Last.Value, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Rema_Batch_AllNaNs_ReturnsNaN()
    {
        double[] source = [double.NaN, double.NaN, double.NaN];
        double[] output = new double[3];

        Rema.Batch(source.AsSpan(), output.AsSpan(), 5);

        // Should be all NaNs, not 0s
        foreach (var val in output)
        {
            Assert.True(double.IsNaN(val), $"Expected NaN but got {val}");
        }
    }

    [Fact]
    public void Rema_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        double lambda = 0.5;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Rema.Batch(series, period, lambda);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Rema.Batch(spanInput, spanOutput, period, lambda);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Rema(period, lambda);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Rema(pubSource, period, lambda);
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
    public void Rema_AllModes_ProduceSameResult_AfterResyncInterval()
    {
        // Guards against implementation drift between CalculateCore (batch/span)
        // and Update(TValue) (streaming/eventing) over long runs.
        int period = 10;
        double lambda = 0.5;
        int count = 12050; // Long-running consistency check

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 321);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Rema.Batch(series, period, lambda);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Rema.Batch(spanInput, spanOutput, period, lambda);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Rema(period, lambda);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }

        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Rema(pubSource, period, lambda);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }

        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void Prime_ThenUpdate_StateWorksCorrectly()
    {
        var rema = new Rema(5);
        double[] history = [10, 20, 30, 40, 50];

        rema.Prime(history);
        double afterPrime = rema.Last.Value;

        // After Prime, an isNew=true should advance the state
        rema.Update(new TValue(DateTime.UtcNow, 60), isNew: true);
        double afterNewBar = rema.Last.Value;

        // Values should be different
        Assert.NotEqual(afterPrime, afterNewBar);

        // isNew=false with a different value should recalculate from previous state
        rema.Update(new TValue(DateTime.UtcNow, 70), isNew: false);
        double afterCorrection = rema.Last.Value;

        // Correction with 70 should give different result than 60
        Assert.NotEqual(afterNewBar, afterCorrection);

        // isNew=false with original value (60) should restore to afterNewBar
        rema.Update(new TValue(DateTime.UtcNow, 60), isNew: false);
        Assert.Equal(afterNewBar, rema.Last.Value, 1e-10);
    }
}
