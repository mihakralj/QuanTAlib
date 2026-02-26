using Xunit;

namespace QuanTAlib.Tests;

public class FdistTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Fdist();
        Assert.Equal("Fdist(1,1,14)", indicator.Name);
        Assert.Equal(14, indicator.WarmupPeriod);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Fdist(d1: 5, d2: 10, period: 20);
        Assert.Equal("Fdist(5,10,20)", indicator.Name);
        Assert.Equal(20, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_D1Zero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fdist(d1: 0));
        Assert.Equal("d1", ex.ParamName);
    }

    [Fact]
    public void Constructor_D1Negative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fdist(d1: -1));
        Assert.Equal("d1", ex.ParamName);
    }

    [Fact]
    public void Constructor_D2Zero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fdist(d2: 0));
        Assert.Equal("d2", ex.ParamName);
    }

    [Fact]
    public void Constructor_D2Negative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fdist(d2: -1));
        Assert.Equal("d2", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fdist(period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fdist(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Fdist(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_OutputInRange()
    {
        var indicator = new Fdist(d1: 5, d2: 5, period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        Assert.True(indicator.Last.Value >= 0.0, "Output must be >= 0");
        Assert.True(indicator.Last.Value <= 1.0, "Output must be <= 1");
    }

    [Fact]
    public void Last_IsAccessible_AfterUpdate()
    {
        var indicator = new Fdist(period: 3);
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void IsHot_Property_ReflectsWarmup()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0 + i));
            Assert.False(indicator.IsHot);
        }

        indicator.Update(new TValue(time.AddMinutes(4), 104.0));
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_AtMaxOfWindow_ReturnsHighValue()
    {
        // When current value equals window max, xNorm=1, xF=10 → F-CDF near 1
        var indicator = new Fdist(d1: 5, d2: 5, period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 101.0, 110.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        Assert.True(indicator.Last.Value > 0.9, $"Expected near 1 but got {indicator.Last.Value}");
    }

    [Fact]
    public void Update_AtMinOfWindow_ReturnsZero()
    {
        // When current value equals window min, xNorm=0, xF=0 → F-CDF(0) = 0
        var indicator = new Fdist(d1: 5, d2: 5, period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 110.0, 102.0, 98.0, 101.0, 90.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
    }

    // ─── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        double first = indicator.Last.Value;

        indicator.Update(new TValue(time, 110.0));
        double second = indicator.Last.Value;

        Assert.NotEqual(first, second, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_RewritesLastBar()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;

        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };
        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        // New bar with value A
        indicator.Update(new TValue(time, 110.0), true);
        double valueA = indicator.Last.Value;

        // Correct same bar with value B (min of window → 0)
        indicator.Update(new TValue(time, 90.0), false);
        double valueB = indicator.Last.Value;

        Assert.NotEqual(valueA, valueB, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 65001);
        var bars = gbm.Fetch(20, time.Ticks, TimeSpan.FromMinutes(1));

        // Streaming without corrections
        var straight = new Fdist(d1: 5, d2: 5, period: 5);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        // With corrections (wrong → corrected)
        var corrected = new Fdist(d1: 5, d2: 5, period: 5);
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
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        Assert.True(indicator.IsHot);

        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    // ─── D) Warmup / convergence ──────────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        int period = 10;
        var indicator = new Fdist(period: period);
        var time = DateTime.UtcNow;

        for (int i = 0; i < period - 1; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0 + i));
            Assert.False(indicator.IsHot, $"Should not be hot at bar {i + 1}");
        }

        indicator.Update(new TValue(time.AddMinutes(period - 1), 100.0 + period));
        Assert.True(indicator.IsHot, "Should be hot after period bars");
    }

    // ─── E) Robustness ────────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        double before = indicator.Last.Value;

        indicator.Update(new TValue(time, double.NaN));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_PositiveInfinity_UsesLastValidValue()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time, double.PositiveInfinity));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time, double.NegativeInfinity));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_BatchNaN_Stable()
    {
        var indicator = new Fdist(period: 5);
        var time = DateTime.UtcNow;

        double[] prices = { 100.0, double.NaN, 102.0, double.NaN, 98.0, 105.0, 103.0 };
        foreach (var p in prices)
        {
            var result = indicator.Update(new TValue(time, p));
            Assert.True(double.IsFinite(result.Value), "Output must always be finite");
            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Update_FlatRange_ReturnsStableValue()
    {
        // All identical values → range=0 → xNorm=0.5 → xF=5 → F-CDF(5; d1, d2)
        var indicator = new Fdist(d1: 5, d2: 5, period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0));
        }

        double expected = Fdist.FCdf(5.0, 5, 5);
        Assert.Equal(expected, indicator.Last.Value, 1e-6);
    }

    // ─── F) Consistency: batch == streaming == span == eventing ──────────────

    [Fact]
    public void AllModes_ConsistencyCheck()
    {
        int count = 100;
        int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 65002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Fdist(d1: 5, d2: 5, period: period);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Fdist.Batch(source, d1: 5, d2: 5, period: period);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Fdist.Batch(rawValues, spanOutput, d1: 5, d2: 5, period: period);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Fdist(eventSource, d1: 5, d2: 5, period: period);
        eventIndicator.Pub += (object? s, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i], true);
        }

        // Verify last value matches across all modes
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
        int period = 15;
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 65003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Fdist(d1: 3, d2: 7, period: period);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Fdist.Batch(source, d1: 3, d2: 7, period: period);

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
            Fdist.Batch([], Array.Empty<double>()));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fdist.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidD1_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fdist.Batch(src, dst, d1: 0));
        Assert.Equal("d1", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidD2_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fdist.Batch(src, dst, d2: 0));
        Assert.Equal("d2", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Fdist.Batch(src, dst, period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputInRange()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 65004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Fdist.Batch(src, dst, d1: 5, d2: 5, period: 20);

        foreach (double v in dst)
        {
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} out of [0,1] range");
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        double[] src = { 100.0, double.NaN, 102.0, 98.0, 105.0, 103.0 };
        double[] dst = new double[src.Length];
        Fdist.Batch(src, dst, period: 5);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v), "Span output should always be finite");
        }
    }

    [Fact]
    public void Batch_Span_NoStackOverflow_LargeData()
    {
        int count = 5000;
        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = 100.0 + Math.Sin(i * 0.1) * 10.0;
        }

        double[] dst = new double[count];
        Fdist.Batch(src, dst, d1: 5, d2: 5, period: 300);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int count = 60;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 65005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Fdist.Batch(src, spanOut, d1: 5, d2: 5, period: 14);

        var streaming = new Fdist(d1: 5, d2: 5, period: 14);
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
        var indicator = new Fdist(period: 3);
        int count = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => count++;

        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 100.0));
        indicator.Update(new TValue(time.AddMinutes(1), 102.0));
        indicator.Update(new TValue(time.AddMinutes(2), 98.0));

        Assert.Equal(3, count);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        int period = 5;
        var source = new TSeries();
        var indicator = new Fdist(source, period: period);

        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            source.Add(new TValue(time, p), true);
            time = time.AddMinutes(1);
        }

        Assert.True(indicator.IsHot);
        Assert.True(indicator.Last.Value >= 0.0 && indicator.Last.Value <= 1.0);
    }

    [Fact]
    public void Pub_EventValue_MatchesLast()
    {
        var indicator = new Fdist(period: 5);
        TValue? lastEvent = null;
        indicator.Pub += (object? s, in TValueEventArgs e) => lastEvent = e.Value;

        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 105.0, 103.0 };

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        Assert.NotNull(lastEvent);
        Assert.Equal(indicator.Last.Value, lastEvent.Value.Value, Tolerance);
    }

    // ─── Additional: DoF parameter effects ──────────────────────────────────

    [Fact]
    public void DifferentDoF_ProduceDifferentResults()
    {
        int count = 60;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 65006);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ind1 = new Fdist(d1: 1, d2: 1, period: 20);
        var ind2 = new Fdist(d1: 5, d2: 5, period: 20);
        var ind3 = new Fdist(d1: 10, d2: 2, period: 20);

        for (int i = 0; i < count; i++)
        {
            ind1.Update(bars.Close[i]);
            ind2.Update(bars.Close[i]);
            ind3.Update(bars.Close[i]);
        }

        // Different DoFs produce different CDFs
        Assert.False(
            Math.Abs(ind1.Last.Value - ind2.Last.Value) < 1e-6 &&
            Math.Abs(ind2.Last.Value - ind3.Last.Value) < 1e-6,
            "Different DoFs should produce at least one distinct result");
    }

    [Fact]
    public void Calculate_StaticMethod_ReturnsTuple()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 65007);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Fdist.Calculate(bars.Close, d1: 5, d2: 5, period: 20);

        Assert.Equal(count, results.Count);
        Assert.True(instance.IsHot);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }
}
