using Xunit;

namespace QuanTAlib.Tests;

public class BinomdistTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Binomdist();
        Assert.Equal("Binomdist(50,20,10)", indicator.Name);
        Assert.Equal(50, indicator.WarmupPeriod);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Binomdist(30, 15, 7);
        Assert.Equal("Binomdist(30,15,7)", indicator.Name);
        Assert.Equal(30, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Binomdist(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Binomdist(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroTrials_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Binomdist(trials: 0));
        Assert.Equal("trials", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeTrials_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Binomdist(trials: -5));
        Assert.Equal("trials", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeThreshold_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Binomdist(threshold: -1));
        Assert.Equal("threshold", ex.ParamName);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_OutputInRange()
    {
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 3, trials: 10, threshold: 5);
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void IsHot_Property_ReflectsWarmup()
    {
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0 + i));
            Assert.False(indicator.IsHot);
        }

        indicator.Update(new TValue(time.AddMinutes(4), 104.0));
        Assert.True(indicator.IsHot);
    }

    // ─── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 43001);
        var bars = gbm.Fetch(20, time.Ticks, TimeSpan.FromMinutes(1));

        // Streaming without corrections
        var straight = new Binomdist(period: 5, trials: 10, threshold: 5);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        // With corrections (wrong → corrected)
        var corrected = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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
    public void Update_FlatRange_ReturnsExpectedCdf()
    {
        // When all values in window are identical, range=0 → p=0.5
        // P(X≤5; n=10, p=0.5) = 0.623046875 (exact)
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0));
        }

        // p=0.5, n=10, k=5: exact = 0.623046875
        Assert.True(Math.Abs(indicator.Last.Value - 0.623046875) < 1e-9,
            $"Expected ~0.623046875 but got {indicator.Last.Value}");
    }

    // ─── F) Consistency: batch == streaming == span == eventing ──────────────

    [Fact]
    public void AllModes_ConsistencyCheck()
    {
        int count = 100;
        int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 43002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Binomdist(period, trials: 15, threshold: 7);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Binomdist.Batch(source, period, trials: 15, threshold: 7);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Binomdist.Batch(rawValues, spanOutput, period, trials: 15, threshold: 7);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Binomdist(eventSource, period, trials: 15, threshold: 7);
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
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 43003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Binomdist(period, trials: 10, threshold: 5);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Binomdist.Batch(source, period, trials: 10, threshold: 5);

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
            Binomdist.Batch([], Array.Empty<double>()));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Binomdist.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Binomdist.Batch(src, dst, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidTrials_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Binomdist.Batch(src, dst, trials: 0));
        Assert.Equal("trials", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidThreshold_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Binomdist.Batch(src, dst, threshold: -1));
        Assert.Equal("threshold", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputInRange()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 43004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Binomdist.Batch(src, dst, period: 20, trials: 10, threshold: 5);

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
        Binomdist.Batch(src, dst, period: 5);

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
        Binomdist.Batch(src, dst, period: 300, trials: 20, threshold: 10);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int count = 60;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 43005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Binomdist.Batch(src, spanOut, period: 14, trials: 10, threshold: 5);

        var streaming = new Binomdist(period: 14, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(period: 3, trials: 10, threshold: 5);
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
        var indicator = new Binomdist(source, period, trials: 10, threshold: 5);

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
        var indicator = new Binomdist(period: 5, trials: 10, threshold: 5);
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

    // ─── Additional: Parameter combinations ───────────────────────────────────

    [Fact]
    public void DifferentTrialsThreshold_ProduceDifferentResults()
    {
        int count = 60;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 43006);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ind1 = new Binomdist(period: 20, trials: 10, threshold: 3);
        var ind2 = new Binomdist(period: 20, trials: 10, threshold: 5);
        var ind3 = new Binomdist(period: 20, trials: 20, threshold: 5);

        for (int i = 0; i < count; i++)
        {
            ind1.Update(bars.Close[i]);
            ind2.Update(bars.Close[i]);
            ind3.Update(bars.Close[i]);
        }

        Assert.NotEqual(ind1.Last.Value, ind2.Last.Value, 1e-4);
        Assert.NotEqual(ind2.Last.Value, ind3.Last.Value, 1e-4);
    }

    [Fact]
    public void Calculate_StaticMethod_ReturnsTuple()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 43007);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Binomdist.Calculate(bars.Close, period: 20);

        Assert.Equal(count, results.Count);
        Assert.True(instance.IsHot);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }

    [Fact]
    public void ThresholdZero_ProbabilityIsNearZeroForMidP()
    {
        // P(X<=0; n=10, p=0.5) = 0.5^10 ≈ 0.000977
        double cdf = Binomdist.BinomialCdf(0.5, 10, 0);
        Assert.True(Math.Abs(cdf - 0.0009765625) < 1e-10, $"Expected 0.0009765625 got {cdf}");
    }

    [Fact]
    public void ThresholdEqualN_ProbabilityIsOne()
    {
        // P(X<=n; n, p) = 1 for any p in (0,1)
        double cdf = Binomdist.BinomialCdf(0.7, 10, 10);
        Assert.Equal(1.0, cdf, 1e-10);
    }

    [Fact]
    public void ProbabilityZero_AlwaysReturnsOne()
    {
        // p=0: all mass at X=0, so P(X<=k) = 1 for k >= 0
        double cdf = Binomdist.BinomialCdf(0.0, 10, 5);
        Assert.Equal(1.0, cdf, Tolerance);
    }

    [Fact]
    public void ProbabilityOne_ReturnsOneOnlyIfKGreaterEqualN()
    {
        // p=1: all mass at X=n, so P(X<=k) = 1 iff k >= n
        double cdfAtN = Binomdist.BinomialCdf(1.0, 10, 10);
        double cdfBelowN = Binomdist.BinomialCdf(1.0, 10, 5);
        Assert.Equal(1.0, cdfAtN, Tolerance);
        Assert.Equal(0.0, cdfBelowN, Tolerance);
    }
}
