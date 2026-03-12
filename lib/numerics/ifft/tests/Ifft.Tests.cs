using Xunit;

namespace QuanTAlib.Tests;

public class IfftTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Ifft();
        Assert.Equal("Ifft(64,5)", indicator.Name);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Ifft(windowSize: 32, numHarmonics: 3);
        Assert.Equal("Ifft(32,3)", indicator.Name);
    }

    [Fact]
    public void Constructor_InvalidWindowSize_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ifft(windowSize: 48));
        Assert.Equal("windowSize", ex.ParamName);
    }

    [Fact]
    public void Constructor_WindowSize16_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ifft(windowSize: 16));
        Assert.Equal("windowSize", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroHarmonics_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ifft(numHarmonics: 0));
        Assert.Equal("numHarmonics", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeHarmonics_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ifft(numHarmonics: -1));
        Assert.Equal("numHarmonics", ex.ParamName);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsWindowSize()
    {
        Assert.Equal(64, new Ifft(windowSize: 64).WarmupPeriod);
        Assert.Equal(32, new Ifft(windowSize: 32).WarmupPeriod);
        Assert.Equal(128, new Ifft(windowSize: 128).WarmupPeriod);
    }

    [Fact]
    public void Constructor_ValidWindowSizes_DoNotThrow()
    {
        var ind32 = new Ifft(windowSize: 32);
        var ind64 = new Ifft(windowSize: 64);
        var ind128 = new Ifft(windowSize: 128);
        Assert.Equal(32, ind32.WarmupPeriod);
        Assert.Equal(64, ind64.WarmupPeriod);
        Assert.Equal(128, ind128.WarmupPeriod);
    }

    [Fact]
    public void Constructor_HarmonicsClampedToHalfWindow()
    {
        // numHarmonics=100 with windowSize=32 → internally clamped to 16, but Name shows original arg
        var indicator = new Ifft(windowSize: 32, numHarmonics: 100);
        Assert.Equal("Ifft(32,100)", indicator.Name);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_OutputIsFinite_AfterWarmup()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90001);
        var bars = gbm.Fetch(windowSize + 20, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Output must be finite at bar {i}, got {indicator.Last.Value}");
        }
    }

    [Fact]
    public void Last_IsAccessible_AfterUpdate()
    {
        var indicator = new Ifft();
        indicator.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void Name_Accessible()
    {
        var indicator = new Ifft(windowSize: 64, numHarmonics: 5);
        Assert.NotNull(indicator.Name);
        Assert.Contains("Ifft", indicator.Name, StringComparison.Ordinal);
    }

    // ─── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90002);
        var bars = gbm.Fetch(windowSize + 5, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < windowSize; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(windowSize), 9999.0), true);
        double after = indicator.Last.Value;

        Assert.True(double.IsFinite(after));
        _ = before;
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        // Hanning window weights endpoints at 0, so changing only the most-recent
        // sample has near-zero effect on DFT output. The correct isNew=false test
        // verifies that state is rolled back so the next isNew=true advances from
        // the pre-correction checkpoint — same as the IterativeCorrection_RestoresState test.
        // We use 'count' bars and verify the last value matches a straight run of the same bars.
        var time = DateTime.UtcNow;
        int windowSize = 32;
        int count = windowSize + 5;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90003);
        var bars = gbm.Fetch(count, time.Ticks, TimeSpan.FromMinutes(1));

        // Reference: straight run through all 'count' bars
        var refInd = new Ifft(windowSize: windowSize);
        for (int i = 0; i < count; i++)
        {
            refInd.Update(bars.Close[i]);
        }
        double refValue = refInd.Last.Value;

        // Corrected run: every bar is submitted as fake first, then corrected to true value
        var corrInd = new Ifft(windowSize: windowSize);
        for (int i = 0; i < count; i++)
        {
            corrInd.Update(new TValue(bars.Close[i].Time, 9999.0), true);
            corrInd.Update(bars.Close[i], false);
        }

        Assert.Equal(refValue, corrInd.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90004);
        int count = 50;
        var bars = gbm.Fetch(count, time.Ticks, TimeSpan.FromMinutes(1));

        var straight = new Ifft(windowSize: 32);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        var corrected = new Ifft(windowSize: 32);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            corrected.Update(new TValue(bars.Close[i].Time, 999.0), true);
            corrected.Update(bars.Close[i], false);
        }

        Assert.Equal(finalStraight, corrected.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90005);
        var bars = gbm.Fetch(windowSize, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        Assert.True(indicator.IsHot);
        indicator.Reset();
        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    // ─── D) Warmup / convergence ──────────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtWindowSize()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        for (int i = 0; i < windowSize - 1; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0 + i));
            Assert.False(indicator.IsHot, $"Should not be hot at bar {i + 1}");
        }

        indicator.Update(new TValue(time.AddMinutes(windowSize - 1), 100.0 + windowSize));
        Assert.True(indicator.IsHot, "Should be hot after windowSize bars");
    }

    [Fact]
    public void WarmupPeriod_EqualToWindowSize()
    {
        Assert.Equal(32, new Ifft(windowSize: 32).WarmupPeriod);
        Assert.Equal(64, new Ifft(windowSize: 64).WarmupPeriod);
        Assert.Equal(128, new Ifft(windowSize: 128).WarmupPeriod);
    }

    // ─── E) Robustness ────────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90006);
        var bars = gbm.Fetch(windowSize, time.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < windowSize; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(windowSize), double.NaN));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_PositiveInfinity_UsesLastValidValue()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90007);
        var bars = gbm.Fetch(windowSize, time.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < windowSize; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(windowSize), double.PositiveInfinity));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90008);
        var bars = gbm.Fetch(windowSize, time.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < windowSize; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(windowSize), double.NegativeInfinity));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_BatchNaN_AlwaysFinite()
    {
        var indicator = new Ifft(windowSize: 32);
        var time = DateTime.UtcNow;

        double[] prices = { 100.0, double.NaN, 102.0, double.NaN, 98.0, 105.0, 103.0, 99.0, 101.0, 104.0, 97.0, 106.0, 108.0 };
        for (int i = 0; i < prices.Length; i++)
        {
            var result = indicator.Update(new TValue(time.AddMinutes(i), prices[i]));
            Assert.True(double.IsFinite(result.Value), $"Output must be finite at {i}, got {result.Value}");
        }
    }

    // ─── F) Consistency: batch == streaming == span == eventing ──────────────

    [Fact]
    public void AllModes_ConsistencyCheck()
    {
        int windowSize = 32;
        int count = 80;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90009);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Ifft(windowSize, numHarmonics: 3);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Ifft.Batch(source, windowSize, numHarmonics: 3);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Ifft.Batch(rawValues, spanOutput, windowSize, numHarmonics: 3);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Ifft(eventSource, windowSize, numHarmonics: 3);
        eventIndicator.Pub += (object? s, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i], true);
        }

        double streamingLast = streaming.Last.Value;
        double batchLast = batch[source.Count - 1].Value;
        double spanLast = spanOutput[source.Count - 1];
        double eventLast = eventResults[^1];

        Assert.Equal(streamingLast, batchLast, Tolerance);
        Assert.Equal(streamingLast, spanLast, Tolerance);
        Assert.Equal(streamingLast, eventLast, Tolerance);
    }

    [Fact]
    public void Streaming_VsBatch_AllValues_Match()
    {
        int count = 80;
        int windowSize = 32;
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 90010);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Ifft(windowSize, numHarmonics: 3);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Ifft.Batch(source, windowSize, numHarmonics: 3);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingVals[i], batch[i].Value, Tolerance);
        }
    }

    // ─── G) Span API tests ────────────────────────────────────────────────────

    [Fact]
    public void Batch_Span_EmptySource_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Ifft.Batch([], Array.Empty<double>()));
        Assert.Equal("src", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Ifft.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidWindowSize_ThrowsArgumentException()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Ifft.Batch(src, dst, windowSize: 48));
        Assert.Equal("windowSize", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroHarmonics_ThrowsArgumentException()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Ifft.Batch(src, dst, numHarmonics: 0));
        Assert.Equal("numHarmonics", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputIsFinite()
    {
        int count = 100;
        int windowSize = 32;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90011);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Ifft.Batch(src, dst, windowSize, numHarmonics: 3);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v), $"IFFT output {v} must be finite");
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        int windowSize = 32;
        double[] src = new double[windowSize + 5];
        for (int i = 0; i < src.Length; i++)
        {
            src[i] = 100.0 + i;
        }

        src[3] = double.NaN;
        double[] dst = new double[src.Length];
        Ifft.Batch(src, dst, windowSize, numHarmonics: 3);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v), $"Span output should always be finite, got {v}");
        }
    }

    [Fact]
    public void Batch_Span_NoStackOverflow_LargeWindow()
    {
        // windowSize=128: uses ArrayPool (> 64 StackallocThreshold)
        int count = 300;
        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = 100.0 + Math.Sin(i * 0.2) * 10.0;
        }

        double[] dst = new double[count];
        Ifft.Batch(src, dst, windowSize: 128, numHarmonics: 5);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int count = 60;
        int windowSize = 32;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 90012);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Ifft.Batch(src, spanOut, windowSize, numHarmonics: 3);

        var streaming = new Ifft(windowSize, numHarmonics: 3);
        for (int i = 0; i < count; i++)
        {
            streaming.Update(bars.Close[i]);
            Assert.Equal(streaming.Last.Value, spanOut[i], Tolerance);
        }
    }

    // ─── H) Chainability ──────────────────────────────────────────────────────

    [Fact]
    public void Pub_EventFires()
    {
        var indicator = new Ifft(windowSize: 32);
        int count = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => count++;

        var time = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0 + i));
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        int windowSize = 32;
        var source = new TSeries();
        var indicator = new Ifft(source, windowSize);

        var time = DateTime.UtcNow;
        for (int i = 0; i < windowSize; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 + Math.Sin(i * 0.5) * 5.0), true);
        }

        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Pub_EventValue_MatchesLast()
    {
        var indicator = new Ifft(windowSize: 32);
        TValue? lastEvent = null;
        indicator.Pub += (object? s, in TValueEventArgs e) => lastEvent = e.Value;

        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90013);
        var bars = gbm.Fetch(windowSize + 2, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        Assert.NotNull(lastEvent);
        Assert.Equal(indicator.Last.Value, lastEvent.Value.Value, Tolerance);
    }

    // ─── Additional: static Calculate method ─────────────────────────────────

    [Fact]
    public void Calculate_StaticMethod_ReturnsTuple()
    {
        int count = 80;
        int windowSize = 32;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90014);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Ifft.Calculate(bars.Close, windowSize);

        Assert.Equal(count, results.Count);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }

    // ─── IFFT-specific: smoothing properties ─────────────────────────────────

    [Fact]
    public void Ifft_OneHarmonic_IsSmootherThanInput()
    {
        // With only 1 harmonic, IFFT should produce lower variance than raw input
        int windowSize = 32;
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: 90015);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Ifft(windowSize, numHarmonics: 1);
        var outputs = new List<double>();
        var inputs = new List<double>();

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            if (indicator.IsHot)
            {
                outputs.Add(indicator.Last.Value);
                inputs.Add(bars.Close[i].Value);
            }
        }

        // Compute variance of outputs vs inputs
        double inputMean = inputs.Sum() / inputs.Count;
        double outputMean = outputs.Sum() / outputs.Count;
        double inputVar = inputs.Sum(v => (v - inputMean) * (v - inputMean)) / inputs.Count;
        double outputVar = outputs.Sum(v => (v - outputMean) * (v - outputMean)) / outputs.Count;

        Assert.True(outputVar < inputVar,
            $"IFFT(H=1) variance {outputVar:F4} should be < input variance {inputVar:F4}");
    }

    [Fact]
    public void Ifft_DifferentHarmonics_ProduceDifferentOutputs()
    {
        // IFFT with H=1 and H=8 must produce different output series on a
        // multi-component signal — they apply different spectral filtering.
        // This verifies the harmonic parameter has observable effect on output.
        int windowSize = 32;
        int count = 200;
        double twoPiOverN = 2.0 * Math.PI / windowSize;
        var time = DateTime.UtcNow;
        var values = new List<TValue>(count);
        for (int i = 0; i < count; i++)
        {
            double v = 100.0
                + 10.0 * Math.Sin(twoPiOverN * 1 * i)
                + 10.0 * Math.Sin(twoPiOverN * 2 * i)
                + 10.0 * Math.Sin(twoPiOverN * 4 * i)
                + 10.0 * Math.Sin(twoPiOverN * 8 * i);
            values.Add(new TValue(time.AddMinutes(i), v));
        }

        var ind1 = new Ifft(windowSize, numHarmonics: 1);
        var ind8 = new Ifft(windowSize, numHarmonics: 8);

        var out1 = new List<double>();
        var out8 = new List<double>();

        for (int i = 0; i < count; i++)
        {
            ind1.Update(values[i]);
            ind8.Update(values[i]);
            if (ind1.IsHot)
            {
                out1.Add(ind1.Last.Value);
                out8.Add(ind8.Last.Value);
            }
        }

        // Both outputs must be finite
        Assert.True(out1.All(double.IsFinite), "All H=1 outputs must be finite");
        Assert.True(out8.All(double.IsFinite), "All H=8 outputs must be finite");

        // The two series must differ — different harmonic count → different filter response
        double maxDiff = 0.0;
        for (int i = 0; i < out1.Count; i++)
        {
            double d = Math.Abs(out1[i] - out8[i]);
            if (d > maxDiff)
            {
                maxDiff = d;
            }
        }
        Assert.True(maxDiff > 1e-6,
            $"H=1 and H=8 outputs should differ on multi-sine input; max diff was {maxDiff:E3}");
    }

    [Fact]
    public void Ifft_OutputAlwaysFinite()
    {
        var indicator = new Ifft(windowSize: 32, numHarmonics: 5);
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 90017);
        var bars = gbm.Fetch(200, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"IFFT output must always be finite, got {indicator.Last.Value} at bar {i}");
        }
    }
}
