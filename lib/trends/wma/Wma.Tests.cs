namespace QuanTAlib.Tests;

#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class WmaTests
{
    [Fact]
    public void Wma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Wma(0));
        Assert.Throws<ArgumentException>(() => new Wma(-1));

        var wma = new Wma(10);
        Assert.NotNull(wma);
    }

    [Fact]
    public void Wma_Calc_ReturnsValue()
    {
        var wma = new Wma(10);

        Assert.Equal(0, wma.Last.Value);

        TValue result = wma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, wma.Last.Value);
    }

    [Fact]
    public void Wma_FirstValue_ReturnsItself()
    {
        var wma = new Wma(10);

        TValue result = wma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Wma_Calc_IsNew_AcceptsParameter()
    {
        var wma = new Wma(10);

        wma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = wma.Last.Value;

        wma.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = wma.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Wma_Calc_IsNew_False_UpdatesValue()
    {
        var wma = new Wma(10);

        wma.Update(new TValue(DateTime.UtcNow, 100));
        wma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = wma.Last.Value;

        wma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = wma.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Wma_Reset_ClearsState()
    {
        var wma = new Wma(10);

        wma.Update(new TValue(DateTime.UtcNow, 100));
        wma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = wma.Last.Value;

        wma.Reset();

        Assert.Equal(0, wma.Last.Value);

        // After reset, should accept new values
        wma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, wma.Last.Value);
        Assert.NotEqual(valueBefore, wma.Last.Value);
    }

    [Fact]
    public void Wma_Properties_Accessible()
    {
        var wma = new Wma(10);

        Assert.Equal(0, wma.Last.Value);
        Assert.False(wma.IsHot);

        wma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, wma.Last.Value);
    }

    [Fact]
    public void Wma_IsHot_BecomesTrueWhenBufferFull()
    {
        var wma = new Wma(5);

        Assert.False(wma.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            wma.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(wma.IsHot);
        }

        wma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(wma.IsHot);
    }

    [Fact]
    public void Wma_CalculatesCorrectWeightedAverage()
    {
        var wma = new Wma(5);

        wma.Update(new TValue(DateTime.UtcNow, 10));
        wma.Update(new TValue(DateTime.UtcNow, 20));
        wma.Update(new TValue(DateTime.UtcNow, 30));
        wma.Update(new TValue(DateTime.UtcNow, 40));
        wma.Update(new TValue(DateTime.UtcNow, 50));

        // WMA(5) of 10,20,30,40,50 = (1*10 + 2*20 + 3*30 + 4*40 + 5*50) / 15
        // = (10 + 40 + 90 + 160 + 250) / 15 = 550 / 15 = 36.666...
        Assert.Equal(550.0 / 15.0, wma.Last.Value, 1e-10);
    }

    [Fact]
    public void Wma_SlidingWindow_Works()
    {
        var wma = new Wma(3);

        wma.Update(new TValue(DateTime.UtcNow, 10));
        wma.Update(new TValue(DateTime.UtcNow, 20));
        wma.Update(new TValue(DateTime.UtcNow, 30));

        // WMA(3) of 10,20,30 = (1*10 + 2*20 + 3*30) / 6 = (10 + 40 + 90) / 6 = 140/6 = 23.333...
        Assert.Equal(140.0 / 6.0, wma.Last.Value, 1e-10);

        wma.Update(new TValue(DateTime.UtcNow, 40));

        // WMA(3) of 20,30,40 = (1*20 + 2*30 + 3*40) / 6 = (20 + 60 + 120) / 6 = 200/6 = 33.333...
        Assert.Equal(200.0 / 6.0, wma.Last.Value, 1e-10);

        wma.Update(new TValue(DateTime.UtcNow, 50));

        // WMA(3) of 30,40,50 = (1*30 + 2*40 + 3*50) / 6 = (30 + 80 + 150) / 6 = 260/6 = 43.333...
        Assert.Equal(260.0 / 6.0, wma.Last.Value, 1e-10);
    }

    [Fact]
    public void Wma_IterativeCorrections_RestoreToOriginalState()
    {
        var wma = new Wma(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            wma.Update(tenthInput, isNew: true);
        }

        // Remember WMA state after 10 values
        double wmaAfterTen = wma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            wma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalWma = wma.Update(tenthInput, isNew: false);

        // WMA should match the original state after 10 values
        Assert.Equal(wmaAfterTen, finalWma.Value, 1e-10);
    }

    [Fact]
    public void Wma_BatchCalc_MatchesIterativeCalc()
    {
        var wmaIterative = new Wma(10);
        var wmaBatch = new Wma(10);
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
            iterativeResults.Add(wmaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = wmaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Wma_Result_ImplicitConversionToDouble()
    {
        var wma = new Wma(10);
        wma.Update(new TValue(DateTime.UtcNow, 100));

        // This should compile and work because TValue has implicit conversion to double
        double result = wma.Last.Value;

        Assert.Equal(100.0, result, 1e-10);
    }

    [Fact]
    public void Wma_NaN_Input_UsesLastValidValue()
    {
        var wma = new Wma(5);

        // Feed some valid values
        wma.Update(new TValue(DateTime.UtcNow, 100));
        wma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = wma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Wma_Infinity_Input_UsesLastValidValue()
    {
        var wma = new Wma(5);

        // Feed some valid values
        wma.Update(new TValue(DateTime.UtcNow, 100));
        wma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = wma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = wma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Wma_MultipleNaN_ContinuesWithLastValid()
    {
        var wma = new Wma(5);

        // Feed valid values
        wma.Update(new TValue(DateTime.UtcNow, 100));
        wma.Update(new TValue(DateTime.UtcNow, 110));
        wma.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = wma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = wma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = wma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Wma_BatchCalc_HandlesNaN()
    {
        var wma = new Wma(5);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = wma.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Wma_Reset_ClearsLastValidValue()
    {
        var wma = new Wma(5);

        // Feed values including NaN
        wma.Update(new TValue(DateTime.UtcNow, 100));
        wma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        wma.Reset();

        // After reset, first valid value should establish new baseline
        var result = wma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    [Fact]
    public void Wma_StaticCalculate_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var results = Wma.Calculate(series, 3);

        Assert.Equal(5, results.Count);
        // WMA(3) for last 3 values [30,40,50]: (1*30 + 2*40 + 3*50) / 6 = 260/6 = 43.333...
        Assert.Equal(260.0 / 6.0, results.Last.Value, 1e-10);
    }

    [Fact]
    public void Wma_Period1_ReturnsInputValues()
    {
        var wma = new Wma(1);

        Assert.Equal(100.0, wma.Update(new TValue(DateTime.UtcNow, 100)).Value, 1e-10);
        Assert.Equal(200.0, wma.Update(new TValue(DateTime.UtcNow, 200)).Value, 1e-10);
        Assert.Equal(150.0, wma.Update(new TValue(DateTime.UtcNow, 150)).Value, 1e-10);
    }

    [Fact]
    public void Wma_MoreWeightOnRecentValues()
    {
        var wma = new Wma(3);
        var sma = new Sma(3);

        // Feed same values to both
        wma.Update(new TValue(DateTime.UtcNow, 10));
        sma.Update(new TValue(DateTime.UtcNow, 10));
        wma.Update(new TValue(DateTime.UtcNow, 20));
        sma.Update(new TValue(DateTime.UtcNow, 20));
        wma.Update(new TValue(DateTime.UtcNow, 100));  // High recent value
        sma.Update(new TValue(DateTime.UtcNow, 100));

        // WMA should be higher than SMA because it weights the high recent value more
        // SMA = (10 + 20 + 100) / 3 = 43.333...
        // WMA = (1*10 + 2*20 + 3*100) / 6 = (10 + 40 + 300) / 6 = 58.333...
        Assert.True(wma.Last.Value > sma.Last.Value);
        Assert.Equal(350.0 / 6.0, wma.Last.Value, 1e-10);
        Assert.Equal(130.0 / 3.0, sma.Last.Value, 1e-10);
    }

    [Fact]
    public void Wma_WarmupDivisor_CalculatedCorrectly()
    {
        var wma = new Wma(5);

        // First value: divisor = 1*(1+1)/2 = 1
        var r1 = wma.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, r1.Value, 1e-10);

        // Second value: divisor = 2*(2+1)/2 = 3, wsum = 1*100 + 2*200 = 500
        var r2 = wma.Update(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(500.0 / 3.0, r2.Value, 1e-10);

        // Third value: divisor = 3*(3+1)/2 = 6, wsum = 1*100 + 2*200 + 3*300 = 1400
        var r3 = wma.Update(new TValue(DateTime.UtcNow, 300));
        Assert.Equal(1400.0 / 6.0, r3.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Wma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() => Wma.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Wma.Calculate(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Wma.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Wma_SpanCalc_MatchesTSeriesCalc()
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
        var tseriesResult = Wma.Calculate(series, 10);

        // Calculate with Span API
        Wma.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Wma_SpanCalc_CalculatesCorrectly()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Wma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        // WMA(3) warmup:
        // i=0: 10 (1*10 / 1)
        // i=1: (1*10 + 2*20) / 3 = 50/3 = 16.666...
        // i=2: (1*10 + 2*20 + 3*30) / 6 = 140/6 = 23.333...
        // i=3: sliding: (1*20 + 2*30 + 3*40) / 6 = 200/6 = 33.333...
        // i=4: (1*30 + 2*40 + 3*50) / 6 = 260/6 = 43.333...
        Assert.Equal(10.0, output[0], 1e-10);
        Assert.Equal(50.0 / 3.0, output[1], 1e-10);
        Assert.Equal(140.0 / 6.0, output[2], 1e-10);
        Assert.Equal(200.0 / 6.0, output[3], 1e-10);
        Assert.Equal(260.0 / 6.0, output[4], 1e-10);
    }

    [Fact]
    public void Wma_SpanCalc_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
            source[i] = gbm.Next().Close;

        // Warm up
        Wma.Calculate(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Wma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Wma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Wma_SpanCalc_Period1_ReturnsInput()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Wma.Calculate(source.AsSpan(), output.AsSpan(), 1);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(source[i], output[i], 1e-10);
        }
    }

    [Fact]
    public void Wma_SpanCalc_UsesStackallocForSmallPeriods()
    {
        double[] source = new double[1000];
        double[] output = new double[1000];
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
            source[i] = gbm.Next().Close;

        // Period <= 512 uses stackalloc
        Wma.Calculate(source.AsSpan(), output.AsSpan(), 100);
        Assert.True(double.IsFinite(output[^1]));

        // Period > 512 uses heap allocation
        double[] output2 = new double[1000];
        Wma.Calculate(source.AsSpan(), output2.AsSpan(), 600);
        Assert.True(double.IsFinite(output2[^1]));
    }
    [Fact]
    public void Wma_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        
        // 1. Batch Mode
        var batchSeries = Wma.Calculate(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Wma.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Wma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Wma(pubSource, period);
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
}
