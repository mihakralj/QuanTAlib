namespace QuanTAlib.Tests;

public class CmaTests
{
    [Fact]
    public void Cma_Calc_ReturnsValue()
    {
        var cma = new Cma();

        Assert.Equal(0, cma.Last.Value);

        TValue result = cma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, cma.Last.Value);
    }

    [Fact]
    public void Cma_FirstValue_ReturnsItself()
    {
        var cma = new Cma();

        TValue result = cma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Cma_Calc_IsNew_AcceptsParameter()
    {
        var cma = new Cma();

        cma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = cma.Last.Value;

        cma.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = cma.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Cma_Calc_IsNew_False_UpdatesValue()
    {
        var cma = new Cma();

        cma.Update(new TValue(DateTime.UtcNow, 100));
        cma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = cma.Last.Value;

        cma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = cma.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Cma_Reset_ClearsState()
    {
        var cma = new Cma();

        cma.Update(new TValue(DateTime.UtcNow, 100));
        cma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = cma.Last.Value;

        cma.Reset();

        Assert.Equal(0, cma.Last.Value);
        Assert.False(cma.IsHot);

        // After reset, should accept new values
        cma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, cma.Last.Value);
        Assert.NotEqual(valueBefore, cma.Last.Value);
    }

    [Fact]
    public void Cma_Properties_Accessible()
    {
        var cma = new Cma();

        Assert.Equal(0, cma.Last.Value);
        Assert.False(cma.IsHot);

        cma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, cma.Last.Value);
        Assert.True(cma.IsHot);
    }

    [Fact]
    public void Cma_IsHot_BecomesTrueAfterFirstValue()
    {
        var cma = new Cma();

        Assert.False(cma.IsHot);

        cma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(cma.IsHot);
    }

    [Fact]
    public void Cma_CalculatesCorrectAverage()
    {
        var cma = new Cma();

        cma.Update(new TValue(DateTime.UtcNow, 10));
        Assert.Equal(10.0, cma.Last.Value, 1e-10); // (10)/1 = 10

        cma.Update(new TValue(DateTime.UtcNow, 20));
        Assert.Equal(15.0, cma.Last.Value, 1e-10); // (10+20)/2 = 15

        cma.Update(new TValue(DateTime.UtcNow, 30));
        Assert.Equal(20.0, cma.Last.Value, 1e-10); // (10+20+30)/3 = 20

        cma.Update(new TValue(DateTime.UtcNow, 40));
        Assert.Equal(25.0, cma.Last.Value, 1e-10); // (10+20+30+40)/4 = 25

        cma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(30.0, cma.Last.Value, 1e-10); // (10+20+30+40+50)/5 = 30
    }

    [Fact]
    public void Cma_IncludesAllValues_NoSlidingWindow()
    {
        var cma = new Cma();

        // Add 10 values: 10, 20, 30, ..., 100
        for (int i = 1; i <= 10; i++)
        {
            cma.Update(new TValue(DateTime.UtcNow, i * 10));
        }

        // CMA of 10,20,30,40,50,60,70,80,90,100 = 550/10 = 55
        Assert.Equal(55.0, cma.Last.Value, 1e-10);

        // Add one more value
        cma.Update(new TValue(DateTime.UtcNow, 110));

        // CMA now includes ALL 11 values: (550 + 110)/11 = 660/11 = 60
        Assert.Equal(60.0, cma.Last.Value, 1e-10);
    }

    [Fact]
    public void Cma_IterativeCorrections_RestoreToOriginalState()
    {
        var cma = new Cma();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            cma.Update(tenthInput, isNew: true);
        }

        // Remember CMA state after 10 values
        double cmaAfterTen = cma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            cma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalCma = cma.Update(tenthInput, isNew: false);

        // CMA should match the original state after 10 values
        Assert.Equal(cmaAfterTen, finalCma.Value, 1e-10);
    }

    [Fact]
    public void Cma_BatchCalc_MatchesIterativeCalc()
    {
        var cmaIterative = new Cma();
        var cmaBatch = new Cma();
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
            iterativeResults.Add(cmaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = cmaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Cma_NaN_Input_UsesLastValidValue()
    {
        var cma = new Cma();

        // Feed some valid values
        cma.Update(new TValue(DateTime.UtcNow, 100));
        cma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = cma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Cma_Infinity_Input_UsesLastValidValue()
    {
        var cma = new Cma();

        // Feed some valid values
        cma.Update(new TValue(DateTime.UtcNow, 100));
        cma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = cma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = cma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Cma_MultipleNaN_ContinuesWithLastValid()
    {
        var cma = new Cma();

        // Feed valid values
        cma.Update(new TValue(DateTime.UtcNow, 100));
        cma.Update(new TValue(DateTime.UtcNow, 110));
        cma.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = cma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = cma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = cma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Cma_BatchCalc_HandlesNaN()
    {
        var cma = new Cma();

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = cma.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Cma_Reset_ClearsLastValidValue()
    {
        var cma = new Cma();

        // Feed values including NaN
        cma.Update(new TValue(DateTime.UtcNow, 100));
        cma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        cma.Reset();

        // After reset, first valid value should establish new baseline
        var result = cma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    [Fact]
    public void Cma_StaticBatch_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var results = Cma.Batch(series);

        Assert.Equal(5, results.Count);
        // CMA for last value: (10+20+30+40+50)/5 = 30
        Assert.Equal(30.0, results.Last.Value, 1e-10);
    }

    [Fact]
    public void Cma_FlatLine_ReturnsSameValue()
    {
        var cma = new Cma();

        for (int i = 0; i < 20; i++)
        {
            cma.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.Equal(100.0, cma.Last.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Cma_SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongSizeOutput = new double[3];

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Cma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan()));
    }

    [Fact]
    public void Cma_SpanBatch_MatchesTSeriesBatch()
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
        var tseriesResult = Cma.Batch(series);

        // Calculate with Span API
        Cma.Batch(source.AsSpan(), output.AsSpan());

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Cma_SpanBatch_CalculatesCorrectly()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Cma.Batch(source.AsSpan(), output.AsSpan());

        Assert.Equal(10.0, output[0], 1e-10); // 10/1 = 10
        Assert.Equal(15.0, output[1], 1e-10); // (10+20)/2 = 15
        Assert.Equal(20.0, output[2], 1e-10); // (10+20+30)/3 = 20
        Assert.Equal(25.0, output[3], 1e-10); // (10+20+30+40)/4 = 25
        Assert.Equal(30.0, output[4], 1e-10); // (10+20+30+40+50)/5 = 30
    }

    [Fact]
    public void Cma_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
            source[i] = gbm.Next().Close;

        // Warm up
        Cma.Batch(source.AsSpan(), output.AsSpan());

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Cma_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Cma.Batch(source.AsSpan(), output.AsSpan());

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Cma_AllModes_ProduceSameResult()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Cma.Batch(series);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Cma.Batch(spanInput, spanOutput);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Cma();
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Cma(pubSource);
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
        var cma = new Cma(source);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, cma.Last.Value);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var cma = new Cma();
        Assert.Equal(1, cma.WarmupPeriod);
    }

    [Fact]
    public void Prime_SetsStateCorrectly()
    {
        var cma = new Cma();
        double[] history = [10, 20, 30, 40, 50]; // CMA = 30

        cma.Prime(history);

        Assert.True(cma.IsHot);
        Assert.Equal(30.0, cma.Last.Value, 1e-10);

        // Verify it continues correctly
        cma.Update(new TValue(DateTime.UtcNow, 60)); // (10+20+30+40+50+60)/6 = 35
        Assert.Equal(35.0, cma.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_HandlesNaN_InHistory()
    {
        var cma = new Cma();
        double[] history = [10, 20, double.NaN, 40];
        // 10 -> 10
        // 10, 20 -> 15
        // 10, 20, 20 (NaN replaced by 20) -> 16.666...
        // 10, 20, 20, 40 -> 22.5

        cma.Prime(history);

        Assert.True(cma.IsHot);
        Assert.Equal(22.5, cma.Last.Value, 1e-9);
    }

    [Fact]
    public void Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 10; i++) series.Add(DateTime.UtcNow, i * 10);
        // 10, 20, 30, 40, 50, 60, 70, 80, 90, 100

        var (results, indicator) = Cma.Calculate(series);

        // Check results
        Assert.Equal(10, results.Count);
        Assert.Equal(30.0, results[4].Value, 1e-10); // CMA after 5 values = 30
        Assert.Equal(55.0, results.Last.Value, 1e-10); // CMA of all 10 = 55

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(55.0, indicator.Last.Value, 1e-10);
        Assert.Equal(1, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 110));
        // CMA now = (550 + 110)/11 = 60
        Assert.Equal(60.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Cma_NumericalStability_LargeDataset()
    {
        // Test that CMA remains stable over a large number of values
        var cma = new Cma();
        double expectedSum = 0;

        for (int i = 1; i <= 100000; i++)
        {
            cma.Update(new TValue(DateTime.UtcNow, 100.0)); // All same value
            expectedSum += 100.0;
        }

        // CMA of 100000 values all equal to 100 should be exactly 100
        Assert.Equal(100.0, cma.Last.Value, 1e-9);
    }

    [Fact]
    public void Cma_NumericalStability_VaryingValues()
    {
        // Test with alternating values
        var cma = new Cma();

        for (int i = 0; i < 10000; i++)
        {
            double value = (i % 2 == 0) ? 100.0 : 200.0;
            cma.Update(new TValue(DateTime.UtcNow, value));
        }

        // CMA of alternating 100, 200 should converge to 150
        Assert.Equal(150.0, cma.Last.Value, 1e-9);
    }
}
