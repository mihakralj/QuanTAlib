using Xunit;

namespace QuanTAlib.Tests;

public class WeibulldistTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Weibulldist();
        Assert.Equal("Weibulldist(1.50,1.00,14)", indicator.Name);
        Assert.Equal(14, indicator.WarmupPeriod);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Weibulldist(k: 2.0, lambda: 0.5, period: 20);
        Assert.Equal("Weibulldist(2.00,0.50,20)", indicator.Name);
        Assert.Equal(20, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroK_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Weibulldist(k: 0.0));
        Assert.Equal("k", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeK_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Weibulldist(k: -1.0));
        Assert.Equal("k", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroLambda_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Weibulldist(lambda: 0.0));
        Assert.Equal("lambda", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeLambda_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Weibulldist(lambda: -1.0));
        Assert.Equal("lambda", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Weibulldist(period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Weibulldist(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Weibulldist(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Weibulldist(period: 5);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_OutputInRange()
    {
        var indicator = new Weibulldist(k: 1.5, lambda: 1.0, period: 5);
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
        var indicator = new Weibulldist(period: 3);
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void IsHot_Property_ReflectsWarmup()
    {
        var indicator = new Weibulldist(period: 5);
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
    public void Update_AtMaxOfWindow_ReturnsNearOne()
    {
        // When current value equals window max, x=1.0 → high CDF value
        var indicator = new Weibulldist(k: 2.0, lambda: 1.0, period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 100.0, 102.0, 98.0, 101.0, 110.0 }; // 110 is max

        foreach (var p in prices)
        {
            indicator.Update(new TValue(time, p));
            time = time.AddMinutes(1);
        }

        // CDF(1.0, k=2, λ=1) = 1 - exp(-1) ≈ 0.6321
        Assert.True(indicator.Last.Value > 0.5, $"Expected > 0.5 but got {indicator.Last.Value}");
    }

    [Fact]
    public void Update_AtMinOfWindow_ReturnsZero()
    {
        // When current value equals window min, x=0.0 → CDF(0, k, λ) = 0
        var indicator = new Weibulldist(k: 2.0, lambda: 1.0, period: 5);
        var time = DateTime.UtcNow;
        double[] prices = { 110.0, 102.0, 98.0, 101.0, 90.0 }; // 90 is min

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
        var indicator = new Weibulldist(period: 5);
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
        var indicator = new Weibulldist(period: 5);
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

        // Correct same bar with value B
        indicator.Update(new TValue(time, 90.0), false);
        double valueB = indicator.Last.Value;

        Assert.NotEqual(valueA, valueB, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72001);
        var bars = gbm.Fetch(20, time.Ticks, TimeSpan.FromMinutes(1));

        // Streaming without corrections
        var straight = new Weibulldist(period: 5);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        // With corrections (wrong → corrected)
        var corrected = new Weibulldist(period: 5);
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
        var indicator = new Weibulldist(period: 5);
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
        var indicator = new Weibulldist(period: period);
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
        var indicator = new Weibulldist(period: 5);
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
        var indicator = new Weibulldist(period: 5);
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
        var indicator = new Weibulldist(period: 5);
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
        var indicator = new Weibulldist(period: 5);
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
    public void Update_FlatRange_ReturnsCdfAtHalf()
    {
        // When all values in window are identical, range=0 → x=0.5
        var indicator = new Weibulldist(k: 2.0, lambda: 1.0, period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0));
        }

        // CDF(0.5/1.0, k=2, λ=1) = 1 - exp(-0.5^2) = 1 - exp(-0.25)
        double expected = 1.0 - Math.Exp(-Math.Pow(0.5, 2.0));
        Assert.Equal(expected, indicator.Last.Value, 1e-6);
    }

    // ─── F) Consistency: batch == streaming == span == eventing ──────────────

    [Fact]
    public void AllModes_ConsistencyCheck()
    {
        int count = 100;
        int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Weibulldist(period: period);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Weibulldist.Batch(source, period: period);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Weibulldist.Batch(rawValues, spanOutput, period: period);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Weibulldist(eventSource, period: period);
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
        int period = 15;
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 72003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Weibulldist(period: period);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Weibulldist.Batch(source, period: period);

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
            Weibulldist.Batch([], Array.Empty<double>()));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Weibulldist.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidK_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Weibulldist.Batch(src, dst, k: 0.0));
        Assert.Equal("k", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_NegativeK_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Weibulldist.Batch(src, dst, k: -0.5));
        Assert.Equal("k", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidLambda_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Weibulldist.Batch(src, dst, lambda: 0.0));
        Assert.Equal("lambda", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_NegativeLambda_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Weibulldist.Batch(src, dst, lambda: -1.0));
        Assert.Equal("lambda", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Weibulldist.Batch(src, dst, period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputInRange()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Weibulldist.Batch(src, dst, period: 20);

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
        Weibulldist.Batch(src, dst, period: 5);

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
            src[i] = 100.0 + (Math.Sin(i * 0.1) * 10.0);
        }

        double[] dst = new double[count];
        Weibulldist.Batch(src, dst, period: 300);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int count = 60;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 72005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Weibulldist.Batch(src, spanOut, period: 14);

        var streaming = new Weibulldist(period: 14);
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
        var indicator = new Weibulldist(period: 3);
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
        var indicator = new Weibulldist(source, period: period);

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
        var indicator = new Weibulldist(period: 5);
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

    // ─── Additional: Shape/scale parameter effects ───────────────────────────

    [Fact]
    public void DifferentShapes_ProduceDifferentResults()
    {
        // Verify at a non-boundary interior point (x=0.4, lambda=1.0) that different k values
        // produce provably distinct CDF outputs — no GBM needed for this mathematical property
        const double x = 0.4;
        const double lambda = 1.0;

        double cdf05 = Weibulldist.StaticCdf(x, k: 0.5, lambda: lambda);  // concave, fast rise
        double cdf15 = Weibulldist.StaticCdf(x, k: 1.5, lambda: lambda);  // intermediate
        double cdf50 = Weibulldist.StaticCdf(x, k: 5.0, lambda: lambda);  // sigmoidal, slow rise

        // All in [0,1]
        Assert.InRange(cdf05, 0.0, 1.0);
        Assert.InRange(cdf15, 0.0, 1.0);
        Assert.InRange(cdf50, 0.0, 1.0);

        // k=0.5 (concave) > k=1.5 > k=5.0 (sigmoidal) at x=0.4 < lambda: strict ordering
        Assert.True(cdf05 > cdf15 + 1e-6, $"k=0.5 ({cdf05:G10}) should exceed k=1.5 ({cdf15:G10}) at x={x}");
        Assert.True(cdf15 > cdf50 + 1e-6, $"k=1.5 ({cdf15:G10}) should exceed k=5.0 ({cdf50:G10}) at x={x}");
    }

    [Fact]
    public void Calculate_StaticMethod_ReturnsTuple()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72007);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Weibulldist.Calculate(bars.Close, period: 20);

        Assert.Equal(count, results.Count);
        Assert.True(instance.IsHot);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }
}
