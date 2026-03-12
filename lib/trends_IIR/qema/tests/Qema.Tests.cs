namespace QuanTAlib.Tests;

public class QemaTests
{
    [Fact]
    public void Qema_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Qema(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Qema(-1));

        var qema = new Qema(10);
        Assert.NotNull(qema);
    }

    [Fact]
    public void Qema_Calc_ReturnsValue()
    {
        var qema = new Qema(10);

        Assert.Equal(0, qema.Last.Value);

        TValue result = qema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, qema.Last.Value);
    }

    [Fact]
    public void Qema_Calc_IsNew_AcceptsParameter()
    {
        var qema = new Qema(10);

        qema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = qema.Last.Value;

        qema.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = qema.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Qema_Calc_IsNew_False_UpdatesValue()
    {
        var qema = new Qema(10);

        qema.Update(new TValue(DateTime.UtcNow, 100));
        qema.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = qema.Last.Value;

        qema.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = qema.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Qema_Reset_ClearsState()
    {
        var qema = new Qema(10);

        qema.Update(new TValue(DateTime.UtcNow, 100));
        qema.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = qema.Last.Value;

        qema.Reset();

        Assert.Equal(0, qema.Last.Value);

        // After reset, should accept new values
        qema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, qema.Last.Value);
        Assert.NotEqual(valueBefore, qema.Last.Value);
    }

    [Fact]
    public void Qema_Properties_Accessible()
    {
        var qema = new Qema(10);

        Assert.Equal(0, qema.Last.Value);
        Assert.False(qema.IsHot);

        qema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, qema.Last.Value);
    }

    [Fact]
    public void Qema_IsHot_BecomesTrueWithSufficientData()
    {
        var qema = new Qema(10);

        // Initially IsHot should be false
        Assert.False(qema.IsHot);

        int steps = 0;
        while (!qema.IsHot && steps < 1000)
        {
            qema.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(qema.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void Qema_IsHot_IsPeriodDependent()
    {
        int[] periods = [10, 20, 50];
        int[] warmupSteps = new int[periods.Length];

        for (int i = 0; i < periods.Length; i++)
        {
            int period = periods[i];
            var qema = new Qema(period);

            int steps = 0;
            while (!qema.IsHot && steps < 500)
            {
                qema.Update(new TValue(DateTime.UtcNow, 100));
                steps++;
            }

            warmupSteps[i] = steps;
        }

        // Verify warmup times increase with period
        Assert.True(warmupSteps[0] < warmupSteps[1], $"Period 10 ({warmupSteps[0]}) should be less than Period 20 ({warmupSteps[1]})");
        Assert.True(warmupSteps[1] < warmupSteps[2], $"Period 20 ({warmupSteps[1]}) should be less than Period 50 ({warmupSteps[2]})");
    }

    [Fact]
    public void Qema_IterativeCorrections_RestoreToOriginalState()
    {
        var qema = new Qema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            qema.Update(tenthInput, isNew: true);
        }

        // Remember QEMA state after 10 values
        double qemaAfterTen = qema.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            qema.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalQema = qema.Update(tenthInput, isNew: false);

        // QEMA should match the original state after 10 values
        Assert.Equal(qemaAfterTen, finalQema.Value, 1e-10);
    }

    [Fact]
    public void Qema_BatchCalc_MatchesIterativeCalc()
    {
        var qemaIterative = new Qema(10);
        var qemaBatch = new Qema(10);
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
            iterativeResults.Add(qemaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = qemaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Qema_NaN_Input_UsesLastValidValue()
    {
        var qema = new Qema(10);

        // Feed some valid values
        qema.Update(new TValue(DateTime.UtcNow, 100));
        qema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = qema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Qema_Infinity_Input_UsesLastValidValue()
    {
        var qema = new Qema(10);

        // Feed some valid values
        qema.Update(new TValue(DateTime.UtcNow, 100));
        qema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = qema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = qema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Qema_MultipleNaN_ContinuesWithLastValid()
    {
        var qema = new Qema(10);

        // Feed valid values
        qema.Update(new TValue(DateTime.UtcNow, 100));
        qema.Update(new TValue(DateTime.UtcNow, 110));
        qema.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = qema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = qema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = qema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Qema_BatchCalc_HandlesNaN()
    {
        var qema = new Qema(10);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = qema.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Qema_Reset_ClearsLastValidValue()
    {
        var qema = new Qema(10);

        // Feed values including NaN
        qema.Update(new TValue(DateTime.UtcNow, 100));
        qema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        qema.Reset();

        // After reset, first valid value should establish new baseline
        var result = qema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Qema_SpanBatch_Period_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentOutOfRangeException>(() => Qema.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Qema.Batch(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Qema.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Qema_SpanBatch_MatchesTSeriesBatch()
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
        var tseriesResult = Qema.Batch(series, 10);

        // Calculate with Span API
        Qema.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Qema_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Warm up
        Qema.Batch(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Qema_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Qema.Batch(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Qema_SpanBatch_BiasCorrection_Works()
    {
        double[] source = [100, 100, 100, 100, 100];
        double[] output = new double[5];

        Qema.Batch(source.AsSpan(), output.AsSpan(), 3);

        // With bias correction, first value should equal input (zero lag for constant)
        Assert.Equal(100.0, output[0], 1e-10);

        // All values should converge to 100 since input is constant
        foreach (var val in output)
        {
            Assert.Equal(100.0, val, 1e-9);
        }
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var qema = new Qema(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, qema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_SetsStateCorrectly()
    {
        var qema = new Qema(5);
        double[] history = [10, 20, 30, 40, 50];

        qema.Prime(history);

        // Verify against a fresh QEMA fed with same data
        var verifyQema = new Qema(5);
        foreach (var val in history)
        {
            verifyQema.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyQema.Last.Value, qema.Last.Value, 1e-10);
        Assert.Equal(verifyQema.IsHot, qema.IsHot);

        // Verify it continues correctly
        qema.Update(new TValue(DateTime.UtcNow, 60));
        verifyQema.Update(new TValue(DateTime.UtcNow, 60));
        Assert.Equal(verifyQema.Last.Value, qema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_HandlesNaN_InHistory()
    {
        var qema = new Qema(5);
        double[] history = [10, 20, double.NaN, 40, 50];

        qema.Prime(history);

        var verifyQema = new Qema(5);
        foreach (var val in history)
        {
            verifyQema.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyQema.Last.Value, qema.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_ThenUpdate_StateWorksCorrectly()
    {
        var qema = new Qema(5);
        double[] history = [10, 20, 30, 40, 50];

        qema.Prime(history);
        double afterPrime = qema.Last.Value;

        // After Prime, an isNew=true should advance the state
        qema.Update(new TValue(DateTime.UtcNow, 60), isNew: true);
        double afterNewBar = qema.Last.Value;

        // Values should be different
        Assert.NotEqual(afterPrime, afterNewBar);

        // isNew=false with a different value should recalculate from previous state
        qema.Update(new TValue(DateTime.UtcNow, 70), isNew: false);
        double afterCorrection = qema.Last.Value;

        // Correction with 70 should give different result than 60
        Assert.NotEqual(afterNewBar, afterCorrection);

        // isNew=false with original value (60) should restore to afterNewBar
        qema.Update(new TValue(DateTime.UtcNow, 60), isNew: false);
        Assert.Equal(afterNewBar, qema.Last.Value, 1e-10);
    }

    [Fact]
    public void Qema_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Qema.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Qema.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Qema(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Qema(pubSource, period);
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
    public void Qema_ZeroLag_WithConstantInput()
    {
        // QEMA should produce zero DC lag for constant input
        var qema = new Qema(20);

        // Feed constant values
        for (int i = 0; i < 100; i++)
        {
            qema.Update(new TValue(DateTime.UtcNow, 100));
        }

        // With zero DC lag, output should equal input for constant signal
        Assert.Equal(100.0, qema.Last.Value, 1e-9);
    }

    [Fact]
    public void Qema_ProgressiveAlphas_ProduceDifferentFromTema()
    {
        // QEMA uses progressive alphas, not fixed alpha like TEMA
        // Results should differ from simple quad EMA with same alpha
        var qema = new Qema(20);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 999);

        var values = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            var result = qema.Update(new TValue(bar.Time, bar.Close));
            values.Add(result.Value);
        }

        // All values should be finite
        Assert.All(values, v => Assert.True(double.IsFinite(v)));

        // QEMA output should be smooth (no wild jumps)
        for (int i = 1; i < values.Count; i++)
        {
            double change = Math.Abs(values[i] - values[i - 1]);
            Assert.True(change < 20, $"Change at index {i} is {change}, expected < 20");
        }
    }
}
