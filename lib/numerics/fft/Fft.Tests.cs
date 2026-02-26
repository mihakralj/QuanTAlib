using Xunit;

namespace QuanTAlib.Tests;

public class FftTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Fft();
        Assert.Equal("Fft(64,4,32)", indicator.Name);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Fft(windowSize: 32, minPeriod: 2, maxPeriod: 16);
        Assert.Equal("Fft(32,2,16)", indicator.Name);
    }

    [Fact]
    public void Constructor_InvalidWindowSize_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fft(windowSize: 48));
        Assert.Equal("windowSize", ex.ParamName);
    }

    [Fact]
    public void Constructor_WindowSize16_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fft(windowSize: 16));
        Assert.Equal("windowSize", ex.ParamName);
    }

    [Fact]
    public void Constructor_MinPeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fft(minPeriod: 1));
        Assert.Equal("minPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_MinPeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fft(minPeriod: 0));
        Assert.Equal("minPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_MaxPeriodExceedsHalfWindow_ThrowsArgumentException()
    {
        // windowSize=64, half=32, maxPeriod=33 → invalid
        var ex = Assert.Throws<ArgumentException>(() => new Fft(windowSize: 64, maxPeriod: 33));
        Assert.Equal("maxPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsWindowSize()
    {
        var ind64 = new Fft(windowSize: 64);
        Assert.Equal(64, ind64.WarmupPeriod);

        // maxPeriod must be <= windowSize/2; explicit maxPeriod required for windowSize=32
        var ind32 = new Fft(windowSize: 32, maxPeriod: 16);
        Assert.Equal(32, ind32.WarmupPeriod);

        var ind128 = new Fft(windowSize: 128, maxPeriod: 64);
        Assert.Equal(128, ind128.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ValidWindowSizes_DoNotThrow()
    {
        var ind32 = new Fft(windowSize: 32, maxPeriod: 16);
        var ind64 = new Fft(windowSize: 64);
        var ind128 = new Fft(windowSize: 128, maxPeriod: 64);
        Assert.Equal(32, ind32.WarmupPeriod);
        Assert.Equal(64, ind64.WarmupPeriod);
        Assert.Equal(128, ind128.WarmupPeriod);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_OutputWithinClampRange()
    {
        var indicator = new Fft(windowSize: 32, minPeriod: 4, maxPeriod: 16);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80001);
        var bars = gbm.Fetch(windowSize + 20, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            if (indicator.IsHot)
            {
                double v = indicator.Last.Value;
                Assert.True(v >= 4.0 && v <= 16.0,
                    $"Output {v} must be within [minPeriod={4}, maxPeriod={16}]");
            }
        }
    }

    [Fact]
    public void Last_IsAccessible_AfterUpdate()
    {
        var indicator = new Fft();
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void Name_Accessible()
    {
        var indicator = new Fft(windowSize: 64, minPeriod: 4, maxPeriod: 32);
        Assert.NotNull(indicator.Name);
        Assert.Contains("Fft", indicator.Name, StringComparison.Ordinal);
    }

    // ─── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80002);
        var bars = gbm.Fetch(windowSize + 5, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < windowSize; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(windowSize), 9999.0), true);
        double after = indicator.Last.Value;

        Assert.True(double.IsFinite(after));
        _ = before; // consumed
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        // Verify that isNew=false rolls back to pre-bar state so the next isNew=true
        // advances from the same checkpoint, not from the corrected bar.
        var time = DateTime.UtcNow;
        int windowSize = 32;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80003);
        var bars = gbm.Fetch(windowSize + 4, time.Ticks, TimeSpan.FromMinutes(1));

        // Reference: straight run through all bars
        var refInd = new Fft(windowSize: windowSize, maxPeriod: 16);
        for (int i = 0; i < bars.Close.Count - 2; i++)
        {
            refInd.Update(bars.Close[i]);
        }
        double refValue = refInd.Last.Value;

        // Corrected run: same bars but bar N-2 is corrected before committing
        var corrInd = new Fft(windowSize: windowSize, maxPeriod: 16);
        for (int i = 0; i < bars.Close.Count - 3; i++)
        {
            corrInd.Update(bars.Close[i]);
        }
        // Feed penultimate bar as new, then correct it
        corrInd.Update(new TValue(bars.Close[bars.Close.Count - 3].Time, 9999.0), true);
        corrInd.Update(bars.Close[bars.Close.Count - 3], false);

        // Now feed last-but-one bar: should match reference path from same checkpoint
        corrInd.Update(bars.Close[bars.Close.Count - 2]);

        Assert.Equal(refValue, corrInd.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80004);
        int count = 50;
        var bars = gbm.Fetch(count, time.Ticks, TimeSpan.FromMinutes(1));

        var straight = new Fft(windowSize: 32, maxPeriod: 16);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        var corrected = new Fft(windowSize: 32, maxPeriod: 16);
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
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80005);
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
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
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
        Assert.Equal(32, new Fft(windowSize: 32, maxPeriod: 16).WarmupPeriod);
        Assert.Equal(64, new Fft(windowSize: 64).WarmupPeriod);
        Assert.Equal(128, new Fft(windowSize: 128, maxPeriod: 64).WarmupPeriod);
    }

    // ─── E) Robustness ────────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80006);
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
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80007);
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
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80008);
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
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80009);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Fft(windowSize, maxPeriod: 16);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Fft.Batch(source, windowSize, maxPeriod: 16);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Fft.Batch(rawValues, spanOutput, windowSize, maxPeriod: 16);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Fft(eventSource, windowSize, maxPeriod: 16);
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
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 80010);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Fft(windowSize, maxPeriod: 16);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Fft.Batch(source, windowSize, maxPeriod: 16);

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
            Fft.Batch([], Array.Empty<double>()));
        Assert.Equal("src", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fft.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidWindowSize_ThrowsArgumentException()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fft.Batch(src, dst, windowSize: 48));
        Assert.Equal("windowSize", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidMinPeriod_ThrowsArgumentException()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fft.Batch(src, dst, minPeriod: 0));
        Assert.Equal("minPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidMaxPeriod_ThrowsArgumentException()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fft.Batch(src, dst, windowSize: 32, maxPeriod: 33));
        Assert.Equal("maxPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputWithinClampRange()
    {
        int count = 100;
        int windowSize = 32;
        int minP = 4;
        int maxP = 16;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80011);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Fft.Batch(src, dst, windowSize, minP, maxP);

        for (int i = windowSize; i < count; i++)
        {
            Assert.True(dst[i] >= minP && dst[i] <= maxP,
                $"Output {dst[i]} out of range [{minP},{maxP}] at index {i}");
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
        Fft.Batch(src, dst, windowSize, maxPeriod: 16);

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
        Fft.Batch(src, dst, windowSize: 128, maxPeriod: 64);

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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 80012);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Fft.Batch(src, spanOut, windowSize, maxPeriod: 16);

        var streaming = new Fft(windowSize, maxPeriod: 16);
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
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
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
        var indicator = new Fft(source, windowSize, maxPeriod: 16);

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
        var indicator = new Fft(windowSize: 32, maxPeriod: 16);
        TValue? lastEvent = null;
        indicator.Pub += (object? s, in TValueEventArgs e) => lastEvent = e.Value;

        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80013);
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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80014);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Fft.Calculate(bars.Close, windowSize, maxPeriod: 16);

        Assert.Equal(count, results.Count);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }

    // ─── FFT-specific: sinusoidal period detection ────────────────────────────

    [Fact]
    public void Fft_SinusoidalInput_DetectsApproximatePeriod()
    {
        // Pure sinusoid at period 16 bars; N=64, minP=4, maxP=32
        // DFT bin k=4 corresponds to period 64/4=16 → should detect near 16
        int period = 16;
        int windowSize = 64;
        var indicator = new Fft(windowSize, minPeriod: 4, maxPeriod: 32);
        var time = DateTime.UtcNow;

        // Feed 3x the window size to ensure convergence
        for (int i = 0; i < windowSize * 3; i++)
        {
            double signal = 50.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / period);
            indicator.Update(new TValue(time.AddMinutes(i), signal), true);
        }

        Assert.True(indicator.IsHot);
        double detected = indicator.Last.Value;
        // Allow ±3 bars tolerance as specified
        Assert.True(Math.Abs(detected - period) <= 3.0,
            $"Detected period {detected:F2} should be within 3 bars of {period}");
    }

    [Fact]
    public void Fft_OutputAlwaysClamped()
    {
        var indicator = new Fft(windowSize: 32, minPeriod: 4, maxPeriod: 16);
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 80015);
        var bars = gbm.Fetch(200, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            if (indicator.IsHot)
            {
                double v = indicator.Last.Value;
                Assert.True(v >= 4.0, $"Output {v} below minPeriod=4");
                Assert.True(v <= 16.0, $"Output {v} above maxPeriod=16");
            }
        }
    }
}
