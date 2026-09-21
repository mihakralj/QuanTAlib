using Xunit;

namespace QuanTAlib.Tests;

public sealed class BbiTests
{
    private const double Tolerance = 1e-10;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_P1Zero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbi(p1: 0));
        Assert.Equal("p1", ex.ParamName);
    }

    [Fact]
    public void Constructor_P2Negative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbi(p2: -1));
        Assert.Equal("p2", ex.ParamName);
    }

    [Fact]
    public void Constructor_P3Zero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbi(p3: 0));
        Assert.Equal("p3", ex.ParamName);
    }

    [Fact]
    public void Constructor_P4Negative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Bbi(p4: -5));
        Assert.Equal("p4", ex.ParamName);
    }

    [Fact]
    public void Constructor_Defaults_SetsProperties()
    {
        var bbi = new Bbi();
        Assert.Equal("Bbi(3,6,12,24)", bbi.Name);
        Assert.Equal(24, bbi.WarmupPeriod);
        Assert.False(bbi.IsHot);
    }

    [Fact]
    public void Constructor_CustomParams_SetsName()
    {
        var bbi = new Bbi(p1: 5, p2: 10, p3: 20, p4: 40);
        Assert.Equal("Bbi(5,10,20,40)", bbi.Name);
        Assert.Equal(40, bbi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsMaxPeriod()
    {
        var bbi = new Bbi(p1: 2, p2: 7, p3: 14, p4: 30);
        Assert.Equal(30, bbi.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var bbi = new Bbi();
        var result = bbi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var bbi = new Bbi();
        bbi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, bbi.Last);
        Assert.False(bbi.IsHot);
        Assert.Equal("Bbi(3,6,12,24)", bbi.Name);
    }

    [Fact]
    public void Update_ConstantInput_BbiEqualsConstant()
    {
        // When all values are constant, every SMA == constant, so BBI == constant.
        var bbi = new Bbi(p1: 3, p2: 6, p3: 12, p4: 24);
        for (int i = 0; i < 30; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, 50.0));
        }
        Assert.Equal(50.0, bbi.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_SingleBar_ValueIsFinite()
    {
        var bbi = new Bbi();
        var result = bbi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_KnownValue_FirstBar()
    {
        // With 1 bar at 100.0, all 4 SMAs = 100.0 → BBI = 100.0
        var bbi = new Bbi(p1: 3, p2: 6, p3: 12, p4: 24);
        var result = bbi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, result.Value, Tolerance);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var bbi = new Bbi();
        bbi.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        bbi.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        Assert.NotEqual(default, bbi.Last);
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var bbi = new Bbi(p1: 3, p2: 6, p3: 12, p4: 24);
        for (int i = 0; i < 25; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        // Bar correction: rewrite last bar
        bbi.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        double corrected1 = bbi.Last.Value;

        // Same correction again — must produce identical result
        bbi.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        double corrected2 = bbi.Last.Value;

        Assert.Equal(corrected1, corrected2, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreBaseline()
    {
        var bbi = new Bbi(p1: 3, p2: 6, p3: 12, p4: 24);
        double[] data = [100, 102, 104, 106, 108, 110, 112, 114, 116, 118,
                         120, 122, 124, 126, 128, 130, 132, 134, 136, 138,
                         140, 142, 144, 146, 148];

        for (int i = 0; i < data.Length; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }
        double baseline = bbi.Last.Value;

        // Correct last bar several times, restore original
        bbi.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        bbi.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        bbi.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);

        Assert.Equal(baseline, bbi.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var bbi = new Bbi();
        for (int i = 0; i < 30; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(bbi.IsHot);

        bbi.Reset();
        Assert.False(bbi.IsHot);
        Assert.Equal(default, bbi.Last);
    }

    // ───── D) Warmup/convergence ─────

    [Fact]
    public void IsHot_FlipsAtWarmupPeriod()
    {
        var bbi = new Bbi(p1: 3, p2: 6, p3: 12, p4: 24);
        // IsHot = (index >= WarmupPeriod) = (index >= 24)
        for (int i = 0; i < 23; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(bbi.IsHot);
        }
        bbi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(bbi.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsMaxPeriod()
    {
        var bbi = new Bbi(p1: 3, p2: 6, p3: 12, p4: 24);
        Assert.Equal(24, bbi.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var bbi = new Bbi(p1: 3, p2: 6, p3: 12, p4: 24);
        for (int i = 0; i < 25; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        bbi.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(bbi.Last.Value));
    }

    [Fact]
    public void Update_PositiveInfinity_UsesLastValid()
    {
        var bbi = new Bbi();
        for (int i = 0; i < 25; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        bbi.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(bbi.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValid()
    {
        var bbi = new Bbi();
        for (int i = 0; i < 25; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        bbi.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(bbi.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var bbi = new Bbi();
        for (int i = 0; i < 5; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        Assert.True(double.IsFinite(bbi.Last.Value));
    }

    // ───── F) Consistency (streaming == batch TSeries == batch Span == eventing) ─────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int p1 = 3, p2 = 6, p3 = 12, p4 = 24;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // 1. Streaming
        var streaming = new Bbi(p1, p2, p3, p4);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Bbi.Batch(source, p1, p2, p3, p4);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Bbi.Batch(source.Values, spanOutput, p1, p2, p3, p4);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Bbi(eventSource, p1, p2, p3, p4);
        var eventResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], Tolerance);
            Assert.Equal(streamResults[i], spanOutput[i], Tolerance);
            Assert.Equal(streamResults[i], eventResults[i], Tolerance);
        }
    }

    // ───── G) Span API tests ─────

    [Fact]
    public void Batch_Span_MismatchedLength_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Bbi.Batch(source.AsSpan(), output.AsSpan()));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroP1_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Bbi.Batch(source.AsSpan(), output.AsSpan(), p1: 0));
        Assert.Equal("p1", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroP2_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Bbi.Batch(source.AsSpan(), output.AsSpan(), p2: 0));
        Assert.Equal("p2", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroP3_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Bbi.Batch(source.AsSpan(), output.AsSpan(), p3: 0));
        Assert.Equal("p3", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroP4_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Bbi.Batch(source.AsSpan(), output.AsSpan(), p4: 0));
        Assert.Equal("p4", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        double[] source = [];
        double[] output = [];
        var ex = Record.Exception(() => Bbi.Batch(source.AsSpan(), output.AsSpan()));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries batchTs = Bbi.Batch(source);
        var spanOutput = new double[source.Count];
        Bbi.Batch(source.Values, spanOutput);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] src = [1, 2, double.NaN, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25];
        var output = new double[src.Length];
        var ex = Record.Exception(() => Bbi.Batch(src.AsSpan(), output.AsSpan()));
        Assert.Null(ex);
        Assert.All(output, v => Assert.True(double.IsFinite(v)));
    }

    [Fact]
    public void Batch_Span_LargeInput_NoStackOverflow()
    {
        int n = 10_000;
        var source = new double[n];
        var output = new double[n];
        for (int i = 0; i < n; i++) { source[i] = 100.0 + (i * 0.01); }
        var ex = Record.Exception(() => Bbi.Batch(source.AsSpan(), output.AsSpan()));
        Assert.Null(ex);
    }

    // ───── H) Chainability ─────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var bbi = new Bbi();
        int firedCount = 0;
        bbi.Pub += (object? _, in TValueEventArgs _) => firedCount++;

        bbi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var bbi = new Bbi(source);
        var downstream = new TSeries();
        bbi.Pub += (object? _, in TValueEventArgs e) => downstream.Add(e.Value);

        for (int i = 0; i < 30; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.Equal(30, downstream.Count);
    }

    // ───── Calculate ─────

    [Fact]
    public void Calculate_ReturnsResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var (results, indicator) = Bbi.Calculate(source);

        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ───── Update(TSeries) ─────

    [Fact]
    public void UpdateTSeries_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Bbi();
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        var batch = new Bbi();
        TSeries batchResults = batch.Update(source);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], Tolerance);
        }
    }
}
