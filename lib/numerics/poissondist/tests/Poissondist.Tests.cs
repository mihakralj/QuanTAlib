using Xunit;

namespace QuanTAlib.Tests;

public class PoissondistTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Poissondist();
        Assert.Equal("Poissondist(1.00,14,5)", indicator.Name);
        Assert.Equal(14, indicator.WarmupPeriod);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Poissondist(lambda: 5.0, period: 30, threshold: 10);
        Assert.Equal("Poissondist(5.00,30,10)", indicator.Name);
        Assert.Equal(30, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroLambda_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Poissondist(lambda: 0.0));
        Assert.Equal("lambda", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeLambda_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Poissondist(lambda: -1.0));
        Assert.Equal("lambda", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Poissondist(period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Poissondist(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeThreshold_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Poissondist(threshold: -1));
        Assert.Equal("threshold", ex.ParamName);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_OutputInRange()
    {
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 3, threshold: 5);
        indicator.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void IsHot_Property_ReflectsWarmup()
    {
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 71001);
        var bars = gbm.Fetch(20, time.Ticks, TimeSpan.FromMinutes(1));

        // Streaming without corrections
        var straight = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        // With corrections (wrong → corrected)
        var corrected = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: period, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
        // Flat range → x=0.5, lambda = lambdaScale*0.5 = 5.0*0.5 = 2.5
        // PoissonCdf(5, 2.5) should be in [0,1]
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0));
        }

        double expected = Poissondist.PoissonCdf(5, 2.5);
        Assert.True(Math.Abs(indicator.Last.Value - expected) < 1e-9,
            $"Expected {expected} but got {indicator.Last.Value}");
    }

    // ─── F) Consistency: batch == streaming == span == eventing ──────────────

    [Fact]
    public void AllModes_ConsistencyCheck()
    {
        int count = 100;
        int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 71002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Poissondist(lambda: 5.0, period: period, threshold: 5);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Poissondist.Batch(source, lambda: 5.0, period: period, threshold: 5);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Poissondist.Batch(rawValues, spanOutput, lambda: 5.0, period: period, threshold: 5);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Poissondist(eventSource, lambda: 5.0, period: period, threshold: 5);
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
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 71003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Poissondist(lambda: 5.0, period: period, threshold: 5);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Poissondist.Batch(source, lambda: 5.0, period: period, threshold: 5);

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
            Poissondist.Batch([], Array.Empty<double>()));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Poissondist.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidLambda_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Poissondist.Batch(src, dst, lambda: 0.0));
        Assert.Equal("lambda", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Poissondist.Batch(src, dst, period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidThreshold_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Poissondist.Batch(src, dst, threshold: -1));
        Assert.Equal("threshold", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputInRange()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 71004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Poissondist.Batch(src, dst, lambda: 5.0, period: 20, threshold: 5);

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
        Poissondist.Batch(src, dst, lambda: 5.0, period: 5);

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
        Poissondist.Batch(src, dst, lambda: 5.0, period: 300, threshold: 5);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int count = 60;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 71005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Poissondist.Batch(src, spanOut, lambda: 5.0, period: 14, threshold: 5);

        var streaming = new Poissondist(lambda: 5.0, period: 14, threshold: 5);
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
        var indicator = new Poissondist(lambda: 5.0, period: 3, threshold: 5);
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
        var indicator = new Poissondist(source, lambda: 5.0, period: period, threshold: 5);

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
        var indicator = new Poissondist(lambda: 5.0, period: 5, threshold: 5);
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
    public void DifferentLambdaThreshold_ProduceDifferentResults()
    {
        int count = 60;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 71006);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ind1 = new Poissondist(lambda: 2.0, period: 20, threshold: 3);
        var ind2 = new Poissondist(lambda: 5.0, period: 20, threshold: 5);
        var ind3 = new Poissondist(lambda: 10.0, period: 20, threshold: 5);

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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 71007);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Poissondist.Calculate(bars.Close, lambda: 5.0, period: 20);

        Assert.Equal(count, results.Count);
        Assert.True(instance.IsHot);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }

    [Fact]
    public void ThresholdZero_CdfIsSmallForHighLambda()
    {
        // P(X<=0; lambda=5) = e^(-5) ≈ 0.00674
        double cdf = Poissondist.PoissonCdf(0, 5.0);
        Assert.True(Math.Abs(cdf - Math.Exp(-5.0)) < 1e-9,
            $"Expected {Math.Exp(-5.0):G} got {cdf:G}");
    }

    [Fact]
    public void LambdaZero_ReturnsOne()
    {
        // λ=0: all mass at X=0, P(X<=k) = 1 for k>=0
        double cdf = Poissondist.PoissonCdf(5, 0.0);
        Assert.Equal(1.0, cdf, 1e-10);
    }

    [Fact]
    public void NegativeK_ReturnsZero()
    {
        double cdf = Poissondist.PoissonCdf(-1, 5.0);
        Assert.Equal(0.0, cdf, 1e-10);
    }
}
