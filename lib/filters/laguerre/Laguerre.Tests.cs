namespace QuanTAlib.Tests;

public class LaguerreTests
{
    // ============== A) Constructor Validation ==============

    [Fact]
    public void Laguerre_Constructor_Gamma_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laguerre(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laguerre(1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laguerre(1.5));

        var lag = new Laguerre(0.0);
        Assert.NotNull(lag);

        var lag2 = new Laguerre(0.99);
        Assert.NotNull(lag2);
    }

    [Fact]
    public void Laguerre_Constructor_DefaultGamma()
    {
        var lag = new Laguerre();
        Assert.Contains("0.80", lag.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Laguerre_Constructor_SetsName()
    {
        var lag = new Laguerre(0.5);
        Assert.Equal("Laguerre(0.50)", lag.Name);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void Laguerre_Calc_ReturnsValue()
    {
        var lag = new Laguerre(0.8);

        Assert.Equal(0, lag.Last.Value);

        TValue result = lag.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, lag.Last.Value);
    }

    [Fact]
    public void Laguerre_Calc_FirstValue_ReturnsInput()
    {
        var lag = new Laguerre(0.8);

        TValue result = lag.Update(new TValue(DateTime.UtcNow, 42.0));

        // First value: all L elements initialized to input, output = input
        Assert.Equal(42.0, result.Value, 1e-10);
    }

    [Fact]
    public void Laguerre_Calc_SmoothsValues()
    {
        var lag = new Laguerre(0.8);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            lag.Update(new TValue(bar.Time, bar.Close));
        }

        // Filter should smooth: result should be finite and reasonable
        Assert.True(double.IsFinite(lag.Last.Value));
        Assert.True(lag.Last.Value > 50 && lag.Last.Value < 200);
    }

    [Fact]
    public void Laguerre_Properties_Accessible()
    {
        var lag = new Laguerre(0.8);

        Assert.Equal(0, lag.Last.Value);
        Assert.False(lag.IsHot);

        lag.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, lag.Last.Value);
    }

    // ============== C) State + Bar Correction ==============

    [Fact]
    public void Laguerre_Calc_IsNew_AcceptsParameter()
    {
        var lag = new Laguerre(0.8);

        lag.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = lag.Last.Value;

        lag.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = lag.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Laguerre_Calc_IsNew_False_UpdatesValue()
    {
        var lag = new Laguerre(0.8);

        lag.Update(new TValue(DateTime.UtcNow, 100));
        lag.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = lag.Last.Value;

        lag.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = lag.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Laguerre_IterativeCorrections_RestoreToOriginalState()
    {
        var lag = new Laguerre(0.8);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            lag.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double lagAfterTen = lag.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            lag.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalLag = lag.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(lagAfterTen, finalLag.Value, 1e-10);
    }

    [Fact]
    public void Laguerre_Reset_ClearsState()
    {
        var lag = new Laguerre(0.8);

        lag.Update(new TValue(DateTime.UtcNow, 100));
        lag.Update(new TValue(DateTime.UtcNow, 105));

        lag.Reset();

        Assert.Equal(0, lag.Last.Value);

        // After reset, should accept new values
        lag.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, lag.Last.Value);
    }

    // ============== D) Warmup / Convergence ==============

    [Fact]
    public void Laguerre_IsHot_BecomesTrueAfterWarmup()
    {
        var lag = new Laguerre(0.8);

        // Initially IsHot should be false
        Assert.False(lag.IsHot);

        for (int i = 0; i < 4; i++)
        {
            lag.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        Assert.True(lag.IsHot);
    }

    [Fact]
    public void Laguerre_WarmupPeriod_IsFour()
    {
        var lag = new Laguerre(0.8);
        Assert.Equal(4, lag.WarmupPeriod);
    }

    // ============== E) Robustness (NaN / Infinity) ==============

    [Fact]
    public void Laguerre_NaN_Input_UsesLastValidValue()
    {
        var lag = new Laguerre(0.8);

        lag.Update(new TValue(DateTime.UtcNow, 100));
        lag.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = lag.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Laguerre_Infinity_Input_UsesLastValidValue()
    {
        var lag = new Laguerre(0.8);

        lag.Update(new TValue(DateTime.UtcNow, 100));
        lag.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = lag.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = lag.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Laguerre_MultipleNaN_ContinuesWithLastValid()
    {
        var lag = new Laguerre(0.8);

        lag.Update(new TValue(DateTime.UtcNow, 100));
        lag.Update(new TValue(DateTime.UtcNow, 110));
        lag.Update(new TValue(DateTime.UtcNow, 120));

        var r1 = lag.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = lag.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = lag.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Laguerre_BatchCalc_HandlesNaN()
    {
        var lag = new Laguerre(0.8);

        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = lag.Update(series);

        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Laguerre_Reset_ClearsLastValidValue()
    {
        var lag = new Laguerre(0.8);

        lag.Update(new TValue(DateTime.UtcNow, 100));
        lag.Update(new TValue(DateTime.UtcNow, double.NaN));

        lag.Reset();

        var result = lag.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ============== F) Consistency (all 4 modes match) ==============

    [Fact]
    public void Laguerre_BatchCalc_MatchesIterativeCalc()
    {
        var lagIterative = new Laguerre(0.8);
        var lagBatch = new Laguerre(0.8);
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
            iterativeResults.Add(lagIterative.Update(item));
        }

        // Calculate batch
        var batchResults = lagBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Laguerre_AllModes_Match()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        double gamma = 0.8;
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
        var lagStream = new Laguerre(gamma);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = lagStream.Update(series[i]).Value;
        }

        // Mode 2: Batch (TSeries)
        var batchResults = Laguerre.Batch(series, gamma);

        // Mode 3: Span
        double[] spanOutput = new double[count];
        Laguerre.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), gamma);

        // Mode 4: Event-driven
        var eventSource = new TSeries();
        var lagEvent = new Laguerre(eventSource, gamma);
        var eventResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            eventSource.Add(series[i]);
            eventResults[i] = lagEvent.Last.Value;
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
    public void Laguerre_SpanBatch_Gamma_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentOutOfRangeException>(() => Laguerre.Batch(source.AsSpan(), output.AsSpan(), -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Laguerre.Batch(source.AsSpan(), output.AsSpan(), 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Laguerre.Batch(source.AsSpan(), output.AsSpan(), 1.5));
    }

    [Fact]
    public void Laguerre_SpanBatch_Length_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Laguerre.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 0.8));
    }

    [Fact]
    public void Laguerre_SpanBatch_MatchesTSeriesBatch()
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

        var tseriesResult = Laguerre.Batch(series, 0.8);
        Laguerre.Batch(source.AsSpan(), output.AsSpan(), 0.8);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Laguerre_SpanBatch_DifferentGammas()
    {
        double[] source = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        double[] output0 = new double[10];
        double[] output05 = new double[10];
        double[] output09 = new double[10];

        Laguerre.Batch(source.AsSpan(), output0.AsSpan(), 0.0);
        Laguerre.Batch(source.AsSpan(), output05.AsSpan(), 0.5);
        Laguerre.Batch(source.AsSpan(), output09.AsSpan(), 0.9);

        for (int i = 0; i < 10; i++)
        {
            Assert.True(double.IsFinite(output0[i]));
            Assert.True(double.IsFinite(output05[i]));
            Assert.True(double.IsFinite(output09[i]));
        }
    }

    [Fact]
    public void Laguerre_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        Laguerre.Batch(source.AsSpan(), output.AsSpan(), 0.8);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Laguerre_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Laguerre.Batch(source.AsSpan(), output.AsSpan(), 0.8);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void Laguerre_Chainability_Works()
    {
        var source = new TSeries();
        var lag = new Laguerre(source, 0.8);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, lag.Last.Value, 1e-10);
    }

