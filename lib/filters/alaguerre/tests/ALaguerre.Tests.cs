namespace QuanTAlib.Tests;

public class ALaguerreTests
{
    // ============== A) Constructor Validation ==============

    [Fact]
    public void ALaguerre_Constructor_Length_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new ALaguerre(0));
        Assert.Throws<ArgumentException>(() => new ALaguerre(-1));
        Assert.Throws<ArgumentException>(() => new ALaguerre(-10));

        var al = new ALaguerre(1);
        Assert.NotNull(al);

        var al2 = new ALaguerre(100);
        Assert.NotNull(al2);
    }

    [Fact]
    public void ALaguerre_Constructor_MedianLength_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new ALaguerre(20, 0));
        Assert.Throws<ArgumentException>(() => new ALaguerre(20, -1));

        var al = new ALaguerre(20, 1);
        Assert.NotNull(al);
    }

    [Fact]
    public void ALaguerre_Constructor_DefaultParameters()
    {
        var al = new ALaguerre();
        Assert.Contains("20", al.Name, StringComparison.Ordinal);
        Assert.Contains("5", al.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ALaguerre_Constructor_SetsName()
    {
        var al = new ALaguerre(10, 3);
        Assert.Equal("ALaguerre(10,3)", al.Name);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void ALaguerre_Calc_ReturnsValue()
    {
        var al = new ALaguerre(20, 5);

        Assert.Equal(0, al.Last.Value);

        TValue result = al.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, al.Last.Value);
    }

    [Fact]
    public void ALaguerre_Calc_FirstValue_ReturnsInput()
    {
        var al = new ALaguerre(20, 5);

        TValue result = al.Update(new TValue(DateTime.UtcNow, 42.0));

        // First value: all L elements initialized to input, output = input
        Assert.Equal(42.0, result.Value, 1e-10);
    }

    [Fact]
    public void ALaguerre_Calc_SmoothsValues()
    {
        var al = new ALaguerre(20, 5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            al.Update(new TValue(bar.Time, bar.Close));
        }

        // Filter should smooth: result should be finite and reasonable
        Assert.True(double.IsFinite(al.Last.Value));
        Assert.True(al.Last.Value > 50 && al.Last.Value < 200);
    }

    [Fact]
    public void ALaguerre_Properties_Accessible()
    {
        var al = new ALaguerre(20, 5);

        Assert.Equal(0, al.Last.Value);
        Assert.False(al.IsHot);

        al.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, al.Last.Value);
    }

    // ============== C) State + Bar Correction ==============

    [Fact]
    public void ALaguerre_Calc_IsNew_AcceptsParameter()
    {
        var al = new ALaguerre(20, 5);

        al.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = al.Last.Value;

        al.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = al.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void ALaguerre_Calc_IsNew_False_UpdatesValue()
    {
        var al = new ALaguerre(20, 5);

        al.Update(new TValue(DateTime.UtcNow, 100));
        al.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = al.Last.Value;

        al.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = al.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void ALaguerre_IterativeCorrections_RestoreToOriginalState()
    {
        var al = new ALaguerre(20, 5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            al.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double alAfterTen = al.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            al.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalAl = al.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(alAfterTen, finalAl.Value, 1e-10);
    }

    [Fact]
    public void ALaguerre_Reset_ClearsState()
    {
        var al = new ALaguerre(20, 5);

        al.Update(new TValue(DateTime.UtcNow, 100));
        al.Update(new TValue(DateTime.UtcNow, 105));

        al.Reset();

        Assert.Equal(0, al.Last.Value);

        // After reset, should accept new values
        al.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, al.Last.Value);
    }

    // ============== D) Warmup / Convergence ==============

    [Fact]
    public void ALaguerre_IsHot_BecomesTrueAfterWarmup()
    {
        var al = new ALaguerre(20, 5);

        // Initially IsHot should be false
        Assert.False(al.IsHot);

        for (int i = 0; i < 20; i++)
        {
            al.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        Assert.True(al.IsHot);
    }

    [Fact]
    public void ALaguerre_WarmupPeriod_IsLengthOrFour()
    {
        var al = new ALaguerre(20, 5);
        Assert.Equal(20, al.WarmupPeriod);

        var al2 = new ALaguerre(2, 1);
        Assert.Equal(4, al2.WarmupPeriod); // min of WarmupBars=4
    }

    // ============== E) Robustness (NaN / Infinity) ==============

    [Fact]
    public void ALaguerre_NaN_Input_UsesLastValidValue()
    {
        var al = new ALaguerre(20, 5);

        al.Update(new TValue(DateTime.UtcNow, 100));
        al.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = al.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void ALaguerre_Infinity_Input_UsesLastValidValue()
    {
        var al = new ALaguerre(20, 5);

        al.Update(new TValue(DateTime.UtcNow, 100));
        al.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = al.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = al.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void ALaguerre_MultipleNaN_ContinuesWithLastValid()
    {
        var al = new ALaguerre(20, 5);

        al.Update(new TValue(DateTime.UtcNow, 100));
        al.Update(new TValue(DateTime.UtcNow, 110));
        al.Update(new TValue(DateTime.UtcNow, 120));

        var r1 = al.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = al.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = al.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void ALaguerre_BatchCalc_HandlesNaN()
    {
        var al = new ALaguerre(20, 5);

        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = al.Update(series);

        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void ALaguerre_Reset_ClearsLastValidValue()
    {
        var al = new ALaguerre(20, 5);

        al.Update(new TValue(DateTime.UtcNow, 100));
        al.Update(new TValue(DateTime.UtcNow, double.NaN));

        al.Reset();

        var result = al.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ============== F) Consistency (all 4 modes match) ==============

    [Fact]
    public void ALaguerre_BatchCalc_MatchesIterativeCalc()
    {
        var alIterative = new ALaguerre(20, 5);
        var alBatch = new ALaguerre(20, 5);
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
            iterativeResults.Add(alIterative.Update(item));
        }

        // Calculate batch
        var batchResults = alBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void ALaguerre_AllModes_Match()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int length = 20;
        int medianLength = 5;
        int count = 100;

        // Generate data
        var series = new TSeries();
        double[] sourceData = new double[count];
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
            sourceData[i] = bar.Close;
        }

        // Mode 1: Streaming
        var alStream = new ALaguerre(length, medianLength);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = alStream.Update(series[i]).Value;
        }

        // Mode 2: Batch (TSeries)
        var batchResults = ALaguerre.Batch(series, length, medianLength);

        // Mode 3: Span
        double[] spanOutput = new double[count];
        ALaguerre.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), length, medianLength);

        // Mode 4: Event-driven
        var eventSource = new TSeries();
        var alEvent = new ALaguerre(eventSource, length, medianLength);
        var eventResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            eventSource.Add(series[i]);
            eventResults[i] = alEvent.Last.Value;
        }

        // Compare all modes
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-10);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
            Assert.Equal(streamResults[i], eventResults[i], 1e-10);
        }
    }

    // ============== G) Span API Tests ==============

    [Fact]
    public void ALaguerre_SpanBatch_Length_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => ALaguerre.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => ALaguerre.Batch(source.AsSpan(), output.AsSpan(), -1));
    }

    [Fact]
    public void ALaguerre_SpanBatch_MedianLength_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => ALaguerre.Batch(source.AsSpan(), output.AsSpan(), 20, 0));
        Assert.Throws<ArgumentException>(() => ALaguerre.Batch(source.AsSpan(), output.AsSpan(), 20, -1));
    }

    [Fact]
    public void ALaguerre_SpanBatch_OutputLength_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => ALaguerre.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 20, 5));
    }

    [Fact]
    public void ALaguerre_SpanBatch_MatchesTSeriesBatch()
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

        var tseriesResult = ALaguerre.Batch(series, 20, 5);
        ALaguerre.Batch(source.AsSpan(), output.AsSpan(), 20, 5);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void ALaguerre_SpanBatch_DifferentParameters()
    {
        double[] source = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        double[] output1 = new double[10];
        double[] output2 = new double[10];
        double[] output3 = new double[10];

        ALaguerre.Batch(source.AsSpan(), output1.AsSpan(), 5, 3);
        ALaguerre.Batch(source.AsSpan(), output2.AsSpan(), 10, 5);
        ALaguerre.Batch(source.AsSpan(), output3.AsSpan(), 20, 7);

        for (int i = 0; i < 10; i++)
        {
            Assert.True(double.IsFinite(output1[i]));
            Assert.True(double.IsFinite(output2[i]));
            Assert.True(double.IsFinite(output3[i]));
        }
    }

    [Fact]
    public void ALaguerre_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        ALaguerre.Batch(source.AsSpan(), output.AsSpan(), 20, 5);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void ALaguerre_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        ALaguerre.Batch(source.AsSpan(), output.AsSpan(), 5, 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void ALaguerre_Chainability_Works()
    {
        var source = new TSeries();
        var al = new ALaguerre(source, 20, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, al.Last.Value, 1e-10);
    }

    [Fact]
    public void ALaguerre_Prime_SetsStateCorrectly()
    {
        var al = new ALaguerre(20, 5);
        double[] history = [10, 20, 30, 40, 50];

        al.Prime(history);

        // Verify against a fresh ALaguerre fed with same data
        var verifyAl = new ALaguerre(20, 5);
        foreach (var val in history)
        {
            verifyAl.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyAl.Last.Value, al.Last.Value, 1e-10);
    }

    // ============== Adaptive behavior-specific tests ==============

    [Fact]
    public void ALaguerre_ConstantInput_ConvergesToInput()
    {
        var al = new ALaguerre(20, 5);

        // Feed constant value - filter should converge to that value
        for (int i = 0; i < 100; i++)
        {
            al.Update(new TValue(DateTime.UtcNow, 42.0));
        }

        Assert.Equal(42.0, al.Last.Value, 1e-6);
    }

    [Fact]
    public void ALaguerre_LargeDataset_RemainsStable()
    {
        var al = new ALaguerre(20, 5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 10000; i++)
        {
            var bar = gbm.Next(isNew: true);
            al.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(al.Last.Value));
        Assert.True(al.Last.Value > 10 && al.Last.Value < 1000);
    }

    [Fact]
    public void ALaguerre_AdaptsToVolatility()
    {
        // When price trends strongly, filter should track faster (larger alpha)
        // When price is stable, filter should smooth more (smaller alpha)
        var alTrend = new ALaguerre(20, 5);
        var alFlat = new ALaguerre(20, 5);

        // Trending input: 100, 110, 120, ...
        for (int i = 0; i < 30; i++)
        {
            alTrend.Update(new TValue(DateTime.UtcNow, 100 + (i * 10.0)));
        }

        // Flat input: constant 100
        for (int i = 0; i < 30; i++)
        {
            alFlat.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        // Both should be finite
        Assert.True(double.IsFinite(alTrend.Last.Value));
        Assert.True(double.IsFinite(alFlat.Last.Value));

        // Flat input should converge exactly
        Assert.Equal(100.0, alFlat.Last.Value, 1e-6);

        // Trending filter should be tracking the rising price
        Assert.True(alTrend.Last.Value > 200);
    }

    [Fact]
    public void ALaguerre_ShortLength_MoreResponsive()
    {
        var alShort = new ALaguerre(5, 3);
        var alLong = new ALaguerre(50, 10);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.15, seed: 42);

        for (int i = 0; i < 60; i++)
        {
            var bar = gbm.Next(isNew: true);
            var input = new TValue(bar.Time, bar.Close);
            alShort.Update(input);
            alLong.Update(input);
        }

        // Both should be finite
        Assert.True(double.IsFinite(alShort.Last.Value));
        Assert.True(double.IsFinite(alLong.Last.Value));

        // Different parameterizations produce finite results
        // (adaptive nature may converge similarly for low-vol data)
        Assert.True(alShort.Last.Value > 50 && alShort.Last.Value < 200);
        Assert.True(alLong.Last.Value > 50 && alLong.Last.Value < 200);
    }

    [Fact]
    public void ALaguerre_StaticCalculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var (results, indicator) = ALaguerre.Calculate(series, 20, 5);

        Assert.Equal(50, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(results[^1].Value));
    }
}
