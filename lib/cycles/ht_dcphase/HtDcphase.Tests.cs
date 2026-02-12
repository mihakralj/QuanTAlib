using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests.Cycles;

public class HtDcphaseTests
{
    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ht = new HtDcphase();
        Assert.Equal("HtDcphase", ht.Name);
        Assert.Equal(63, ht.WarmupPeriod);
        Assert.False(ht.IsHot);
    }

    [Fact]
    public void Constructor_WithPublisher_Subscribes()
    {
        var source = new TSeries();
        var ht = new HtDcphase(source);
        Assert.False(ht.IsHot);

        // Feed data through publisher
        for (int i = 0; i < 80; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + Math.Sin(i * 0.3) * 10));
        }

        Assert.True(ht.IsHot);
        Assert.True(double.IsFinite(ht.Last.Value));
    }

    [Fact]
    public void Constructor_NullPublisher_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HtDcphase(null!));
    }

    [Fact]
    public void Last_DefaultBeforeAnyUpdate()
    {
        var ht = new HtDcphase();
        Assert.Equal(default, ht.Last);
    }

    // ── IsHot & Warmup ──────────────────────────────────────────────────

    [Fact]
    public void Update_BecomesHotAfterWarmup()
    {
        var ht = new HtDcphase();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

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
        var ht = new HtDcphase();
        for (int i = 0; i < 60; i++)
        {
            ht.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }
        Assert.False(ht.IsHot);
    }

    // ── Update ──────────────────────────────────────────────────────────

    [Fact]
    public void Update_FirstBarsReturnZero()
    {
        var ht = new HtDcphase();
        var result = ht.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void PhaseRange_IsValid()
    {
        var ht = new HtDcphase();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ht.Update(new TValue(bar.Time, bar.Close));
        }

        // After warmup, phase should be in valid range
        double phase = ht.Last.Value;
        Assert.True(phase >= -45.0 && phase <= 315.0,
            $"Phase {phase} should be in range [-45, 315]");
    }

    [Fact]
    public void Update_ProducesFiniteValuesAfterWarmup()
    {
        var ht = new HtDcphase();
        var gbm = new GBM(seed: 99);
        var bars = gbm.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ht.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ht.IsHot);
        Assert.True(double.IsFinite(ht.Last.Value));
    }

    // ── Bar Correction ──────────────────────────────────────────────────

    [Fact]
    public void SameBarUpdate_ReturnsSameValue()
    {
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;

        // Prime with data
        for (int i = 0; i < 70; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + Math.Sin(i * 0.1) * 10));
        }

        Assert.True(ht.IsHot);

        // First update (new bar)
        var result1 = ht.Update(new TValue(now.AddMinutes(70), 105), isNew: true);

        // Same bar update
        var result2 = ht.Update(new TValue(now.AddMinutes(70), 106), isNew: false);

        Assert.Equal(result1.Value, result2.Value);
    }

    [Fact]
    public void BarCorrection_MultipleCorrections_StableState()
    {
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 70; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + i * 0.5));
        }

        // New bar
        ht.Update(new TValue(now.AddMinutes(70), 150), isNew: true);
        var afterNew = ht.Last;

        // Multiple corrections
        ht.Update(new TValue(now.AddMinutes(70), 160), isNew: false);
        ht.Update(new TValue(now.AddMinutes(70), 140), isNew: false);
        ht.Update(new TValue(now.AddMinutes(70), 150), isNew: false);
        var afterCorrections = ht.Last;

        Assert.Equal(afterNew.Value, afterCorrections.Value);
    }

    // ── NaN handling ────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_BeforeValidData_ReturnsZero()
    {
        var ht = new HtDcphase();
        var result = ht.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_NaN_AfterValidData_UsesLastValid()
    {
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 80; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + Math.Sin(i * 0.2) * 5));
        }

        Assert.True(ht.IsHot);
        var result = ht.Update(new TValue(now.AddMinutes(80), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 80; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + i * 0.5));
        }

        var result = ht.Update(new TValue(now.AddMinutes(80), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── Reset ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 80; i++)
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
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 80; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + Math.Sin(i * 0.2) * 5));
        }
        var firstResult = ht.Last.Value;

        ht.Reset();
        for (int i = 0; i < 80; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + Math.Sin(i * 0.2) * 5));
        }
        Assert.Equal(firstResult, ht.Last.Value);
    }

    // ── Batch ───────────────────────────────────────────────────────────

    [Fact]
    public void Batch_TSeries_MatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var batchResult = HtDcphase.Batch(series);

        var streaming = new HtDcphase();
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

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

        var spanOutput = new double[values.Length];
        HtDcphase.Batch(values, spanOutput);

        var streaming = new HtDcphase();
        for (int i = 0; i < values.Length; i++)
        {
            var result = streaming.Update(new TValue(DateTime.UtcNow.AddTicks(i), values[i]));
            Assert.Equal(result.Value, spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var source = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentException>(() => HtDcphase.Batch(source, output));
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var ht = new HtDcphase();
        var result = ht.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Update_TSeries_ReturnsCorrectCount()
    {
        var ht = new HtDcphase();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = ht.Update(bars.Close);
        Assert.Equal(100, result.Count);
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = HtDcphase.Calculate(bars.Close);
        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var ht = new HtDcphase();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = bars.Close.Values.ToArray();

        ht.Prime(values);
        Assert.True(ht.IsHot);
    }

    [Fact]
    public void Prime_WithStepParameter()
    {
        var ht = new HtDcphase();
        var values = new double[80];
        for (int i = 0; i < 80; i++)
        {
            values[i] = 100 + Math.Sin(i * 0.2) * 5;
        }

        ht.Prime(values, TimeSpan.FromMinutes(5));
        Assert.True(ht.IsHot);
    }

    // ── Determinism ─────────────────────────────────────────────────────

    [Fact]
    public void TwoInstances_SameInput_SameOutput()
    {
        var ht1 = new HtDcphase();
        var ht2 = new HtDcphase();
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
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            var result = ht.Update(new TValue(now.AddMinutes(i), 100.0));
            Assert.True(double.IsFinite(result.Value),
                $"Bar {i}: Expected finite, got {result.Value}");
        }
    }

    // ── Dispose ─────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ht = new HtDcphase();
        ht.Update(new TValue(DateTime.UtcNow, 100));
        ht.Dispose();
        Assert.True(true); // S2699: explicit assertion for dispose-only test
    }
}
