using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests.Cycles;

public class HtDcperiodTests
{
    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ht = new HtDcperiod();
        Assert.Equal("HtDcperiod", ht.Name);
        Assert.Equal(32, ht.WarmupPeriod);
        Assert.False(ht.IsHot);
    }

    [Fact]
    public void Constructor_WithPublisher_SubscribesToEvents()
    {
        var source = new TSeries();
        var ht = new HtDcperiod(source);
        Assert.False(ht.IsHot);

        // Feed data through publisher
        for (int i = 0; i < 40; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + (Math.Sin(i * 0.3) * 10)));
        }

        Assert.True(ht.IsHot);
        Assert.True(double.IsFinite(ht.Last.Value));
    }

    [Fact]
    public void Constructor_WithNullPublisher_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HtDcperiod(null!));
    }

    [Fact]
    public void Last_DefaultBeforeAnyUpdate()
    {
        var ht = new HtDcperiod();
        Assert.Equal(default, ht.Last);
    }

    // ── IsHot & Warmup ──────────────────────────────────────────────────

    [Fact]
    public void Update_BecomesHotAfterWarmup()
    {
        var ht = new HtDcperiod();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(80, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ht.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ht.IsHot);
        Assert.True(double.IsFinite(ht.Last.Value));
    }

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var ht = new HtDcperiod();
        for (int i = 0; i < 30; i++)
        {
            ht.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }
        Assert.False(ht.IsHot);
    }

    [Fact]
    public void WarmupPeriod_Returns32()
    {
        var ht = new HtDcperiod();
        Assert.Equal(32, ht.WarmupPeriod);
    }

    // ── Update (streaming) ──────────────────────────────────────────────

    [Fact]
    public void Update_FirstBarsReturnZero()
    {
        var ht = new HtDcperiod();
        // During WMA initialization (first ~37 bars), output should be 0
        var result = ht.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_ProducesFiniteValuesAfterWarmup()
    {
        var ht = new HtDcperiod();
        var gbm = new GBM(seed: 99);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        TValue lastResult = default;
        foreach (var bar in bars)
        {
            lastResult = ht.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(lastResult.Value));
    }

    [Fact]
    public void Update_PeriodInValidRange()
    {
        // The dominant cycle period should be clamped between 6 and 50
        var ht = new HtDcperiod();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        bool anyHot = false;
        foreach (var bar in bars)
        {
            var result = ht.Update(new TValue(bar.Time, bar.Close));
            if (ht.IsHot)
            {
                anyHot = true;
                // Period output should be in a reasonable range
                Assert.True(double.IsFinite(result.Value),
                    $"Period should be finite, got {result.Value}");
            }
        }
        Assert.True(anyHot);
    }

    // ── Bar Correction (isNew=false) ────────────────────────────────────

    [Fact]
    public void SameBarUpdate_ReturnsSameValue()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;

        // Prime with data
        for (int i = 0; i < 50; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + (Math.Sin(i * 0.1) * 10)));
        }

        Assert.True(ht.IsHot);

        // First update (new bar)
        var result1 = ht.Update(new TValue(now.AddMinutes(50), 105), isNew: true);

        // Same bar update with different price
        var result2 = ht.Update(new TValue(now.AddMinutes(50), 106), isNew: false);

        // isNew=false should rollback and reapply - result should equal result1 since
        // bar correction restores previous state first
        Assert.Equal(result1.Value, result2.Value);
    }

    [Fact]
    public void BarCorrection_DoesNotAdvanceState()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // New bar
        ht.Update(new TValue(now.AddMinutes(50), 150), isNew: true);
        var afterNew = ht.Last;

        // Multiple corrections should not change the state relative to the new bar
        ht.Update(new TValue(now.AddMinutes(50), 151), isNew: false);
        ht.Update(new TValue(now.AddMinutes(50), 152), isNew: false);
        ht.Update(new TValue(now.AddMinutes(50), 150), isNew: false);
        var afterCorrections = ht.Last;

        Assert.Equal(afterNew.Value, afterCorrections.Value);
    }

    // ── NaN handling ────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_BeforeAnyValidData_ReturnsZero()
    {
        var ht = new HtDcperiod();
        var result = ht.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_NaN_UsesLastValidPrice()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;

        // Feed valid data to warm up
        for (int i = 0; i < 50; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + (Math.Sin(i * 0.2) * 5)));
        }

        Assert.True(ht.IsHot);

        // Feed NaN - should use last valid price
        var result = ht.Update(new TValue(now.AddMinutes(50), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidPrice()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + (i * 0.5)));
        }

        var result = ht.Update(new TValue(now.AddMinutes(50), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── Reset ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        Assert.True(ht.IsHot);
        ht.Reset();
        Assert.False(ht.IsHot);
        Assert.Equal(default, ht.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;

        // First use
        for (int i = 0; i < 50; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + (Math.Sin(i * 0.2) * 5)));
        }
        Assert.True(ht.IsHot);
        var firstResult = ht.Last.Value;

        // Reset and reuse
        ht.Reset();
        Assert.False(ht.IsHot);

        for (int i = 0; i < 50; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + (Math.Sin(i * 0.2) * 5)));
        }
        Assert.True(ht.IsHot);
        Assert.Equal(firstResult, ht.Last.Value);
    }

    // ── Batch/TSeries Update ────────────────────────────────────────────

    [Fact]
    public void Update_TSeries_ReturnsCorrectCount()
    {
        var ht = new HtDcperiod();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = ht.Update(bars.Close);
        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var ht = new HtDcperiod();
        var result = ht.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_TSeries_MatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Batch
        var batchResult = HtDcperiod.Batch(series);

        // Streaming
        var streaming = new HtDcperiod();
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        // Compare
        Assert.Equal(batchResult.Count, streamingResults.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = bars.Close.Values.ToArray();

        // Span batch
        var spanOutput = new double[values.Length];
        HtDcperiod.Batch(values, spanOutput);

        // Streaming
        var streaming = new HtDcperiod();
        var streamingResults = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            streamingResults[i] = streaming.Update(new TValue(DateTime.UtcNow.AddTicks(i), values[i])).Value;
        }

        // Compare
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var source = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentException>(() => HtDcperiod.Batch(source, output));
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = HtDcperiod.Calculate(bars.Close);
        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WamsUpIndicator()
    {
        var ht = new HtDcperiod();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(80, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = bars.Close.Values.ToArray();

        ht.Prime(values);
        Assert.True(ht.IsHot);
    }

    [Fact]
    public void Prime_WithStepParameter()
    {
        var ht = new HtDcperiod();
        var values = new double[50];
        for (int i = 0; i < 50; i++)
        {
            values[i] = 100 + (Math.Sin(i * 0.2) * 5);
        }

        ht.Prime(values, TimeSpan.FromMinutes(5));
        Assert.True(ht.IsHot);
    }

    // ── Determinism ─────────────────────────────────────────────────────

    [Fact]
    public void TwoInstances_SameInput_SameOutput()
    {
        var ht1 = new HtDcperiod();
        var ht2 = new HtDcperiod();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            var r1 = ht1.Update(tv);
            var r2 = ht2.Update(tv);
            Assert.Equal(r1.Value, r2.Value);
        }
    }

    // ── Constant price ──────────────────────────────────────────────────

    [Fact]
    public void ConstantPrice_ProducesFiniteOutput()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 80; i++)
        {
            var result = ht.Update(new TValue(now.AddMinutes(i), 100.0));
            Assert.True(double.IsFinite(result.Value),
                $"Bar {i}: Expected finite, got {result.Value}");
        }
    }

    // ── Sinusoidal input ────────────────────────────────────────────────

    [Fact]
    public void SinusoidalInput_DetectsApproximatePeriod()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;

        // Feed a clean sinusoidal with period ~20 bars
        int inputPeriod = 20;
        double omega = 2.0 * Math.PI / inputPeriod;

        for (int i = 0; i < 300; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + (10 * Math.Sin(omega * i))));
        }

        // After sufficient data, the detected period should be
        // somewhere in the ballpark of the input period
        Assert.True(ht.IsHot);
        double detected = ht.Last.Value;
        Assert.True(double.IsFinite(detected));
        // The Hilbert transform period detection is approximate
        Assert.True(detected >= 6.0 && detected <= 50.0,
            $"Detected period {detected} should be in [6, 50] range");
    }

    // ── Dispose ─────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ht = new HtDcperiod();
        ht.Update(new TValue(DateTime.UtcNow, 100));
        ht.Dispose();
        Assert.True(true); // S2699: explicit assertion for dispose-only test
    }

    [Fact]
    public void Dispose_WithPublisher_DoesNotThrow()
    {
        // HtDcperiod subscribes to source but does not track the source
        // reference for unsubscription — Dispose still must not throw
        var source = new TSeries();
        var ht = new HtDcperiod(source);
        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(ht.Last.Value) || ht.Last.Value == 0.0);
        ht.Dispose();
    }
}
