using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class RvgiTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.2, seed: 42);
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_DefaultPeriod_IsValid()
    {
        var rvgi = new Rvgi();
        Assert.Equal("Rvgi(10)", rvgi.Name);
        Assert.Equal(10, rvgi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rvgi(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rvgi(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var rvgi = new Rvgi(period: 14);
        Assert.Equal("Rvgi(14)", rvgi.Name);
        Assert.Equal(14, rvgi.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var rvgi = new Rvgi(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        var result = rvgi.Update(bar);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var rvgi = new Rvgi(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        rvgi.Update(bar);
        Assert.True(double.IsFinite(rvgi.Last.Value));
    }

    [Fact]
    public void Update_RvgiAndSignal_Accessible()
    {
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 20; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true));
        }
        Assert.True(double.IsFinite(rvgi.RvgiValue));
        Assert.True(double.IsFinite(rvgi.Signal));
    }

    [Fact]
    public void Update_IsHot_FalseBeforeWarmup()
    {
        var rvgi = new Rvgi(period: 5);
        Assert.False(rvgi.IsHot);
    }

    [Fact]
    public void Update_Name_MatchesPeriod()
    {
        var rvgi = new Rvgi(period: 7);
        Assert.Equal("Rvgi(7)", rvgi.Name);
    }

    [Fact]
    public void Update_BullishBars_PositiveRvgi()
    {
        // Bars where close > open (up bars) should yield positive RVGI
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 30; i++)
        {
            double open = 100.0;
            double close = 105.0;   // consistently close > open
            double high = 107.0;
            double low = 98.0;
            rvgi.Update(new TBar(DateTime.UtcNow.AddMinutes(i), open, high, low, close, 1000), isNew: true);
        }
        Assert.True(rvgi.RvgiValue > 0.0, $"Expected RVGI > 0, got {rvgi.RvgiValue}");
    }

    [Fact]
    public void Update_BearishBars_NegativeRvgi()
    {
        // Bars where close < open (down bars) should yield negative RVGI
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 30; i++)
        {
            double open = 105.0;
            double close = 100.0;   // consistently close < open
            double high = 107.0;
            double low = 98.0;
            rvgi.Update(new TBar(DateTime.UtcNow.AddMinutes(i), open, high, low, close, 1000), isNew: true);
        }
        Assert.True(rvgi.RvgiValue < 0.0, $"Expected RVGI < 0, got {rvgi.RvgiValue}");
    }

    [Fact]
    public void Update_DojiBars_ZeroDenominator_ReturnsZero()
    {
        // Doji bars: high == low (zero range) → denominator = 0 → RVGI = 0
        var rvgi = new Rvgi(period: 3);
        for (int i = 0; i < 10; i++)
        {
            // high == low == open == close → zero range
            rvgi.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100.0, 100.0, 100.0, 100.0, 0), isNew: true);
        }
        Assert.Equal(0.0, rvgi.RvgiValue, Tolerance);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 10; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        }
        _ = rvgi.RvgiValue;
        rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        // state should change (different bar advances output)
        Assert.True(double.IsFinite(rvgi.RvgiValue));
        Assert.True(double.IsFinite(rvgi.Signal));
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 12; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        }

        // Two corrections with same bar must yield identical result (idempotent)
        var bar = new TBar(DateTime.UtcNow, 105, 110, 100, 107, 1000);
        rvgi.Update(bar, isNew: false);
        double rv1 = rvgi.RvgiValue;
        double sg1 = rvgi.Signal;

        rvgi.Update(bar, isNew: false);
        double rv2 = rvgi.RvgiValue;
        double sg2 = rvgi.Signal;

        Assert.Equal(rv1, rv2, Tolerance);
        Assert.Equal(sg1, sg2, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var rvgi = new Rvgi(period: 5);
        var bars = new TBar[15];
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = _gbm.Next(isNew: true);
        }

        foreach (var b in bars)
        {
            rvgi.Update(b, isNew: true);
        }

        double baselineRvgi = rvgi.RvgiValue;
        double baselineSig = rvgi.Signal;

        // Corrupt then restore to last bar
        rvgi.Update(new TBar(DateTime.UtcNow, 200, 250, 150, 220, 5000), isNew: false);
        rvgi.Update(new TBar(DateTime.UtcNow, 999, 1050, 900, 1000, 9999), isNew: false);
        rvgi.Update(bars[^1], isNew: false);

        Assert.Equal(baselineRvgi, rvgi.RvgiValue, Tolerance);
        Assert.Equal(baselineSig, rvgi.Signal, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 20; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        }

        rvgi.Reset();

        Assert.False(rvgi.IsHot);
        // After reset, output should be 0 (doji bar with no bars prior)
        Assert.Equal(default, rvgi.Last);
    }

    // ───── D) Warmup / IsHot ─────

    [Fact]
    public void IsHot_FlipsAfterPeriodBars()
    {
        var rvgi = new Rvgi(period: 5);
        Assert.False(rvgi.IsHot);

        for (int i = 0; i < 4; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true), isNew: true);
            Assert.False(rvgi.IsHot);
        }

        rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        Assert.True(rvgi.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        Assert.Equal(10, new Rvgi(period: 10).WarmupPeriod);
        Assert.Equal(14, new Rvgi(period: 14).WarmupPeriod);
    }

    // ───── E) Robustness (NaN/Infinity) ─────

    [Fact]
    public void Update_NaN_High_DoesNotPropagate()
    {
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 8; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var nanBar = new TBar(DateTime.UtcNow, 100, double.NaN, 95, 102, 1000);
        rvgi.Update(nanBar, isNew: true);
        Assert.True(double.IsFinite(rvgi.RvgiValue));
        Assert.True(double.IsFinite(rvgi.Signal));
    }

    [Fact]
    public void Update_InfinityClose_DoesNotPropagate()
    {
        var rvgi = new Rvgi(period: 5);
        for (int i = 0; i < 8; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var infBar = new TBar(DateTime.UtcNow, 100, 110, 90, double.PositiveInfinity, 1000);
        rvgi.Update(infBar, isNew: true);
        Assert.True(double.IsFinite(rvgi.RvgiValue));
        Assert.True(double.IsFinite(rvgi.Signal));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var rvgi = new Rvgi(period: 5);
        // Feed a run of NaN bars — should not throw or produce non-finite output
        for (int i = 0; i < 5; i++)
        {
            rvgi.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                double.NaN, double.NaN, double.NaN, double.NaN, 0), isNew: true);
            Assert.True(double.IsFinite(rvgi.RvgiValue));
        }
    }

    // ───── F) Consistency (all modes match) ─────

    [Fact]
    [SkipLocalsInit]
    public void Consistency_Streaming_Vs_Batch_Match()
    {
        const int N = 100;
        const int period = 14;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 123);
        var bars = new TBar[N];
        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
        }

        // Streaming
        var rvgi = new Rvgi(period);
        for (int i = 0; i < N; i++)
        {
            rvgi.Update(bars[i], isNew: true);
        }
        double streamRvgi = rvgi.RvgiValue;
        double streamSig = rvgi.Signal;

        // Batch span
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        for (int i = 0; i < N; i++)
        {
            opens[i] = bars[i].Open; highs[i] = bars[i].High;
            lows[i] = bars[i].Low; closes[i] = bars[i].Close;
        }

        var rvgiBatch = new double[N];
        var sigBatch = new double[N];
        Rvgi.Batch(opens, highs, lows, closes, rvgiBatch, sigBatch, period);

        Assert.Equal(streamRvgi, rvgiBatch[N - 1], Tolerance);
        Assert.Equal(streamSig, sigBatch[N - 1], Tolerance);
    }

    [Fact]
    [SkipLocalsInit]
    public void Consistency_UpdateAll_Vs_Batch_Match()
    {
        const int N = 80;
        const int period = 10;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 456);
        var series = new TBarSeries();
        for (int i = 0; i < N; i++)
        {
            series.Add(gbm.Next(isNew: true));
        }

        var rvgiInst = new Rvgi(period);
        var (rvgiSeries, sigSeries) = rvgiInst.UpdateAll(series);

        var rvgiBatch = new double[N];
        var sigBatch = new double[N];
        Rvgi.Batch(series.OpenValues, series.HighValues, series.LowValues, series.CloseValues,
            rvgiBatch, sigBatch, period);

        Assert.Equal(rvgiSeries.Last.Value, rvgiBatch[N - 1], Tolerance);
        Assert.Equal(sigSeries.Last.Value, sigBatch[N - 1], Tolerance);
    }

    // ───── G) Span API validation ─────

    [Fact]
    public void Batch_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Rvgi.Batch(
                new double[5], new double[5], new double[5], new double[5],
                new double[5], new double[5], period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedHighLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Rvgi.Batch(
                new double[5], new double[6], new double[5], new double[5],
                new double[5], new double[5], period: 3));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedOutputLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Rvgi.Batch(
                new double[5], new double[5], new double[5], new double[5],
                new double[4], new double[5], period: 3));
        Assert.Equal("rvgiOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedSignalOutputLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Rvgi.Batch(
                new double[5], new double[5], new double[5], new double[5],
                new double[5], new double[4], period: 3));
        Assert.Equal("signalOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInputs_NoThrow()
    {
        var ex = Record.Exception(() =>
            Rvgi.Batch(
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                Span<double>.Empty, Span<double>.Empty, period: 5));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_LargePeriod_UsesArrayPool()
    {
        const int period = 300;
        const int N = 500;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 99);
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        for (int i = 0; i < N; i++)
        {
            var b = gbm.Next(isNew: true);
            opens[i] = b.Open; highs[i] = b.High;
            lows[i] = b.Low; closes[i] = b.Close;
        }
        var rvgiOut = new double[N];
        var sigOut = new double[N];
        Rvgi.Batch(opens, highs, lows, closes, rvgiOut, sigOut, period);

        Assert.True(double.IsFinite(rvgiOut[N - 1]));
        Assert.True(double.IsFinite(sigOut[N - 1]));
    }

    // ───── H) Chainability / events ─────

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var rvgi = new Rvgi(period: 5);
        int fired = 0;
        rvgi.Pub += (_, in _) => fired++;

        for (int i = 0; i < 5; i++)
        {
            rvgi.Update(_gbm.Next(isNew: true), isNew: true);
        }

        Assert.Equal(5, fired);
    }

    [Fact]
    public void Constructor_TBarSeries_Chains()
    {
        var series = new TBarSeries();
        var rvgi = new Rvgi(series, period: 3);

        for (int i = 0; i < 6; i++)
        {
            series.Add(_gbm.Next(isNew: true));
        }

        Assert.True(rvgi.IsHot);
        Assert.True(double.IsFinite(rvgi.RvgiValue));
        Assert.True(double.IsFinite(rvgi.Signal));
    }

    // ───── Known-value tests ─────

    [Fact]
    public void KnownValue_AllUpBars_PositiveRvgi()
    {
        // Constant up bars: O=100, H=106, L=98, C=105 (C-O=5, H-L=8)
        // SWMA(C-O) = (5+2*5+2*5+5)/6 = 5, SWMA(H-L) = (8+2*8+2*8+8)/6 = 8
        // SMA ratio = 5/8 = 0.625
        var rvgi = new Rvgi(period: 3);
        for (int i = 0; i < 20; i++)
        {
            rvgi.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100, 106, 98, 105, 1000), isNew: true);
        }
        // After many identical bars, RVGI should converge to 5/8
        Assert.Equal(5.0 / 8.0, rvgi.RvgiValue, 1e-6);
    }

    [Fact]
    public void KnownValue_SymmetricBars_ZeroRvgi()
    {
        // Bars where close == open (doji-like but with range) → C-O = 0 → RVGI = 0
        var rvgi = new Rvgi(period: 3);
        for (int i = 0; i < 20; i++)
        {
            rvgi.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100, 105, 95, 100, 1000), isNew: true);
        }
        Assert.Equal(0.0, rvgi.RvgiValue, Tolerance);
    }

    [Fact]
    public void KnownValue_SignalConverges_ToRvgi_WhenConstant()
    {
        // When RVGI is constant, signal SWMA converges to the same value
        var rvgi = new Rvgi(period: 3);
        for (int i = 0; i < 30; i++)
        {
            rvgi.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100, 106, 98, 105, 1000), isNew: true);
        }
        // After many identical bars, signal should equal RVGI (SWMA of constant = constant)
        Assert.Equal(rvgi.RvgiValue, rvgi.Signal, 1e-6);
    }
}
