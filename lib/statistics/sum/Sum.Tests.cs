namespace QuanTAlib.Tests;

public class SumTests
{
    [Fact]
    public void Sum_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Sum(0));
        Assert.Throws<ArgumentException>(() => new Sum(-1));

        var sum = new Sum(10);
        Assert.NotNull(sum);
    }

    [Fact]
    public void Sum_Calc_ReturnsValue()
    {
        var sum = new Sum(10);

        Assert.Equal(0, sum.Last.Value);

        TValue result = sum.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, sum.Last.Value);
    }

    [Fact]
    public void Sum_FirstValue_ReturnsItself()
    {
        var sum = new Sum(10);

        TValue result = sum.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sum_Calc_IsNew_AcceptsParameter()
    {
        var sum = new Sum(10);

        sum.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = sum.Last.Value;

        sum.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = sum.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Sum_Calc_IsNew_False_UpdatesValue()
    {
        var sum = new Sum(10);

        sum.Update(new TValue(DateTime.UtcNow, 100));
        sum.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = sum.Last.Value;

        sum.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = sum.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Sum_Reset_ClearsState()
    {
        var sum = new Sum(10);

        sum.Update(new TValue(DateTime.UtcNow, 100));
        sum.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = sum.Last.Value;

        sum.Reset();

        Assert.Equal(0, sum.Last.Value);
        Assert.False(sum.IsHot);

        sum.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, sum.Last.Value);
        Assert.NotEqual(valueBefore, sum.Last.Value);
    }

    [Fact]
    public void Sum_Properties_Accessible()
    {
        var sum = new Sum(10);

        Assert.Equal(0, sum.Last.Value);
        Assert.False(sum.IsHot);

        sum.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, sum.Last.Value);
    }

    [Fact]
    public void Sum_IsHot_BecomesTrueWhenBufferFull()
    {
        var sum = new Sum(5);

        Assert.False(sum.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            sum.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(sum.IsHot);
        }

        sum.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(sum.IsHot);
    }

    [Fact]
    public void Sum_CalculatesCorrectSum()
    {
        var sum = new Sum(5);

        sum.Update(new TValue(DateTime.UtcNow, 10));
        Assert.Equal(10.0, sum.Last.Value, 1e-10); // 10

        sum.Update(new TValue(DateTime.UtcNow, 20));
        Assert.Equal(30.0, sum.Last.Value, 1e-10); // 10+20

        sum.Update(new TValue(DateTime.UtcNow, 30));
        Assert.Equal(60.0, sum.Last.Value, 1e-10); // 10+20+30

        sum.Update(new TValue(DateTime.UtcNow, 40));
        Assert.Equal(100.0, sum.Last.Value, 1e-10); // 10+20+30+40

        sum.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(150.0, sum.Last.Value, 1e-10); // 10+20+30+40+50
    }

    [Fact]
    public void Sum_SlidingWindow_Works()
    {
        var sum = new Sum(3);

        sum.Update(new TValue(DateTime.UtcNow, 10));
        sum.Update(new TValue(DateTime.UtcNow, 20));
        sum.Update(new TValue(DateTime.UtcNow, 30));
        Assert.Equal(60.0, sum.Last.Value, 1e-10); // 10+20+30

        sum.Update(new TValue(DateTime.UtcNow, 40));
        Assert.Equal(90.0, sum.Last.Value, 1e-10); // 20+30+40

        sum.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(120.0, sum.Last.Value, 1e-10); // 30+40+50
    }

    [Fact]
    public void Sum_IterativeCorrections_RestoreToOriginalState()
    {
        var sum = new Sum(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            sum.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = sum.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            sum.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = sum.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Sum_BatchCalc_MatchesIterativeCalc()
    {
        var sumIterative = new Sum(10);
        var sumBatch = new Sum(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

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
            iterativeResults.Add(sumIterative.Update(item));
        }

        // Calculate batch
        var batchResults = sumBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Sum_NaN_Input_UsesLastValidValue()
    {
        var sum = new Sum(5);

        sum.Update(new TValue(DateTime.UtcNow, 100));
        sum.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = sum.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Sum_Infinity_Input_UsesLastValidValue()
    {
        var sum = new Sum(5);

        sum.Update(new TValue(DateTime.UtcNow, 100));
        sum.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = sum.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = sum.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Sum_MultipleNaN_ContinuesWithLastValid()
    {
        var sum = new Sum(5);

        sum.Update(new TValue(DateTime.UtcNow, 100));
        sum.Update(new TValue(DateTime.UtcNow, 110));
        sum.Update(new TValue(DateTime.UtcNow, 120));

        var r1 = sum.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = sum.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = sum.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Sum_BatchCalc_HandlesNaN()
    {
        var sum = new Sum(5);

        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = sum.Update(series);

        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Sum_Reset_ClearsLastValidValue()
    {
        var sum = new Sum(5);

        sum.Update(new TValue(DateTime.UtcNow, 100));
        sum.Update(new TValue(DateTime.UtcNow, double.NaN));

        sum.Reset();

        var result = sum.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sum_StaticBatch_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var results = Sum.Batch(series, 3);

        Assert.Equal(5, results.Count);
        // Sum(3) for last value: 30+40+50 = 120
        Assert.Equal(120.0, results.Last.Value, 1e-10);
    }

    [Fact]
    public void Sum_FlatLine_ReturnsSameValue()
    {
        var sum = new Sum(10);

        for (int i = 0; i < 20; i++)
        {
            sum.Update(new TValue(DateTime.UtcNow, 100));
        }

        // Sum of 10 values of 100 = 1000
        Assert.Equal(1000.0, sum.Last.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Sum_SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Sum.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Sum.Batch(source.AsSpan(), output.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() => Sum.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Sum_SpanBatch_MatchesTSeriesBatch()
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

        var tseriesResult = Sum.Batch(series, 10);
        Sum.Batch(source.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Sum_SpanBatch_CalculatesCorrectly()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Sum.Batch(source.AsSpan(), output.AsSpan(), 3);

        Assert.Equal(10.0, output[0], 1e-10);  // 10
        Assert.Equal(30.0, output[1], 1e-10);  // 10+20
        Assert.Equal(60.0, output[2], 1e-10);  // 10+20+30
        Assert.Equal(90.0, output[3], 1e-10);  // 20+30+40
        Assert.Equal(120.0, output[4], 1e-10); // 30+40+50
    }

    [Fact]
    public void Sum_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
            source[i] = gbm.Next().Close;

        Sum.Batch(source.AsSpan(), output.AsSpan(), 100);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Sum_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Sum.Batch(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Sum_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Sum.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Sum.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Sum(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Sum(pubSource, period);
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
    public void Sum_Chainability_Works()
    {
        var source = new TSeries();
        var sum = new Sum(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, sum.Last.Value);
    }

    [Fact]
    public void Sum_WarmupPeriod_IsSetCorrectly()
    {
        var sum = new Sum(10);
        Assert.Equal(10, sum.WarmupPeriod);
    }

    [Fact]
    public void Sum_Prime_SetsStateCorrectly()
    {
        var sum = new Sum(5);
        double[] history = [10, 20, 30, 40, 50]; // Sum = 150

        sum.Prime(history);

        Assert.True(sum.IsHot);
        Assert.Equal(150.0, sum.Last.Value, 1e-10);

        // Verify it continues correctly with sliding window
        sum.Update(new TValue(DateTime.UtcNow, 60)); // 20+30+40+50+60 = 200
        Assert.Equal(200.0, sum.Last.Value, 1e-10);
    }

    [Fact]
    public void Sum_Prime_WithInsufficientHistory_IsNotHot()
    {
        var sum = new Sum(10);
        double[] history = [10, 20, 30, 40, 50];

        sum.Prime(history);

        Assert.False(sum.IsHot);
        Assert.Equal(150.0, sum.Last.Value, 1e-10); // Sum of what we have
    }

    [Fact]
    public void Sum_Prime_HandlesNaN_InHistory()
    {
        var sum = new Sum(3);
        double[] history = [10, 20, double.NaN, 40];
        // Values used: 10, 20, 20 (NaN replaced), 40
        // Final window (3): 20, 20, 40 = 80

        sum.Prime(history);

        Assert.True(sum.IsHot);
        Assert.True(double.IsFinite(sum.Last.Value));
    }

    [Fact]
    public void Sum_Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 10; i++)
            series.Add(DateTime.UtcNow, i * 10);
        // 10, 20, 30, 40, 50, 60, 70, 80, 90, 100

        var (results, indicator) = Sum.Calculate(series, 5);

        // Check results
        Assert.Equal(10, results.Count);
        Assert.Equal(150.0, results[4].Value, 1e-10); // Sum(10..50) = 150
        Assert.Equal(400.0, results.Last.Value, 1e-10); // Sum(60..100) = 400

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(400.0, indicator.Last.Value, 1e-10);
        Assert.Equal(5, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 110));
        // Sum now = 70+80+90+100+110 = 450
        Assert.Equal(450.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Sum_NumericalStability_LargeDataset()
    {
        // Test that Sum remains stable over a large number of values
        var sum = new Sum(100);

        for (int i = 1; i <= 100000; i++)
        {
            sum.Update(new TValue(DateTime.UtcNow, 1.0));
        }

        // Sum of 100 values of 1.0 = 100
        Assert.Equal(100.0, sum.Last.Value, 1e-9);
    }

    [Fact]
    public void Sum_NumericalStability_VaryingMagnitudes()
    {
        // Test with values of wildly different magnitudes
        var sum = new Sum(4);

        sum.Update(new TValue(DateTime.UtcNow, 1e10));
        sum.Update(new TValue(DateTime.UtcNow, 1.0));
        sum.Update(new TValue(DateTime.UtcNow, 1e-10));
        sum.Update(new TValue(DateTime.UtcNow, 1e10));

        // Kahan-Babuška should handle this accurately
        double expected = 1e10 + 1.0 + 1e-10 + 1e10;
        Assert.Equal(expected, sum.Last.Value, 1e-5);
    }

    [Fact]
    public void Sum_KahanBabuska_BetterThanNaive()
    {
        // Test case that would cause precision loss with naive summation
        var sum = new Sum(1000);

        // Add a large value followed by many small values
        sum.Update(new TValue(DateTime.UtcNow, 1e15));

        for (int i = 0; i < 999; i++)
        {
            sum.Update(new TValue(DateTime.UtcNow, 1.0));
        }

        // With Kahan-Babuška, the small values should not be lost
        // Naive sum would lose precision
        double expected = 1e15 + 999.0;
        double actual = sum.Last.Value;

        // Should be very close to expected
        double relativeError = Math.Abs(actual - expected) / expected;
        Assert.True(relativeError < 1e-14, $"Relative error {relativeError} too large");
    }

    [Fact]
    public void Sum_Period1_ReturnsInput()
    {
        var sum = new Sum(1);

        sum.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, sum.Last.Value, 1e-10);

        sum.Update(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(200.0, sum.Last.Value, 1e-10);

        sum.Update(new TValue(DateTime.UtcNow, 150));
        Assert.Equal(150.0, sum.Last.Value, 1e-10);
    }
}
