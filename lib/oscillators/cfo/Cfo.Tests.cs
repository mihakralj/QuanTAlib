using Xunit;

namespace QuanTAlib.Tests;

public sealed class CfoTests
{
    private const int DefaultPeriod = 14;
    private const double Tolerance = 1e-10;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cfo(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cfo(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var cfo = new Cfo(period: 10);
        Assert.Equal(10, cfo.Period);
        Assert.Equal("Cfo(10)", cfo.Name);
        Assert.Equal(10, cfo.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var cfo = new Cfo(DefaultPeriod);
        var result = cfo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var cfo = new Cfo(DefaultPeriod);
        cfo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, cfo.Last);
        Assert.False(cfo.IsHot);
        Assert.Equal($"Cfo({DefaultPeriod})", cfo.Name);
    }

    [Fact]
    public void Update_ConstantInput_ZeroCfo()
    {
        var cfo = new Cfo(period: 5);
        for (int i = 0; i < 10; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 50.0));
        }
        // Constant input => TSF == source => CFO == 0
        Assert.Equal(0.0, cfo.Last.Value, Tolerance);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var cfo = new Cfo(DefaultPeriod);
        cfo.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        cfo.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);

        var last = cfo.Last;
        // Should have two distinct updates
        Assert.NotEqual(default, last);
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var cfo = new Cfo(period: 5);
        for (int i = 0; i < 6; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        // Bar correction: rewrite last bar
        cfo.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected = cfo.Last;

        // Repeat same correction — should produce identical result
        cfo.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected2 = cfo.Last;

        Assert.Equal(corrected.Value, corrected2.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var cfo = new Cfo(period: 5);
        double[] data = [100, 102, 104, 106, 108, 110];

        for (int i = 0; i < data.Length; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }

        var baseline = cfo.Last.Value;

        // Correct last bar 3 times, then restore original
        cfo.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        cfo.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        cfo.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);

        Assert.Equal(baseline, cfo.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var cfo = new Cfo(DefaultPeriod);
        for (int i = 0; i < 20; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(cfo.IsHot);

        cfo.Reset();
        Assert.False(cfo.IsHot);
        Assert.Equal(default, cfo.Last);
    }

    // ───── D) Warmup / convergence ─────

    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        var cfo = new Cfo(period: 5);
        for (int i = 0; i < 4; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(cfo.IsHot);
        }
        cfo.Update(new TValue(DateTime.UtcNow, 104.0));
        Assert.True(cfo.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var cfo = new Cfo(period: 20);
        Assert.Equal(20, cfo.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var cfo = new Cfo(period: 5);
        for (int i = 0; i < 6; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        cfo.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(cfo.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var cfo = new Cfo(period: 5);
        for (int i = 0; i < 6; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        cfo.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(cfo.Last.Value));

        cfo.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(cfo.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var cfo = new Cfo(period: 5);
        for (int i = 0; i < 3; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        // No exception thrown; result should be finite (falls back to 0.0)
        Assert.True(double.IsFinite(cfo.Last.Value));
    }

    // ───── F) Consistency (4 modes match) ─────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // 1. Streaming
        var streaming = new Cfo(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Cfo.Batch(source, period);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Cfo.Batch(source.Values, spanOutput, period);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Cfo(eventSource, period);
        var eventResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        // Compare all modes
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
        var ex = Assert.Throws<ArgumentException>(() => Cfo.Batch(source.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroPeriod_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Cfo.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        double[] source = [];
        double[] output = [];
        var ex = Record.Exception(() => Cfo.Batch(source.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        int period = 10;

        TSeries batchTs = Cfo.Batch(source, period);
        var spanOutput = new double[source.Count];
        Cfo.Batch(source.Values, spanOutput, period);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] src = [1, 2, double.NaN, 4, 5, 6, 7, 8, 9, 10];
        var output = new double[src.Length];
        var ex = Record.Exception(() => Cfo.Batch(src.AsSpan(), output.AsSpan(), 5));
        Assert.Null(ex);
    }

    // ───── H) Chainability ─────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var cfo = new Cfo(DefaultPeriod);
        int firedCount = 0;
        cfo.Pub += (object? _, in TValueEventArgs _) => firedCount++;

        cfo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var cfo = new Cfo(source, period: 5);
        var downstream = new TSeries();
        cfo.Pub += (object? _, in TValueEventArgs e) => downstream.Add(e.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.Equal(10, downstream.Count);
    }

    // ───── Calculate ─────

    [Fact]
    public void Calculate_ReturnsResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var (results, indicator) = Cfo.Calculate(source, period: 5);

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
        int period = 10;

        var streaming = new Cfo(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        var batch = new Cfo(period);
        TSeries batchResults = batch.Update(source);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], Tolerance);
        }
    }

    // ───── Division by zero ─────

    [Fact]
    public void Update_ZeroSource_ReturnsNaN()
    {
        var cfo = new Cfo(period: 3);
        for (int i = 0; i < 3; i++)
        {
            cfo.Update(new TValue(DateTime.UtcNow, 0.0));
        }
        Assert.True(double.IsNaN(cfo.Last.Value));
    }
}
