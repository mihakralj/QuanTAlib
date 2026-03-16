using Xunit;

namespace QuanTAlib.Tests;

public sealed class DymiTests
{
    private const double Tolerance = 1e-10;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_BasePeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dymi(basePeriod: 1));
        Assert.Equal("basePeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShortPeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dymi(shortPeriod: 1));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_LongPeriodEqualShortPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dymi(shortPeriod: 5, longPeriod: 5));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_LongPeriodLessThanShortPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dymi(shortPeriod: 10, longPeriod: 5));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_MinPeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dymi(minPeriod: 1));
        Assert.Equal("minPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_MaxPeriodLessThanMinPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dymi(minPeriod: 10, maxPeriod: 5));
        Assert.Equal("maxPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidDefaults_SetsProperties()
    {
        var d = new Dymi();
        Assert.Equal(14, d.BasePeriod);
        Assert.Equal(5, d.ShortPeriod);
        Assert.Equal(10, d.LongPeriod);
        Assert.Equal(3, d.MinPeriod);
        Assert.Equal(30, d.MaxPeriod);
        Assert.Equal("Dymi(14,5,10,3,30)", d.Name);
        Assert.False(d.IsHot);
    }

    [Fact]
    public void Constructor_CustomPeriods_SetsProperties()
    {
        var d = new Dymi(basePeriod: 10, shortPeriod: 3, longPeriod: 7, minPeriod: 2, maxPeriod: 20);
        Assert.Equal(10, d.BasePeriod);
        Assert.Equal(3, d.ShortPeriod);
        Assert.Equal(7, d.LongPeriod);
        Assert.Equal(2, d.MinPeriod);
        Assert.Equal(20, d.MaxPeriod);
    }

    [Fact]
    public void BatchSpan_OutputLengthMismatch_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[4];
        var ex = Assert.Throws<ArgumentException>(() => Dymi.Batch(src, out1));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_BasePeriodOne_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Dymi.Batch(src, out1, basePeriod: 1));
        Assert.Equal("basePeriod", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_LongPeriodEqualShort_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Dymi.Batch(src, out1, shortPeriod: 5, longPeriod: 5));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_MaxPeriodLessThanMin_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Dymi.Batch(src, out1, minPeriod: 5, maxPeriod: 3));
        Assert.Equal("maxPeriod", ex.ParamName);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var d = new Dymi();
        var result = d.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_OutputInRange0To100()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3, seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            double v = d.Update(bar).Value;
            Assert.True(v >= 0.0 && v <= 100.0, $"DYMI={v} out of [0,100]");
        }
    }

    [Fact]
    public void Update_NameIsAccessible()
    {
        var d = new Dymi(14, 5, 10, 3, 30);
        _ = d.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal("Dymi(14,5,10,3,30)", d.Name);
    }

    [Fact]
    public void Update_LastIsAccessible()
    {
        var d = new Dymi();
        var t = new TValue(DateTime.UtcNow, 100.0);
        var result = d.Update(t);
        Assert.Equal(result, d.Last);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        var t = DateTime.UtcNow;
        d.Update(new TValue(t, 100.0), isNew: true);
        var v1 = d.Last;
        d.Update(new TValue(t.AddMinutes(1), 105.0), isNew: true);
        var v2 = d.Last;
        Assert.NotEqual(default, v1);
        Assert.NotEqual(default, v2);
    }

    [Fact]
    public void Update_IsNewFalse_RollsBack()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        double[] prices = [100, 102, 104, 103, 105, 107, 106, 108, 110, 109, 111, 113];
        var t = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            d.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
        }

        // Correction with new price
        d.Update(new TValue(t.AddMinutes(prices.Length), 150.0), isNew: false);
        var corrected1 = d.Last.Value;

        // Same correction again must be idempotent
        d.Update(new TValue(t.AddMinutes(prices.Length), 150.0), isNew: false);
        var corrected2 = d.Last.Value;

        Assert.Equal(corrected1, corrected2, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        double[] prices = [100, 102, 98, 105, 103, 107, 101, 108, 100, 109, 102, 110];
        var t = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            d.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
        }

        // Capture state after last isNew=true
        var baseline = d.Last.Value;

        // Multiple corrections (each restores to prior state)
        d.Update(new TValue(t.AddMinutes(prices.Length), 90.0), isNew: false);
        d.Update(new TValue(t.AddMinutes(prices.Length), 120.0), isNew: false);
        d.Update(new TValue(t.AddMinutes(prices.Length), prices[^1]), isNew: false);

        // Correction with same price as baseline should reproduce baseline
        Assert.Equal(baseline, d.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Reset_ClearsState()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 7);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            d.Update(bar, isNew: true);
        }

        d.Reset();
        Assert.False(d.IsHot);
        Assert.Equal(default, d.Last);
    }

    // ───── D) Warmup / convergence ─────

    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        // Use small periods to make warmup manageable
        var d = new Dymi(basePeriod: 5, shortPeriod: 3, longPeriod: 5, minPeriod: 2, maxPeriod: 10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.3, seed: 11);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        bool everHot = false;
        foreach (var bar in bars.Close)
        {
            d.Update(bar, isNew: true);
            if (d.IsHot)
            {
                everHot = true;
                break;
            }
        }

        Assert.True(everHot, "DYMI should become hot within 200 bars");
    }

    [Fact]
    public void WarmupPeriod_IsLongPeriodPlusMaxPeriod()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        Assert.Equal(40, d.WarmupPeriod); // longPeriod(10) + maxPeriod(30)
    }

    // ───── E) Robustness: NaN / Infinity ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        var t = DateTime.UtcNow;

        // Feed valid values first
        for (int i = 0; i < 20; i++)
        {
            d.Update(new TValue(t.AddMinutes(i), 100.0 + i), isNew: true);
        }

        // Feed NaN — should not produce NaN output
        var result = d.Update(new TValue(t.AddMinutes(20), double.NaN), isNew: true);
        Assert.True(double.IsFinite(result.Value), $"Expected finite, got {result.Value}");
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        var t = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            d.Update(new TValue(t.AddMinutes(i), 100.0 + i), isNew: true);
        }

        var result = d.Update(new TValue(t.AddMinutes(20), double.PositiveInfinity), isNew: true);
        Assert.True(double.IsFinite(result.Value), $"Expected finite, got {result.Value}");
    }

    [Fact]
    public void Update_BatchNaN_AllFinite()
    {
        var d = new Dymi(basePeriod: 14, shortPeriod: 5, longPeriod: 10, minPeriod: 3, maxPeriod: 30);
        var t = DateTime.UtcNow;

        // Mix NaN into sequence
        double[] prices = [100, 101, double.NaN, 102, 103, double.NaN, double.NaN, 104, 105, 106,
                           107, 108, 109, 110, 111, 112, 113, 114, 115, 116];
        for (int i = 0; i < prices.Length; i++)
        {
            var result = d.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ───── F) Consistency: batch == streaming == span ─────

    [Fact]
    public void Consistency_BatchTSeries_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 2001);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var streaming = new Dymi(14, 5, 10, 3, 30);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        // Batch TSeries
        TSeries batchTs = Dymi.Batch(source, 14, 5, 10, 3, 30);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batchTs.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Consistency_BatchSpan_MatchesBatchTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 2002);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries batchTs = Dymi.Batch(source, 14, 5, 10, 3, 30);

        var spanOut = new double[source.Count];
        Dymi.Batch(source.Values, spanOut, 14, 5, 10, 3, 30);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOut[i], Tolerance);
        }
    }

    [Fact]
    public void Consistency_Eventing_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 2003);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var streaming = new Dymi(14, 5, 10, 3, 30);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        // Event-based
        var eventTs = new TSeries();
        var eventDymi = new Dymi(eventTs, 14, 5, 10, 3, 30);
        var eventVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventTs.Add(source[i]);
            eventVals[i] = eventDymi.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], eventVals[i], Tolerance);
        }
    }

    // ───── G) Span API tests ─────

    [Fact]
    public void BatchSpan_EmptySource_DoesNotThrow()
    {
        var src = Array.Empty<double>();
        var out1 = Array.Empty<double>();
        Dymi.Batch(src, out1);
        Assert.Empty(out1);
    }

    [Fact]
    public void BatchSpan_LargeData_NoStackOverflow()
    {
        int n = 2000;
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 9999);
        var bars = gbm.Fetch(n, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var src = bars.Close.Values;
        var out1 = new double[n];

        // Should not throw StackOverflowException — uses ArrayPool for large buffers
        Dymi.Batch(src, out1);

        bool anyFinite = false;
        for (int i = 0; i < n; i++)
        {
            Assert.True(out1[i] >= 0.0 && out1[i] <= 100.0);
            if (double.IsFinite(out1[i]))
            {
                anyFinite = true;
            }
        }

        Assert.True(anyFinite);
    }

    [Fact]
    public void BatchSpan_OutputAlwaysInRange()
    {
        var gbm = new GBM(startPrice: 50.0, mu: 0.05, sigma: 0.5, seed: 777);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var src = bars.Close.Values;
        var out1 = new double[src.Length];

        Dymi.Batch(src, out1);

        for (int i = 0; i < src.Length; i++)
        {
            Assert.True(out1[i] >= 0.0 && out1[i] <= 100.0, $"out1[{i}]={out1[i]} out of [0,100]");
        }
    }

    // ───── H) Chainability ─────

    [Fact]
    public void Chainability_PubFires()
    {
        var source = new TSeries();
        var d = new Dymi(source, 14, 5, 10, 3, 30);

        int count = 0;
        d.Pub += (object? _, in TValueEventArgs e) => count++;

        var t = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(t.AddMinutes(i), 100.0 + i));
        }

        Assert.Equal(10, count);
    }

    [Fact]
    public void Chainability_EventBasedChaining_Works()
    {
        var source = new TSeries();
        var d = new Dymi(source, 14, 5, 10, 3, 30);
        var output = new TSeries();
        d.Pub += (object? _, in TValueEventArgs e) => output.Add(e.Value);

        var t = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            source.Add(new TValue(t.AddMinutes(i), 100.0 + i * 0.5));
        }

        Assert.Equal(30, output.Count);
    }
}