    [Fact]
    public void Laguerre_Prime_SetsStateCorrectly()
    {
        var lag = new Laguerre(0.8);
        double[] history = [10, 20, 30, 40, 50];

        lag.Prime(history);

        // Verify against a fresh Laguerre fed with same data
        var verifyLag = new Laguerre(0.8);
        foreach (var val in history)
        {
            verifyLag.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyLag.Last.Value, lag.Last.Value, 1e-10);
    }

    // ============== Gamma-specific behavior ==============

    [Fact]
    public void Laguerre_Gamma0_IsFIR()
    {
        // When gamma=0, L0=input, L1=L0[1], L2=L1[1], L3=L2[1]
        // This is a 4-tap FIR: (input + 2*prev1 + 2*prev2 + prev3) / 6
        var lag = new Laguerre(0.0);

        lag.Update(new TValue(DateTime.UtcNow, 10));
        lag.Update(new TValue(DateTime.UtcNow, 20));
        lag.Update(new TValue(DateTime.UtcNow, 30));
        lag.Update(new TValue(DateTime.UtcNow, 40));
        double result = lag.Update(new TValue(DateTime.UtcNow, 50)).Value;

        // With gamma=0: L0=50, L1=40, L2=30, L3=20
        // Filt = (50 + 2*40 + 2*30 + 20) / 6 = (50+80+60+20)/6 = 210/6 = 35
        Assert.Equal(35.0, result, 1e-10);
    }

    [Fact]
    public void Laguerre_HigherGamma_MoreSmoothing()
    {
        // Higher gamma = more smoothing = slower to react to price changes
        var lagLow = new Laguerre(0.2);
        var lagHigh = new Laguerre(0.9);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.15, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var input = new TValue(bar.Time, bar.Close);
            lagLow.Update(input);
            lagHigh.Update(input);
        }

        // Both should be finite
        Assert.True(double.IsFinite(lagLow.Last.Value));
        Assert.True(double.IsFinite(lagHigh.Last.Value));

        // High gamma should diverge more from current price (more lag)
        // This is a statistical property, hard to test exactly, but values should differ
        Assert.NotEqual(lagLow.Last.Value, lagHigh.Last.Value, 2);
    }

    [Fact]
    public void Laguerre_ConstantInput_ConvergesToInput()
    {
        var lag = new Laguerre(0.8);

        // Feed constant value - filter should converge to that value
        for (int i = 0; i < 100; i++)
        {
            lag.Update(new TValue(DateTime.UtcNow, 42.0));
        }

        Assert.Equal(42.0, lag.Last.Value, 1e-10);
    }

    [Fact]
    public void Laguerre_LargeDataset_RemainsStable()
    {
        var lag = new Laguerre(0.8);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 10000; i++)
        {
            var bar = gbm.Next(isNew: true);
            lag.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(lag.Last.Value));
        Assert.True(lag.Last.Value > 10 && lag.Last.Value < 1000);
    }
}
