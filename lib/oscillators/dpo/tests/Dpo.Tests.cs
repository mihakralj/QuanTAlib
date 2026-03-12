using Xunit;

namespace QuanTAlib.Tests;

public sealed class DpoTests
{
    private const int DefaultPeriod = 20;
    private const double Tolerance = 1e-10;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dpo(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Dpo(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var dpo = new Dpo(period: 10);
        Assert.Equal(10, dpo.Period);
        Assert.Equal("Dpo(10)", dpo.Name);
        int expectedDisplacement = (10 / 2) + 1;
        Assert.Equal(expectedDisplacement, dpo.Displacement);
        Assert.Equal(10 + expectedDisplacement, dpo.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var dpo = new Dpo(DefaultPeriod);
        var result = dpo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var dpo = new Dpo(DefaultPeriod);
        dpo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, dpo.Last);
        Assert.False(dpo.IsHot);
        Assert.Equal($"Dpo({DefaultPeriod})", dpo.Name);
    }

    [Fact]
    public void Update_ConstantInput_ZeroDpo()
    {
        var dpo = new Dpo(period: 5);
        int warmup = 5 + (5 / 2) + 1; // period + displacement
        for (int i = 0; i < warmup + 5; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, 50.0));
        }
        // Constant input => SMA == source => DPO == 0
        Assert.Equal(0.0, dpo.Last.Value, Tolerance);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var dpo = new Dpo(DefaultPeriod);
        dpo.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        dpo.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);

        var last = dpo.Last;
        Assert.NotEqual(default, last);
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var dpo = new Dpo(period: 5);
        int warmup = 5 + (5 / 2) + 1;
        for (int i = 0; i < warmup + 2; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        dpo.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected = dpo.Last;

        dpo.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected2 = dpo.Last;

        Assert.Equal(corrected.Value, corrected2.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var dpo = new Dpo(period: 5);
        int warmup = 5 + (5 / 2) + 1;
        double[] data = new double[warmup + 3];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 100 + i * 2;
        }

        for (int i = 0; i < data.Length; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }

        var baseline = dpo.Last.Value;

        dpo.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        dpo.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        dpo.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);

        Assert.Equal(baseline, dpo.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var dpo = new Dpo(DefaultPeriod);
        for (int i = 0; i < 40; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(dpo.IsHot);

        dpo.Reset();
        Assert.False(dpo.IsHot);
        Assert.Equal(default, dpo.Last);
    }

    // ───── D) Warmup / convergence ─────

    [Fact]
    public void IsHot_FlipsAtWarmupPeriod()
    {
        int period = 5;
        int displacement = (period / 2) + 1; // 3
        int warmup = period + displacement; // 8
        var dpo = new Dpo(period);

        for (int i = 0; i < warmup - 1; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(dpo.IsHot, $"Should not be hot at bar {i + 1}");
        }
        dpo.Update(new TValue(DateTime.UtcNow, 108.0));
        Assert.True(dpo.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriodPlusDisplacement()
    {
        var dpo = new Dpo(period: 20);
        Assert.Equal(20 + (20 / 2) + 1, dpo.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var dpo = new Dpo(period: 5);
        int warmup = 5 + (5 / 2) + 1;
        for (int i = 0; i < warmup + 2; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        dpo.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(dpo.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var dpo = new Dpo(period: 5);
        int warmup = 5 + (5 / 2) + 1;
        for (int i = 0; i < warmup + 2; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        dpo.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(dpo.Last.Value));

        dpo.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(dpo.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var dpo = new Dpo(period: 5);
        for (int i = 0; i < 3; i++)
        {
            dpo.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        Assert.True(double.IsFinite(dpo.Last.Value));
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
        var streaming = new Dpo(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Dpo.Batch(source, period);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Dpo.Batch(source.Values, spanOutput, period);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Dpo(eventSource, period);
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
        var ex = Assert.Throws<ArgumentException>(() => Dpo.Batch(source.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroPeriod_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Dpo.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        double[] source = [];
        double[] output = [];
        var ex = Record.Exception(() => Dpo.Batch(source.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        int period = 10;

        TSeries batchTs = Dpo.Batch(source, period);
        var spanOutput = new double[source.Count];
        Dpo.Batch(source.Values, spanOutput, period);

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
        var ex = Record.Exception(() => Dpo.Batch(src.AsSpan(), output.AsSpan(), 5));
        Assert.Null(ex);
    }

    // ───── H) Chainability ─────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var dpo = new Dpo(DefaultPeriod);
        int firedCount = 0;
        dpo.Pub += (object? _, in TValueEventArgs _) => firedCount++;

        dpo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var dpo = new Dpo(source, period: 5);
        var downstream = new TSeries();
        dpo.Pub += (object? _, in TValueEventArgs e) => downstream.Add(e.Value);

        for (int i = 0; i < 15; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.Equal(15, downstream.Count);
    }

    // ───── Calculate ─────

    [Fact]
    public void Calculate_ReturnsResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var (results, indicator) = Dpo.Calculate(source, period: 5);

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

        var streaming = new Dpo(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        var batch = new Dpo(period);
        TSeries batchResults = batch.Update(source);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], Tolerance);
        }
    }

    // ───── Displacement property ─────

    [Fact]
    public void Displacement_Correct_EvenPeriod()
    {
        var dpo = new Dpo(period: 20);
        Assert.Equal(11, dpo.Displacement); // 20/2 + 1
    }

    [Fact]
    public void Displacement_Correct_OddPeriod()
    {
        var dpo = new Dpo(period: 21);
        Assert.Equal(11, dpo.Displacement); // 21/2 + 1 = 10 + 1 (integer division)
    }
}
