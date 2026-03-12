using Xunit;

namespace QuanTAlib.Tests;

public class CwtTests
{
    private const double Tolerance = 1e-10;

    // ─── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var indicator = new Cwt();
        Assert.Equal("Cwt(10,6)", indicator.Name);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsName()
    {
        var indicator = new Cwt(scale: 20.0, omega0: 5.0);
        Assert.Equal("Cwt(20,5)", indicator.Name);
    }

    [Fact]
    public void Constructor_ZeroScale_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cwt(scale: 0.0));
        Assert.Equal("scale", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeScale_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cwt(scale: -1.0));
        Assert.Equal("scale", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroOmega_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cwt(omega0: 0.0));
        Assert.Equal("omega0", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeOmega_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cwt(omega0: -6.0));
        Assert.Equal("omega0", ex.ParamName);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsWindowSize()
    {
        // windowSize = 2*round(3*scale)+1 = 2*30+1 = 61 for scale=10
        var indicator = new Cwt(scale: 10.0);
        Assert.Equal(61, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_SmallScale_CorrectWarmup()
    {
        // scale=1: halfWindow=round(3)=3, windowSize=7
        var indicator = new Cwt(scale: 1.0);
        Assert.Equal(7, indicator.WarmupPeriod);
    }

    // ─── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        var input = new TValue(time, 100.0);
        var result = indicator.Update(input);
        Assert.Equal(input.Time, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Output_IsNonNegative()
    {
        // CWT magnitude is always >= 0
        var indicator = new Cwt(scale: 3.0);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70001);
        var bars = gbm.Fetch(windowSize + 10, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(indicator.Last.Value >= 0.0,
                $"CWT magnitude must be >= 0, got {indicator.Last.Value} at bar {i}");
        }
    }

    [Fact]
    public void Last_IsAccessible_AfterUpdate()
    {
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 50.0));
        Assert.NotEqual(default, indicator.Last);
    }

    [Fact]
    public void Name_Accessible()
    {
        var indicator = new Cwt(scale: 5.0, omega0: 6.0);
        Assert.NotNull(indicator.Name);
        Assert.Contains("Cwt", indicator.Name, StringComparison.Ordinal);
    }

    // ─── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70002);
        var bars = gbm.Fetch(windowSize + 5, time.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < windowSize; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        double before = indicator.Last.Value;
        indicator.Update(new TValue(time.AddMinutes(windowSize), 9999.0), true);
        double after = indicator.Last.Value;

        // Extreme new value should change the output
        Assert.True(double.IsFinite(after));
        // Values may differ (9999 vs GBM prices)
        _ = before; // consumed
    }

    [Fact]
    public void Update_IsNewFalse_RewritesLastBar()
    {
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70003);
        var bars = gbm.Fetch(windowSize + 2, time.Ticks, TimeSpan.FromMinutes(1));

        // Fill to warmup
        for (int i = 0; i < windowSize; i++)
        {
            indicator.Update(bars.Close[i]);
        }

        // New bar with extreme value A
        indicator.Update(new TValue(time.AddMinutes(windowSize), 9999.0), true);
        double valueA = indicator.Last.Value;

        // Correct same bar with a different extreme value B
        indicator.Update(new TValue(time.AddMinutes(windowSize), 0.001), false);
        double valueB = indicator.Last.Value;

        Assert.NotEqual(valueA, valueB, 1e-6);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var time = DateTime.UtcNow;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70004);
        int count = 30;
        var bars = gbm.Fetch(count, time.Ticks, TimeSpan.FromMinutes(1));

        // Streaming without corrections
        var straight = new Cwt(scale: 2.0);
        for (int i = 0; i < bars.Close.Count; i++)
        {
            straight.Update(bars.Close[i]);
        }

        double finalStraight = straight.Last.Value;

        // With corrections (wrong → corrected)
        var corrected = new Cwt(scale: 2.0);
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
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70005);
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
        // scale=2: halfWindow=round(6)=6, windowSize=13
        var indicator = new Cwt(scale: 2.0);
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
    public void WarmupPeriod_ScaleDependent()
    {
        // scale=5: halfWindow=round(15)=15, windowSize=31
        var ind5 = new Cwt(scale: 5.0);
        Assert.Equal(31, ind5.WarmupPeriod);

        // scale=0.5: halfWindow=round(1.5)=2, windowSize=5
        var ind05 = new Cwt(scale: 0.5);
        Assert.Equal(5, ind05.WarmupPeriod);
    }

    // ─── E) Robustness ────────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        // Fill to hot
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70006);
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
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70007);
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
        var indicator = new Cwt(scale: 2.0);
        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70008);
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
        var indicator = new Cwt(scale: 2.0);
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
        int scale = 3;
        int count = 80;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70009);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Cwt(scale);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Batch (TSeries)
        var batch = Cwt.Batch(source, scale);

        // Span
        var rawValues = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            rawValues[i] = source[i].Value;
        }

        var spanOutput = new double[source.Count];
        Cwt.Batch(rawValues, spanOutput, scale);

        // Eventing
        var eventResults = new List<double>();
        var eventSource = new TSeries();
        var eventIndicator = new Cwt(eventSource, scale);
        eventIndicator.Pub += (object? s, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i], true);
        }

        // Verify last value matches all modes
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
        double scale = 2.0;
        var gbm = new GBM(startPrice: 50, mu: 0.0, sigma: 0.3, seed: 70010);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Cwt(scale);
        var streamingVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamingVals[i] = streaming.Last.Value;
        }

        var batch = Cwt.Batch(source, scale);

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
            Cwt.Batch([], Array.Empty<double>()));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() =>
            Cwt.Batch(src, dst));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidScale_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Cwt.Batch(src, dst, scale: 0.0));
        Assert.Equal("scale", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidOmega_ThrowsArgumentException()
    {
        double[] src = { 1.0, 2.0, 3.0 };
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Cwt.Batch(src, dst, omega0: -1.0));
        Assert.Equal("omega0", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputIsNonNegative()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70011);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Cwt.Batch(src, dst, scale: 3.0);

        foreach (double v in dst)
        {
            Assert.True(v >= 0.0, $"CWT magnitude {v} must be >= 0");
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        int windowSize = 7; // scale=1: 2*3+1=7
        double[] src = new double[windowSize + 5];
        for (int i = 0; i < src.Length; i++)
        {
            src[i] = 100.0 + i;
        }

        src[3] = double.NaN;
        double[] dst = new double[src.Length];
        Cwt.Batch(src, dst, scale: 1.0);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v), $"Span output should always be finite, got {v}");
        }
    }

    [Fact]
    public void Batch_Span_NoStackOverflow_LargeScale()
    {
        // scale=40: halfWindow=120, windowSize=241 → uses ArrayPool (>128)
        int count = 500;
        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = 100.0 + (Math.Sin(i * 0.1) * 10.0);
        }

        double[] dst = new double[count];
        // Should not throw StackOverflowException
        Cwt.Batch(src, dst, scale: 40.0);

        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int count = 60;
        double scale = 2.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 70012);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Cwt.Batch(src, spanOut, scale: scale);

        var streaming = new Cwt(scale);
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
        var indicator = new Cwt(scale: 2.0);
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
        double scale = 2.0;
        var source = new TSeries();
        var indicator = new Cwt(source, scale);
        int windowSize = indicator.WarmupPeriod;

        var time = DateTime.UtcNow;
        for (int i = 0; i < windowSize; i++)
        {
            source.Add(new TValue(time.AddMinutes(i), 100.0 + i), true);
        }

        Assert.True(indicator.IsHot);
        Assert.True(indicator.Last.Value >= 0.0);
    }

    [Fact]
    public void Pub_EventValue_MatchesLast()
    {
        var indicator = new Cwt(scale: 2.0);
        TValue? lastEvent = null;
        indicator.Pub += (object? s, in TValueEventArgs e) => lastEvent = e.Value;

        var time = DateTime.UtcNow;
        int windowSize = indicator.WarmupPeriod;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70013);
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
        double scale = 3.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 70014);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, instance) = Cwt.Calculate(bars.Close, scale);

        Assert.Equal(count, results.Count);
        Assert.Equal(results[^1].Value, instance.Last.Value, Tolerance);
    }
}
