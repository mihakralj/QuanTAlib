using Xunit;

namespace QuanTAlib.Tests;

public sealed class CtiTests
{
    private const int DefaultPeriod = 20;
    private const double Tolerance = 1e-7;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_PeriodOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cti(period: 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cti(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Cti(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var cti = new Cti(period: 10);
        Assert.Equal(10, cti.Period);
        Assert.Equal("Cti(10)", cti.Name);
        Assert.Equal(10, cti.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var cti = new Cti(DefaultPeriod);
        var result = cti.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var cti = new Cti(DefaultPeriod);
        cti.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, cti.Last);
        Assert.False(cti.IsHot);
        Assert.Equal($"Cti({DefaultPeriod})", cti.Name);
    }

    [Fact]
    public void Update_OutputBounded_MinusOneToOne()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.3, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var cti = new Cti(DefaultPeriod);

        foreach (var bar in bars.Close)
        {
            cti.Update(bar);
            if (cti.IsHot)
            {
                Assert.InRange(cti.Last.Value, -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void Update_PerfectAscending_CTI_Equals_One()
    {
        // Perfect arithmetic sequence → perfect positive linear correlation → CTI = 1.0
        var cti = new Cti(period: 10);
        for (int i = 1; i <= 15; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, i * 1.0));
        }

        Assert.True(cti.IsHot);
        Assert.Equal(1.0, cti.Last.Value, 10);
    }

    [Fact]
    public void Update_PerfectDescending_CTI_Equals_MinusOne()
    {
        // Perfect descending sequence → CTI = -1.0
        var cti = new Cti(period: 10);
        for (int i = 15; i >= 1; i--)
        {
            cti.Update(new TValue(DateTime.UtcNow, i * 1.0));
        }

        Assert.True(cti.IsHot);
        Assert.Equal(-1.0, cti.Last.Value, 10);
    }

    [Fact]
    public void Update_ConstantInput_CTI_IsNotNaN()
    {
        // Constant price → denomY = 0 → ComputePearson returns 0.0, not NaN
        var cti = new Cti(period: 5);
        for (int i = 0; i < 10; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, 50.0));
        }

        Assert.True(double.IsFinite(cti.Last.Value));
        Assert.Equal(0.0, cti.Last.Value, Tolerance);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var cti = new Cti(DefaultPeriod);
        cti.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        cti.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        Assert.NotEqual(default, cti.Last);
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var cti = new Cti(period: 5);
        for (int i = 0; i < 6; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        // Bar correction: rewrite last bar
        cti.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected = cti.Last;

        // Same correction again → same result
        cti.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false);
        var corrected2 = cti.Last;

        Assert.Equal(corrected.Value, corrected2.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var cti = new Cti(period: 5);
        double[] data = [100, 102, 104, 106, 108, 110];

        for (int i = 0; i < data.Length; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, data[i]), isNew: true);
        }

        var baseline = cti.Last.Value;

        // Apply three corrections, then restore original value
        cti.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        cti.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        cti.Update(new TValue(DateTime.UtcNow, data[^1]), isNew: false);

        Assert.Equal(baseline, cti.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var cti = new Cti(DefaultPeriod);
        for (int i = 0; i < 25; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(cti.IsHot);

        cti.Reset();
        Assert.False(cti.IsHot);
        Assert.Equal(default, cti.Last);
    }

    // ───── D) Warmup/convergence ─────

    [Fact]
    public void IsHot_FlipsAfterPeriodBars()
    {
        var cti = new Cti(period: 5);
        for (int i = 0; i < 4; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(cti.IsHot);
        }
        cti.Update(new TValue(DateTime.UtcNow, 104.0));
        Assert.True(cti.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var cti = new Cti(period: 20);
        Assert.Equal(20, cti.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var cti = new Cti(period: 5);
        for (int i = 0; i < 6; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        _ = cti.Last.Value;
        cti.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(cti.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var cti = new Cti(period: 5);
        for (int i = 0; i < 6; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        cti.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(cti.Last.Value));

        cti.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(cti.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var cti = new Cti(period: 5);
        for (int i = 0; i < 3; i++)
        {
            cti.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        Assert.True(double.IsFinite(cti.Last.Value));
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
        var streaming = new Cti(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Cti.Batch(source, period);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Cti.Batch(source.Values, spanOutput, period);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Cti(eventSource, period);
        var eventResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        for (int i = period; i < source.Count; i++)
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
        var ex = Assert.Throws<ArgumentException>(() => Cti.Batch(source.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodOne_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Cti.Batch(source.AsSpan(), output.AsSpan(), 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        double[] source = [];
        double[] output = [];
        var ex = Record.Exception(() => Cti.Batch(source.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;
        int period = 10;

        TSeries batchTs = Cti.Batch(source, period);
        var spanOutput = new double[source.Count];
        Cti.Batch(source.Values, spanOutput, period);

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
        var ex = Record.Exception(() => Cti.Batch(src.AsSpan(), output.AsSpan(), 5));
        Assert.Null(ex);
        Assert.True(output.All(double.IsFinite));
    }

    [Fact]
    public void Batch_Span_PerfectAscending_OutputsOne()
    {
        int period = 5;
        double[] src = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var output = new double[src.Length];
        Cti.Batch(src.AsSpan(), output.AsSpan(), period);

        // After warmup, all values should be 1.0
        for (int i = period - 1; i < src.Length; i++)
        {
            Assert.Equal(1.0, output[i], 10);
        }
    }

    [Fact]
    public void Batch_Span_LargeDataset_NoStackOverflow()
    {
        double[] src = new double[10000];
        double[] output = new double[10000];
        for (int i = 0; i < src.Length; i++)
        {
            src[i] = 100.0 + i * 0.01;
        }
        var ex = Record.Exception(() => Cti.Batch(src.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Null(ex);
    }

    // ───── H) Chainability ─────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var cti = new Cti(DefaultPeriod);
        int firedCount = 0;
        cti.Pub += (object? _, in TValueEventArgs _) => firedCount++;

        cti.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var cti = new Cti(source, period: 5);
        var downstream = new TSeries();
        cti.Pub += (object? _, in TValueEventArgs e) => downstream.Add(e.Value);

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

        var (results, indicator) = Cti.Calculate(source, period: 5);

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

        var streaming = new Cti(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        var batch = new Cti(period);
        TSeries batchResults = batch.Update(source);

        for (int i = period; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], Tolerance);
        }
    }
}
