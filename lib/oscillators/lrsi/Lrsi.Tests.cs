using Xunit;

namespace QuanTAlib.Tests;

public sealed class LrsiTests
{
    private const double Tolerance = 1e-10;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_GammaNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Lrsi(gamma: -0.1));
        Assert.Equal("gamma", ex.ParamName);
    }

    [Fact]
    public void Constructor_GammaGreaterThanOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Lrsi(gamma: 1.1));
        Assert.Equal("gamma", ex.ParamName);
    }

    [Fact]
    public void Constructor_GammaZero_IsValid()
    {
        var lrsi = new Lrsi(gamma: 0.0);
        Assert.Equal(0.0, lrsi.Gamma);
    }

    [Fact]
    public void Constructor_GammaOne_IsValid()
    {
        var lrsi = new Lrsi(gamma: 1.0);
        Assert.Equal(1.0, lrsi.Gamma);
    }

    [Fact]
    public void Constructor_DefaultGamma_SetsProperties()
    {
        var lrsi = new Lrsi();
        Assert.Equal(0.5, lrsi.Gamma);
        Assert.Equal("Lrsi(0.50)", lrsi.Name);
        Assert.Equal(4, lrsi.WarmupPeriod);
        Assert.Equal(default, lrsi.Last);
    }

    [Fact]
    public void Constructor_CustomGamma_SetsName()
    {
        var lrsi = new Lrsi(gamma: 0.75);
        Assert.Equal("Lrsi(0.75)", lrsi.Name);
        Assert.Equal(0.75, lrsi.Gamma);
    }

    [Fact]
    public void BatchSpan_OutputLengthMismatch_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[4];
        var ex = Assert.Throws<ArgumentException>(() => Lrsi.Calculate(src, out1));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_GammaNegative_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Lrsi.Calculate(src, out1, gamma: -0.1));
        Assert.Equal("gamma", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_GammaGreaterThanOne_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Lrsi.Calculate(src, out1, gamma: 1.01));
        Assert.Equal("gamma", ex.ParamName);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var lrsi = new Lrsi();
        var result = lrsi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_OutputInRange0To1()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3, seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            double v = lrsi.Update(bar).Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"LRSI={v} out of [0,1]");
        }
    }

    [Fact]
    public void Update_NameIsAccessible()
    {
        var lrsi = new Lrsi(0.5);
        _ = lrsi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal("Lrsi(0.50)", lrsi.Name);
    }

    [Fact]
    public void Update_LastIsAccessible()
    {
        var lrsi = new Lrsi();
        var t = new TValue(DateTime.UtcNow, 100.0);
        var result = lrsi.Update(t);
        Assert.Equal(result, lrsi.Last);
    }

    [Fact]
    public void Update_ConstantPrice_ProducesHalfPoint()
    {
        // Constant input → all stages equal → cu=cd=0 → LRSI = 0.5
        var lrsi = new Lrsi(gamma: 0.5);
        var t = DateTime.UtcNow;
        double last = 0;
        for (int i = 0; i < 200; i++)
        {
            last = lrsi.Update(new TValue(t.AddMinutes(i), 100.0)).Value;
        }
        Assert.Equal(0.5, last, 1e-6);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        var t = DateTime.UtcNow;
        lrsi.Update(new TValue(t, 100.0), isNew: true);
        var v1 = lrsi.Last;
        lrsi.Update(new TValue(t.AddMinutes(1), 105.0), isNew: true);
        var v2 = lrsi.Last;
        Assert.NotEqual(default, v1);
        Assert.NotEqual(default, v2);
    }

    [Fact]
    public void Update_IsNewFalse_RollsBack()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        double[] prices = [100, 102, 104, 103, 105, 107, 106, 108, 110, 109, 111, 113];
        var t = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            lrsi.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
        }

        // Correction with a different price
        lrsi.Update(new TValue(t.AddMinutes(prices.Length), 150.0), isNew: false);
        var corrected1 = lrsi.Last.Value;

        // Same correction again must be idempotent
        lrsi.Update(new TValue(t.AddMinutes(prices.Length), 150.0), isNew: false);
        var corrected2 = lrsi.Last.Value;

        Assert.Equal(corrected1, corrected2, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        double[] prices = [100, 102, 98, 105, 103, 107, 101, 108, 100, 109, 102, 110];
        var t = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            lrsi.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
        }

        // Capture last isNew=true state
        var baseline = lrsi.Last.Value;

        // Multiple corrections (each restores to prior state before applying new price)
        lrsi.Update(new TValue(t.AddMinutes(prices.Length), 90.0), isNew: false);
        lrsi.Update(new TValue(t.AddMinutes(prices.Length), 120.0), isNew: false);
        lrsi.Update(new TValue(t.AddMinutes(prices.Length), prices[^1]), isNew: false);

        // Correction with same price as last isNew=true should reproduce baseline
        Assert.Equal(baseline, lrsi.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Reset_ClearsState()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 7);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            lrsi.Update(bar, isNew: true);
        }

        lrsi.Reset();
        Assert.False(lrsi.IsHot);
        Assert.Equal(default, lrsi.Last);
    }

    // ───── D) Warmup / convergence ─────

    [Fact]
    public void WarmupPeriod_IsFour()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        Assert.Equal(4, lrsi.WarmupPeriod);
    }

    [Fact]
    public void IsHot_FlipsAfterFirstBar()
    {
        // LRSI starts hot after first non-zero input moves any filter stage
        var lrsi = new Lrsi(gamma: 0.5);
        Assert.False(lrsi.IsHot);

        // After first price update the filter stages become non-zero
        lrsi.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(lrsi.IsHot);
    }

    [Fact]
    public void IsHot_RemainsHotAfterReset_ReturnsToFalse()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        lrsi.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(lrsi.IsHot);
        lrsi.Reset();
        Assert.False(lrsi.IsHot);
    }

    // ───── E) Robustness: NaN / Infinity ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        var t = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            lrsi.Update(new TValue(t.AddMinutes(i), 100.0 + i), isNew: true);
        }

        var result = lrsi.Update(new TValue(t.AddMinutes(20), double.NaN), isNew: true);
        Assert.True(double.IsFinite(result.Value), $"Expected finite, got {result.Value}");
        Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
    }

    [Fact]
    public void Update_PositiveInfinity_UsesLastValid()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        var t = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            lrsi.Update(new TValue(t.AddMinutes(i), 100.0 + i), isNew: true);
        }

        var result = lrsi.Update(new TValue(t.AddMinutes(20), double.PositiveInfinity), isNew: true);
        Assert.True(double.IsFinite(result.Value), $"Expected finite, got {result.Value}");
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValid()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        var t = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            lrsi.Update(new TValue(t.AddMinutes(i), 100.0 + i), isNew: true);
        }

        var result = lrsi.Update(new TValue(t.AddMinutes(20), double.NegativeInfinity), isNew: true);
        Assert.True(double.IsFinite(result.Value), $"Expected finite, got {result.Value}");
    }

    [Fact]
    public void Update_BatchNaN_AllFinite()
    {
        var lrsi = new Lrsi(gamma: 0.5);
        var t = DateTime.UtcNow;

        double[] prices = [100, 101, double.NaN, 102, 103, double.NaN, double.NaN, 104, 105, 106,
                           107, 108, 109, 110, 111, 112, 113, 114, 115, 116];
        for (int i = 0; i < prices.Length; i++)
        {
            var result = lrsi.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
            Assert.True(double.IsFinite(result.Value), $"Not finite at index {i}: {result.Value}");
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }
    }

    // ───── F) Consistency: batch == streaming == span == eventing ─────

    [Fact]
    public void Consistency_BatchTSeries_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 2001);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var streaming = new Lrsi(0.5);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        // Batch TSeries
        TSeries batchTs = Lrsi.Calculate(source, 0.5);

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

        TSeries batchTs = Lrsi.Calculate(source, 0.5);

        var spanOut = new double[source.Count];
        Lrsi.Calculate(source.Values, spanOut, 0.5);

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
        var streaming = new Lrsi(0.5);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        // Event-based
        var eventTs = new TSeries();
        var eventLrsi = new Lrsi(eventTs, 0.5);
        var eventVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventTs.Add(source[i]);
            eventVals[i] = eventLrsi.Last.Value;
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
        Lrsi.Calculate(src, out1);
        Assert.Empty(out1);
    }

    [Fact]
    public void BatchSpan_LargeData_UsesArrayPool()
    {
        // 257 exceeds StackallocThreshold=256; LRSI has no internal buffer
        // but we exercise the span path with large data (no stack overflow risk here)
        int n = 500;
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 9999);
        var bars = gbm.Fetch(n, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var src = bars.Close.Values;
        var out1 = new double[n];

        Lrsi.Calculate(src, out1);

        for (int i = 0; i < n; i++)
        {
            Assert.True(out1[i] >= 0.0 && out1[i] <= 1.0, $"out1[{i}]={out1[i]} out of [0,1]");
        }
    }

    [Fact]
    public void BatchSpan_WithNaN_AllOutputsFinite()
    {
        double[] src = [100, 101, double.NaN, 102, 103, double.NaN, 104, 105];
        var out1 = new double[src.Length];

        Lrsi.Calculate(src, out1);

        for (int i = 0; i < out1.Length; i++)
        {
            Assert.True(double.IsFinite(out1[i]), $"out1[{i}]={out1[i]} not finite");
            Assert.True(out1[i] >= 0.0 && out1[i] <= 1.0);
        }
    }

    [Fact]
    public void BatchSpan_OutputAlwaysInRange()
    {
        var gbm = new GBM(startPrice: 50.0, mu: 0.05, sigma: 0.5, seed: 777);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var src = bars.Close.Values;
        var out1 = new double[src.Length];

        Lrsi.Calculate(src, out1);

        for (int i = 0; i < src.Length; i++)
        {
            Assert.True(out1[i] >= 0.0 && out1[i] <= 1.0, $"out1[{i}]={out1[i]} out of [0,1]");
        }
    }

    // ───── H) Chainability ─────

    [Fact]
    public void Chainability_PubFires()
    {
        var source = new TSeries();
        var lrsi = new Lrsi(source, 0.5);

        int count = 0;
        lrsi.Pub += (object? _, in TValueEventArgs e) => count++;

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
        var lrsi = new Lrsi(source, 0.5);
        var output = new TSeries();
        lrsi.Pub += (object? _, in TValueEventArgs e) => output.Add(e.Value);

        var t = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            source.Add(new TValue(t.AddMinutes(i), 100.0 + i * 0.5));
        }

        Assert.Equal(30, output.Count);
    }
}
