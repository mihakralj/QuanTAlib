namespace QuanTAlib.Tests;

public class DecyclerTests
{
    // ============== Bucket A: Constructor Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Decycler(period: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Decycler(period: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Decycler(period: -10));

        var dec = new Decycler(2);
        Assert.NotNull(dec);
    }

    // ============== Bucket B: Basic Calculation ==============

    [Fact]
    public void Properties_AreAccessible()
    {
        var dec = new Decycler(60);
        Assert.Equal(60, dec.Period);
        Assert.StartsWith("Decycler", dec.Name, StringComparison.Ordinal);
        Assert.Equal("Decycler(60)", dec.Name);
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var dec = new Decycler(60);
        var res = dec.Update(new TValue(DateTime.UtcNow, 100));

        // First bar: output = source (HP = 0)
        Assert.Equal(100, res.Value);
        Assert.Equal(100, dec.Last.Value);
    }

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var dec = new Decycler(20);

        dec.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = dec.Last.Value;

        dec.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double value2 = dec.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    // ============== Bucket C: State + Bar Correction ==============

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var dec = new Decycler(20);

        // Feed initial values
        dec.Update(new TValue(DateTime.UtcNow, 100));
        dec.Update(new TValue(DateTime.UtcNow, 110));

        // Committed state after 2 bars
        _ = dec.Last.Value;

        // New bar
        var newVal = dec.Update(new TValue(DateTime.UtcNow, 120), isNew: true).Value;

        // Same bar correction
        var correctedVal = dec.Update(new TValue(DateTime.UtcNow, 125), isNew: false).Value;

        Assert.NotEqual(newVal, correctedVal);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var dec = new Decycler(20);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            dec.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = dec.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            dec.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = dec.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var dec = new Decycler(20);
        dec.Update(new TValue(DateTime.UtcNow, 100));
        dec.Update(new TValue(DateTime.UtcNow, 110));

        dec.Reset();

        // After reset, IsHot should be false (state cleared)
        Assert.False(dec.IsHot);

        // After reset, first value should be source itself (HP = 0 on first bar)
        var res = dec.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50, res.Value);
    }

    [Fact]
    public void Reset_ClearsLastValidValue()
    {
        var dec = new Decycler(20);

        // Feed values including NaN
        dec.Update(new TValue(DateTime.UtcNow, 100));
        dec.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        dec.Reset();

        // After reset, first valid value should establish new baseline
        var result = dec.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ============== Bucket D: Warmup / Convergence ==============

    [Fact]
    public void IsHot_BecomeTrueAfterFirstBar()
    {
        var dec = new Decycler(60);

        // Initially IsHot should be false
        Assert.False(dec.IsHot);

        // After first bar, IsHot should become true (IsInitialized flag)
        dec.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(dec.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsToPeriod()
    {
        var dec20 = new Decycler(20);
        var dec60 = new Decycler(60);
        var dec100 = new Decycler(100);

        Assert.Equal(20, dec20.WarmupPeriod);
        Assert.Equal(60, dec60.WarmupPeriod);
        Assert.Equal(100, dec100.WarmupPeriod);
    }

    // ============== Bucket E: Robustness — NaN + Infinity ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var dec = new Decycler(20);

        // Feed some valid values
        dec.Update(new TValue(DateTime.UtcNow, 100));
        dec.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN — should use last valid value (110)
        var resultAfterNaN = dec.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var dec = new Decycler(20);

        dec.Update(new TValue(DateTime.UtcNow, 100));
        dec.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity
        var resultAfterPosInf = dec.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity
        var resultAfterNegInf = dec.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var dec = new Decycler(20);

        dec.Update(new TValue(DateTime.UtcNow, 100));
        dec.Update(new TValue(DateTime.UtcNow, 110));
        dec.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = dec.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = dec.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = dec.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void StreamingCalc_HandlesNaN_InSeries()
    {
        var dec = new Decycler(10);

        // Streaming Update handles NaN via last-valid substitution
        var r1 = dec.Update(new TValue(DateTime.UtcNow, 100));
        var r2 = dec.Update(new TValue(DateTime.UtcNow, 110));
        var r3 = dec.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r4 = dec.Update(new TValue(DateTime.UtcNow, 120));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
        Assert.True(double.IsFinite(r4.Value));
    }

    // ============== Bucket F: Consistency — All 4 Modes Match ==============

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Decycler.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Decycler.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Decycler(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Decycler(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert — precision 9 due to accumulation differences
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    [Fact]
    public void BatchCalc_MatchesStaticBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 123);

        // Generate data
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        Assert.True(series.Count > 0);

        // Calculate via instance batch
        var dec = new Decycler(20);
        var batchResults = dec.Update(series);

        // Calculate via static Batch(TSeries)
        var staticResults = Decycler.Batch(series, 20);

        // Compare
        Assert.Equal(batchResults.Count, staticResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, staticResults[i].Value, 1e-9);
        }
    }

    // ============== Bucket G: Span API Tests ==============

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] src = new double[10];
        double[] dst = new double[5];
        Assert.Throws<ArgumentException>(() => Decycler.Batch(src, dst, 20));
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        // Period must be >= 2
        Assert.Throws<ArgumentOutOfRangeException>(() => Decycler.Batch(source.AsSpan(), output.AsSpan(), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Decycler.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Decycler.Batch(source.AsSpan(), output.AsSpan(), -5));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var series = new TSeries();
        double[] source = new double[100];
        double[] output = new double[100];

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 456);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(bar.Time, bar.Close);
        }

        // Calculate with TSeries API
        var tseriesResult = Decycler.Batch(series, 20);

        // Calculate with Span API
        Decycler.Batch(source.AsSpan(), output.AsSpan(), 20);

        // Compare
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_ProducesFiniteOutput_ForValidInput()
    {
        double[] source = [100, 110, 105, 120, 130];
        double[] output = new double[5];

        Decycler.Batch(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite for valid input
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }

        // First bar output = source (no HP yet)
        Assert.Equal(100.0, output[0]);
    }

    [Fact]
    public void SpanBatch_LargeData_NoStackOverflow()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Should run without throwing (no stack overflow)
        Decycler.Batch(source.AsSpan(), output.AsSpan(), 60);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoThrow()
    {
        double[] source = [];
        double[] output = [];

        // Should handle empty input gracefully
        Decycler.Batch(source.AsSpan(), output.AsSpan(), 20);

        Assert.Empty(output);
    }

    // ============== Bucket H: Chainability ==============

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var dec = new Decycler(source, 20);

        source.Add(new TValue(DateTime.UtcNow, 100));
        // First bar: output = source
        Assert.Equal(100, dec.Last.Value, 1e-10);
    }

    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var dec = new Decycler(20);
        bool pubFired = false;

        void OnPub(object? sender, in TValueEventArgs args) => pubFired = true;
        dec.Pub += OnPub;

        dec.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(pubFired);
    }

    [Fact]
    public void EventChaining_ProducesResults()
    {
        // Chain: source → decycler1 → decycler2
        var source = new TSeries();
        var dec1 = new Decycler(source, 20);
        var dec2 = new Decycler(dec1, 30);

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 789);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Both indicators should have processed data
        Assert.True(double.IsFinite(dec1.Last.Value));
        Assert.True(double.IsFinite(dec2.Last.Value));
        Assert.NotEqual(0, dec1.Last.Value);
        Assert.NotEqual(0, dec2.Last.Value);
    }

    [Fact]
    public void Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 20; i++)
        {
            series.Add(DateTime.UtcNow, i * 10);
        }

        var (results, indicator) = Decycler.Calculate(series, 10);

        // Check results
        Assert.Equal(20, results.Count);

        // Verify against standard calculation
        var verifyDec = new Decycler(10);
        var verifyResults = verifyDec.Update(series);

        Assert.Equal(verifyResults.Last.Value, results.Last.Value, 1e-10);
        Assert.Equal(verifyDec.Last.Value, indicator.Last.Value, 1e-10);

        // Check indicator state
        Assert.True(indicator.IsHot);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 210));
        verifyDec.Update(new TValue(DateTime.UtcNow, 210));
        Assert.Equal(verifyDec.Last.Value, indicator.Last.Value, 1e-10);
    }
}
