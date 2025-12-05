namespace QuanTAlib.Tests;

#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class SmaTests
{
    [Fact]
    public void Sma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Sma(0));
        Assert.Throws<ArgumentException>(() => new Sma(-1));

        var sma = new Sma(10);
        Assert.NotNull(sma);
    }

    [Fact]
    public void Sma_Calc_ReturnsValue()
    {
        var sma = new Sma(10);

        Assert.Equal(0, sma.Value.Value);

        TValue result = sma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, sma.Value.Value);
    }

    [Fact]
    public void Sma_FirstValue_ReturnsItself()
    {
        var sma = new Sma(10);

        TValue result = sma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sma_Calc_IsNew_AcceptsParameter()
    {
        var sma = new Sma(10);

        sma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = sma.Value;

        sma.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = sma.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Sma_Calc_IsNew_False_UpdatesValue()
    {
        var sma = new Sma(10);

        sma.Update(new TValue(DateTime.UtcNow, 100));
        sma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = sma.Value;

        sma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = sma.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Sma_Reset_ClearsState()
    {
        var sma = new Sma(10);

        sma.Update(new TValue(DateTime.UtcNow, 100));
        sma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = sma.Value;

        sma.Reset();

        Assert.Equal(0, sma.Value.Value);

        // After reset, should accept new values
        sma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, sma.Value.Value);
        Assert.NotEqual(valueBefore, sma.Value.Value);
    }

    [Fact]
    public void Sma_Properties_Accessible()
    {
        var sma = new Sma(10);

        Assert.Equal(0, sma.Value.Value);
        Assert.False(sma.IsHot);

        sma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, sma.Value.Value);
    }

    [Fact]
    public void Sma_IsHot_BecomesTrueWhenBufferFull()
    {
        var sma = new Sma(5);

        Assert.False(sma.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            sma.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(sma.IsHot);
        }

        sma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(sma.IsHot);
    }

    [Fact]
    public void Sma_CalculatesCorrectAverage()
    {
        var sma = new Sma(5);

        sma.Update(new TValue(DateTime.UtcNow, 10));
        sma.Update(new TValue(DateTime.UtcNow, 20));
        sma.Update(new TValue(DateTime.UtcNow, 30));
        sma.Update(new TValue(DateTime.UtcNow, 40));
        sma.Update(new TValue(DateTime.UtcNow, 50));

        // SMA(5) of 10,20,30,40,50 = 150/5 = 30
        Assert.Equal(30.0, sma.Value.Value, 1e-10);
    }

    [Fact]
    public void Sma_SlidingWindow_Works()
    {
        var sma = new Sma(3);

        sma.Update(new TValue(DateTime.UtcNow, 10));
        sma.Update(new TValue(DateTime.UtcNow, 20));
        sma.Update(new TValue(DateTime.UtcNow, 30));

        // SMA(3) of 10,20,30 = 60/3 = 20
        Assert.Equal(20.0, sma.Value.Value, 1e-10);

        sma.Update(new TValue(DateTime.UtcNow, 40));

        // SMA(3) of 20,30,40 = 90/3 = 30
        Assert.Equal(30.0, sma.Value.Value, 1e-10);

        sma.Update(new TValue(DateTime.UtcNow, 50));

        // SMA(3) of 30,40,50 = 120/3 = 40
        Assert.Equal(40.0, sma.Value.Value, 1e-10);
    }

    [Fact]
    public void Sma_IterativeCorrections_RestoreToOriginalState()
    {
        var sma = new Sma(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            sma.Update(tenthInput, isNew: true);
        }

        // Remember SMA state after 10 values
        double smaAfterTen = sma.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            sma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalSma = sma.Update(tenthInput, isNew: false);

        // SMA should match the original state after 10 values
        Assert.Equal(smaAfterTen, finalSma.Value, 1e-10);
    }

    [Fact]
    public void Sma_BatchCalc_MatchesIterativeCalc()
    {
        var smaIterative = new Sma(10);
        var smaBatch = new Sma(10);
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
            iterativeResults.Add(smaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = smaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Sma_Result_ImplicitConversionToDouble()
    {
        var sma = new Sma(10);
        sma.Update(new TValue(DateTime.UtcNow, 100));

        // This should compile and work because TValue has implicit conversion to double
        double result = sma.Value;

        Assert.Equal(100.0, result, 1e-10);
    }

    [Fact]
    public void Sma_NaN_Input_UsesLastValidValue()
    {
        var sma = new Sma(5);

        // Feed some valid values
        sma.Update(new TValue(DateTime.UtcNow, 100));
        sma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = sma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Sma_Infinity_Input_UsesLastValidValue()
    {
        var sma = new Sma(5);

        // Feed some valid values
        sma.Update(new TValue(DateTime.UtcNow, 100));
        sma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = sma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = sma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Sma_MultipleNaN_ContinuesWithLastValid()
    {
        var sma = new Sma(5);

        // Feed valid values
        sma.Update(new TValue(DateTime.UtcNow, 100));
        sma.Update(new TValue(DateTime.UtcNow, 110));
        sma.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = sma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = sma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = sma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Sma_BatchCalc_HandlesNaN()
    {
        var sma = new Sma(5);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = sma.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Sma_Reset_ClearsLastValidValue()
    {
        var sma = new Sma(5);

        // Feed values including NaN
        sma.Update(new TValue(DateTime.UtcNow, 100));
        sma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        sma.Reset();

        // After reset, first valid value should establish new baseline
        var result = sma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sma_StaticCalculate_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var results = Sma.Calculate(series, 3);

        Assert.Equal(5, results.Count);
        // SMA(3) for last value: (30+40+50)/3 = 40
        Assert.Equal(40.0, results.Last.Value, 1e-10);
    }

    [Fact]
    public void Sma_Period1_ReturnsInputValues()
    {
        var sma = new Sma(1);

        Assert.Equal(100.0, sma.Update(new TValue(DateTime.UtcNow, 100)).Value, 1e-10);
        Assert.Equal(200.0, sma.Update(new TValue(DateTime.UtcNow, 200)).Value, 1e-10);
        Assert.Equal(150.0, sma.Update(new TValue(DateTime.UtcNow, 150)).Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Sma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() => Sma.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Sma.Calculate(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Sma.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Sma_SpanCalc_MatchesTSeriesCalc()
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
        var tseriesResult = Sma.Calculate(series, 10);

        // Calculate with Span API
        Sma.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Sma_SpanCalc_CalculatesCorrectly()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Sma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        // SMA(3) warmup: 10, (10+20)/2=15, (10+20+30)/3=20, then sliding: (20+30+40)/3=30, (30+40+50)/3=40
        Assert.Equal(10.0, output[0], 1e-10);
        Assert.Equal(15.0, output[1], 1e-10);
        Assert.Equal(20.0, output[2], 1e-10);
        Assert.Equal(30.0, output[3], 1e-10);
        Assert.Equal(40.0, output[4], 1e-10);
    }

    [Fact]
    public void Sma_SpanCalc_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];
        var rng = new Random(42); // nosemgrep
        for (int i = 0; i < source.Length; i++)
            source[i] = rng.NextDouble() * 100; // nosemgrep

        // Warm up
        Sma.Calculate(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        // (allocation is measured by BenchmarkDotNet, not unit tests)
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Sma_SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Sma.Calculate(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Sma_SpanCalc_Period1_ReturnsInput()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Sma.Calculate(source.AsSpan(), output.AsSpan(), 1);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(source[i], output[i], 1e-10);
        }
    }
}
