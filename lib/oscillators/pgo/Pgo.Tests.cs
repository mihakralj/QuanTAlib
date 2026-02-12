using Xunit;

namespace QuanTAlib.Tests;

public sealed class PgoTests
{
    private const int DefaultPeriod = 14;
    private const double Tolerance = 1e-10;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_PeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pgo(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pgo(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var pgo = new Pgo(period: 10);
        Assert.Equal(10, pgo.Period);
        Assert.Equal("Pgo(10)", pgo.Name);
        Assert.Equal(10, pgo.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var pgo = new Pgo(DefaultPeriod);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        var result = pgo.Update(bar);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var pgo = new Pgo(DefaultPeriod);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        pgo.Update(bar);
        Assert.NotEqual(default, pgo.Last);
        Assert.False(pgo.IsHot);
        Assert.Equal($"Pgo({DefaultPeriod})", pgo.Name);
    }

    [Fact]
    public void Update_ConstantBars_ZeroPgo()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 10; i++)
        {
            pgo.Update(new TBar(DateTime.UtcNow, 50, 50, 50, 50, 100));
        }
        // Constant bars have TR=0, SMA=close => PGO = 0/0 => 0.0 (guard)
        Assert.Equal(0.0, pgo.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_RisingClose_PositivePgo()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 10; i++)
        {
            double c = 100.0 + i;
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 2, c - 2, c, 100));
        }
        // Rising close above SMA => positive PGO
        Assert.True(pgo.Last.Value > 0);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var pgo = new Pgo(DefaultPeriod);
        pgo.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000), isNew: true);
        pgo.Update(new TBar(DateTime.UtcNow, 102, 110, 98, 108, 1000), isNew: true);

        var last = pgo.Last;
        Assert.NotEqual(default, last);
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 6; i++)
        {
            double c = 100.0 + i;
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 2, c - 2, c, 100), isNew: true);
        }

        // Bar correction: rewrite last bar
        pgo.Update(new TBar(DateTime.UtcNow, 104, 107, 103, 105, 100), isNew: false);
        var corrected = pgo.Last;

        // Repeat same correction — should produce identical result
        pgo.Update(new TBar(DateTime.UtcNow, 104, 107, 103, 105, 100), isNew: false);
        var corrected2 = pgo.Last;

        Assert.Equal(corrected.Value, corrected2.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var pgo = new Pgo(period: 5);
        TBar[] bars =
        [
            new(DateTime.UtcNow, 99, 102, 98, 100, 100),
            new(DateTime.UtcNow, 101, 104, 100, 102, 100),
            new(DateTime.UtcNow, 103, 106, 102, 104, 100),
            new(DateTime.UtcNow, 105, 108, 104, 106, 100),
            new(DateTime.UtcNow, 107, 110, 106, 108, 100),
            new(DateTime.UtcNow, 109, 112, 108, 110, 100),
        ];

        for (int i = 0; i < bars.Length; i++)
        {
            pgo.Update(bars[i], isNew: true);
        }

        double baseline = pgo.Last.Value;

        // Correct last bar 3 times, then restore original
        pgo.Update(new TBar(DateTime.UtcNow, 120, 130, 110, 999, 100), isNew: false);
        pgo.Update(new TBar(DateTime.UtcNow, 120, 130, 110, 888, 100), isNew: false);
        pgo.Update(bars[^1], isNew: false);

        Assert.Equal(baseline, pgo.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pgo = new Pgo(DefaultPeriod);
        for (int i = 0; i < 20; i++)
        {
            double c = 100.0 + i;
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 2, c - 2, c, 100));
        }
        Assert.True(pgo.IsHot);

        pgo.Reset();
        Assert.False(pgo.IsHot);
        Assert.Equal(default, pgo.Last);
    }

    // ───── D) Warmup / convergence ─────

    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 4; i++)
        {
            double c = 100.0 + i;
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 2, c - 2, c, 100));
            Assert.False(pgo.IsHot);
        }
        pgo.Update(new TBar(DateTime.UtcNow, 103, 106, 102, 104, 100));
        Assert.True(pgo.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var pgo = new Pgo(period: 20);
        Assert.Equal(20, pgo.WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 6; i++)
        {
            double c = 100.0 + i;
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 2, c - 2, c, 100));
        }

        pgo.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 100));
        Assert.True(double.IsFinite(pgo.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 6; i++)
        {
            double c = 100.0 + i;
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 2, c - 2, c, 100));
        }

        pgo.Update(new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity,
            double.PositiveInfinity, double.PositiveInfinity, 100));
        Assert.True(double.IsFinite(pgo.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 3; i++)
        {
            pgo.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));
        }
        Assert.True(double.IsFinite(pgo.Last.Value));
    }

    // ───── F) Consistency (4 modes match) ─────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Streaming (TBar)
        var streaming = new Pgo(period);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        // 2. Batch TBarSeries
        TSeries batchSeries = Pgo.Batch(bars, period);

        // 3. Batch Span
        var spanOutput = new double[bars.Count];
        Pgo.Batch(bars.High.Values, bars.Low.Values, bars.Close.Values, spanOutput, period);

        // Compare all modes
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], Tolerance);
            Assert.Equal(streamResults[i], spanOutput[i], Tolerance);
        }
    }

    // ───── G) Span API tests ─────

    [Fact]
    public void Batch_Span_MismatchedLength_ThrowsArgumentException()
    {
        var high = new double[10];
        var low = new double[10];
        var close = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Pgo.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Equal("destination", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroPeriod_ThrowsArgumentException()
    {
        var high = new double[10];
        var low = new double[10];
        var close = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() =>
            Pgo.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        double[] high = [];
        double[] low = [];
        double[] close = [];
        double[] output = [];
        var ex = Record.Exception(() =>
            Pgo.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), output.AsSpan(), DefaultPeriod));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_Span_MatchesTBarSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 10;

        TSeries batchTs = Pgo.Batch(bars, period);
        var spanOutput = new double[bars.Count];
        Pgo.Batch(bars.High.Values, bars.Low.Values, bars.Close.Values, spanOutput, period);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Batch_Span_NaN_Handled()
    {
        double[] high = [102, 104, double.NaN, 108, 110, 112, 114, 116, 118, 120];
        double[] low = [98, 100, double.NaN, 104, 106, 108, 110, 112, 114, 116];
        double[] close = [100, 102, double.NaN, 106, 108, 110, 112, 114, 116, 118];
        var output = new double[close.Length];
        var ex = Record.Exception(() =>
            Pgo.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), output.AsSpan(), 5));
        Assert.Null(ex);
    }

    // ───── H) Chainability ─────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var pgo = new Pgo(DefaultPeriod);
        int firedCount = 0;
        pgo.Pub += (object? _, in TValueEventArgs _) => firedCount++;

        pgo.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var pgo = new Pgo(period: 5);
        var downstream = new TSeries();
        pgo.Pub += (object? _, in TValueEventArgs e) => downstream.Add(e.Value);

        for (int i = 0; i < 10; i++)
        {
            double c = 100.0 + i;
            pgo.Update(new TBar(DateTime.UtcNow, c - 1, c + 2, c - 2, c, 100));
        }

        Assert.Equal(10, downstream.Count);
    }

    // ───── Calculate ─────

    [Fact]
    public void Calculate_ReturnsResultsAndHotIndicator()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Pgo.Calculate(bars, period: 5);

        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ───── Update(TBarSeries) ─────

    [Fact]
    public void UpdateTBarSeries_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 10;

        var streaming = new Pgo(period);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        var batch = new Pgo(period);
        TSeries batchResults = batch.Update(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], Tolerance);
        }
    }

    // ───── TValue overload ─────

    [Fact]
    public void Update_TValue_ReturnsResult()
    {
        var pgo = new Pgo(period: 5);
        for (int i = 0; i < 10; i++)
        {
            pgo.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        // TValue creates synthetic bars (O=H=L=C=val). TR = |val - prevClose| > 0
        // when values change, so ATR > 0 and PGO is nonzero for rising prices.
        Assert.True(double.IsFinite(pgo.Last.Value));
        Assert.True(pgo.Last.Value > 0, "Rising TValue inputs should produce positive PGO");
    }
}
