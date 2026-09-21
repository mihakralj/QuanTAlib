using Xunit;

namespace QuanTAlib.Tests;

public class DwtTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Dwt();
        Assert.Equal("Dwt(4,0)", indicator.Name);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Dwt(levels: 3, output: 1);
        Assert.Equal("Dwt(3,1)", indicator.Name);
    }

    [Fact]
    public void Constructor_ZeroLevel_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dwt(levels: 0));
        Assert.Equal("levels", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeLevel_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dwt(levels: -1));
        Assert.Equal("levels", ex.ParamName);
    }

    [Fact]
    public void Constructor_LevelAboveMax_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dwt(levels: 9));
        Assert.Equal("levels", ex.ParamName);
    }

    [Fact]
    public void Constructor_OutputNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dwt(levels: 4, output: -1));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Constructor_OutputAboveLevels_ThrowsArgumentException()
    {
        // levels=3, output=4 is invalid
        var ex = Assert.Throws<ArgumentException>(() => new Dwt(levels: 3, output: 4));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Constructor_OutputEqualToLevels_IsValid()
    {
        // output == levels is valid (detail at deepest level)
        var indicator = new Dwt(levels: 3, output: 3);
        Assert.NotNull(indicator);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsPowerOfTwo()
    {
        // WarmupPeriod = 2^levels
        Assert.Equal(2, new Dwt(levels: 1).WarmupPeriod);
        Assert.Equal(4, new Dwt(levels: 2).WarmupPeriod);
        Assert.Equal(8, new Dwt(levels: 3).WarmupPeriod);
        Assert.Equal(16, new Dwt(levels: 4).WarmupPeriod);
        Assert.Equal(32, new Dwt(levels: 5).WarmupPeriod);
        Assert.Equal(64, new Dwt(levels: 6).WarmupPeriod);
        Assert.Equal(128, new Dwt(levels: 7).WarmupPeriod);
        Assert.Equal(256, new Dwt(levels: 8).WarmupPeriod);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ApproximationOutput_IsFinite()
    {
        var indicator = new Dwt(levels: 2, output: 0);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80001);
        var bars = gbm.Fetch(warmup + 10, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            var result = indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(result.Value),
                $"DWT approximation must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Update_DetailOutput_IsFinite()
    {
        var indicator = new Dwt(levels: 3, output: 1); // detail at level 1
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80002);
        var bars = gbm.Fetch(warmup + 10, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            var result = indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(result.Value),
                $"DWT detail must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void Last_IsAccessible_AfterUpdate()
    {
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void Name_Accessible_AndContainsDwt()
    {
        var indicator = new Dwt(levels: 4, output: 0);
        Assert.NotNull(indicator.Name);
        Assert.Contains("Dwt", indicator.Name, StringComparison.Ordinal);
    }

    // ─── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80003);
        var bars = gbm.Fetch(warmup + 5, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < warmup; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(warmup), 9999.0), true);
        double after = indicator.Last.Value;

        Assert.True(double.IsFinite(after));
        Assert.NotEqual(before, after, 1.0); // extreme value should change result
    }

    [Fact]
    public void Update_IsNewFalse_RewritesLastBar()
    {
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80004);
        var bars = gbm.Fetch(warmup + 2, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < warmup; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        // New bar with extreme value A
        indicator.Update(new TValue(time.AddMinutes(warmup), 9999.0), true);
        double valueA = indicator.Last.Value;

        // Correct same bar with very different value B
        indicator.Update(new TValue(time.AddMinutes(warmup), 0.001), false);
        double valueB = indicator.Last.Value;

        Assert.NotEqual(valueA, valueB, 1e-6);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80005);
        int count = 30;
        var bars = gbm.Fetch(count, time.Ticks, TimeSpan.FromMinutes(1));

        // Streaming without corrections
        var straight = new Dwt(levels: 2);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        // With corrections (wrong → corrected to same value)
        var corrected = new Dwt(levels: 2);
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
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80006);
        var bars = gbm.Fetch(warmup, time.Ticks, TimeSpan.FromMinutes(1));

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
    public void IsHot_FlipsAtBufferSize()
    {
        // levels=2: bufferSize=4
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod; // 4

        for (int i = 0; i < warmup - 1; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0 + i));
            Assert.False(indicator.IsHot, $"Should not be hot at bar {i + 1}");
        }

        indicator.Update(new TValue(time.AddMinutes(warmup - 1), 100.0 + warmup));
        Assert.True(indicator.IsHot, "Should be hot after warmup bars");
    }

    [Fact]
    public void WarmupPeriod_LevelsDependent()
    {
        Assert.Equal(4, new Dwt(levels: 2).WarmupPeriod);
        Assert.Equal(16, new Dwt(levels: 4).WarmupPeriod);
        Assert.Equal(64, new Dwt(levels: 6).WarmupPeriod);
    }

    // ─── E) Robustness ────────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80007);
        var bars = gbm.Fetch(warmup, time.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < warmup; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(warmup), double.NaN));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_PositiveInfinity_UsesLastValidValue()
    {
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80008);
        var bars = gbm.Fetch(warmup, time.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < warmup; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(warmup), double.PositiveInfinity));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var indicator = new Dwt(levels: 2);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80009);
        var bars = gbm.Fetch(warmup, time.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < warmup; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(warmup), double.NegativeInfinity));
        Assert.Equal(before, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_BatchNaN_AlwaysFinite()
    {
        var indicator = new Dwt(levels: 1); // warmup = 2
        var time = DateTime.UtcNow;

        double[] prices = { 100.0, double.NaN, 102.0, double.NaN, 98.0, 105.0, 103.0, 99.0 };
        for (int i = 0; i < prices.Length; i++)
        {
            var result = indicator.Update(new TValue(time.AddMinutes(i), prices[i]));
            Assert.True(double.IsFinite(result.Value),
                $"Output must be finite at {i}, got {result.Value}");
        }
    }

    // ─── F) Consistency: batch == streaming == span == eventing ──────────────

    [Fact]
    public void AllModes_ConsistencyCheck()
    {
        int levels = 2;
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80010);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Dwt(levels);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Dwt.Batch(source, levels);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Dwt.Batch(rawValues, spanOutput, levels);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Dwt(eventSource, levels);
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
        int count = 50;
        int levels = 2;
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 80011);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Dwt(levels);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Dwt.Batch(source, levels);

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
            Dwt.Batch([], Array.Empty<double>()));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Dwt.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidLevels_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Dwt.Batch(src, dst, levels: 0));
        Assert.Equal("levels", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidOutput_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Dwt.Batch(src, dst, levels: 2, outputComponent: 5));
        Assert.Equal("outputComponent", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputIsFinite()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80012);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Dwt.Batch(src, dst, levels: 3);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v), $"DWT output {v} must be finite");
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        // levels=1: bufferSize=2
        double[] src = new double[20];
        for (int i = 0; i < src.Length; i++)
        {
            src[i] = 100.0 + i;
        }

        src[3] = double.NaN;
        double[] dst = new double[src.Length];
        Dwt.Batch(src, dst, levels: 1);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v), $"Span output should be finite, got {v}");
        }
    }

    [Fact]
    public void Batch_Span_NoStackOverflow_Level8()
    {
        // levels=8: bufferSize=256 — exactly at StackallocThreshold boundary
        int count = 500;
        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = 100.0 + (Math.Sin(i * 0.1) * 10.0);
        }

        double[] dst = new double[count];
        Dwt.Batch(src, dst, levels: 8);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int count = 60;
        int levels = 2;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 80013);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Dwt.Batch(src, spanOut, levels: levels);

        var streaming = new Dwt(levels);
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
        var indicator = new Dwt(levels: 2);
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
        int levels = 2;
        var source = new TSeries();
        var indicator = new Dwt(source, levels);
        int warmup = indicator.WarmupPeriod;

        var time = DateTime.UtcNow;
        for (int i = 0; i < warmup; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 + i), true);
        }

        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Pub_EventValue_MatchesLast()
    {
        var indicator = new Dwt(levels: 2);
        TValue? lastEvent = null;
        indicator.Pub += (object? s, in TValueEventArgs e) => lastEvent = e.Value;

        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80014);
        var bars = gbm.Fetch(warmup + 2, time.Ticks, TimeSpan.FromMinutes(1));

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
        int count = 50;
        int levels = 2;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80015);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Dwt.Calculate(bars.Close, levels);

        Assert.Equal(count, results.Count);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }

    [Fact]
    public void AllLevels_Approximation_IsFinite()
    {
        int count = 300;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80016);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int level = 1; level <= 8; level++)
        {
            var ind = new Dwt(levels: level, output: 0);
            for (int i = 0; i < bars.Close.Count; i++)
            {
                var result = ind.Update(bars.Close[i]);
                Assert.True(double.IsFinite(result.Value),
                    $"Level {level} approximation must be finite at bar {i}");
            }
        }
    }

    [Fact]
    public void AllDetailLevels_AreFinite()
    {
        int count = 300;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 80017);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test detail output at each level
        for (int maxLevels = 1; maxLevels <= 5; maxLevels++)
        {
            for (int detail = 1; detail <= maxLevels; detail++)
            {
                var ind = new Dwt(levels: maxLevels, output: detail);
                for (int i = 0; i < bars.Close.Count; i++)
                {
                    var result = ind.Update(bars.Close[i]);
                    Assert.True(double.IsFinite(result.Value),
                        $"Detail level {detail}/{maxLevels} must be finite at bar {i}");
                }
            }
        }
    }
}
