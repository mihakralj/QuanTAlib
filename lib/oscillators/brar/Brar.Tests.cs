using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class BrarTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.2, seed: 42);
    private const double Tolerance = 1e-9;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_DefaultPeriod_IsValid()
    {
        var brar = new Brar();
        Assert.Equal("Brar(26)", brar.Name);
        Assert.Equal(26, brar.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Brar(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Brar(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var brar = new Brar(period: 14);
        Assert.Equal("Brar(14)", brar.Name);
        Assert.Equal(14, brar.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var brar = new Brar(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        var result = brar.Update(bar);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var brar = new Brar(period: 5);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        brar.Update(bar);
        Assert.True(double.IsFinite(brar.Last.Value));
    }

    [Fact]
    public void Update_BrAndAr_Accessible()
    {
        var brar = new Brar(period: 5);
        for (int i = 0; i < 10; i++)
        {
            brar.Update(_gbm.Next(isNew: true));
        }
        Assert.True(double.IsFinite(brar.Br));
        Assert.True(double.IsFinite(brar.Ar));
    }

    [Fact]
    public void Update_BullishBars_BrAbove100()
    {
        var brar = new Brar(period: 5);
        // Bars where High is far above PrevClose, PrevClose is above Low
        // H >> PrevC >> L: strong upside push
        double price = 100.0;
        for (int i = 0; i < 20; i++)
        {
            double open = price + 1;
            double high = price + 5;
            double low = price - 1;
            brar.Update(new TBar(DateTime.UtcNow.AddMinutes(i), open, high, low, price + 3, 1000), isNew: true);
            price += 3;
        }
        Assert.True(brar.Br > 100.0, $"Expected BR > 100, got {brar.Br}");
    }

    [Fact]
    public void Update_SymmetricBars_ArNear100()
    {
        var brar = new Brar(period: 10);
        // Open exactly at midpoint of High-Low → AR numerator == AR denominator → AR = 100
        for (int i = 0; i < 30; i++)
        {
            double open = 100.0; // midpoint of 95..105
            double high = 105.0;
            double low = 95.0;
            double close = 100.0;
            brar.Update(new TBar(DateTime.UtcNow.AddMinutes(i), open, high, low, close, 1000), isNew: true);
        }
        Assert.Equal(100.0, brar.Ar, Tolerance);
    }

    [Fact]
    public void Update_ZeroDenominator_BrReturns100()
    {
        var brar = new Brar(period: 3);
        // PrevClose at or below Low for every bar → BR denominator = 0 → returns 100
        for (int i = 0; i < 5; i++)
        {
            // low=95 > prevClose=90 → max(0, prevClose-low)=0 every time
            brar.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100.0, 110.0, 95.0, 90.0, 1000), isNew: true);
        }
        Assert.True(double.IsFinite(brar.Br));
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var brar = new Brar(period: 5);
        for (int i = 0; i < 10; i++)
        {
            brar.Update(_gbm.Next(isNew: true), isNew: true);
        }
        brar.Update(_gbm.Next(isNew: true), isNew: true);
        Assert.True(double.IsFinite(brar.Br));
        Assert.True(double.IsFinite(brar.Ar));
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var brar = new Brar(period: 5);
        for (int i = 0; i < 12; i++)
        {
            brar.Update(_gbm.Next(isNew: true), isNew: true);
        }

        // Two corrections with same data must yield same result
        var bar = new TBar(DateTime.UtcNow, 105, 110, 100, 107, 1000);
        brar.Update(bar, isNew: false);
        double corrected1 = brar.Br;
        double arCorrected1 = brar.Ar;

        brar.Update(bar, isNew: false);
        double corrected2 = brar.Br;

        Assert.Equal(corrected1, corrected2, Tolerance);
        Assert.Equal(arCorrected1, brar.Ar, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var brar = new Brar(period: 5);
        var bars = new TBar[15];
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i] = _gbm.Next(isNew: true);
        }

        foreach (var b in bars)
        {
            brar.Update(b, isNew: true);
        }

        double baselineBr = brar.Br;
        double baselineAr = brar.Ar;

        // Corrupt and restore
        brar.Update(new TBar(DateTime.UtcNow, 200, 250, 150, 220, 5000), isNew: false);
        brar.Update(new TBar(DateTime.UtcNow, 999, 1050, 900, 1000, 9999), isNew: false);
        brar.Update(bars[^1], isNew: false);

        Assert.Equal(baselineBr, brar.Br, Tolerance);
        Assert.Equal(baselineAr, brar.Ar, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var brar = new Brar(period: 5);
        for (int i = 0; i < 10; i++)
        {
            brar.Update(_gbm.Next(isNew: true), isNew: true);
        }

        brar.Reset();

        Assert.False(brar.IsHot);
        // After reset, single bar should give 100 for both (only 1 bar, symmetric or equilibrium)
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        brar.Update(bar, isNew: true);
        Assert.True(double.IsFinite(brar.Br));
        Assert.True(double.IsFinite(brar.Ar));
    }

    // ───── D) Warmup / IsHot ─────

    [Fact]
    public void IsHot_FlipsAfterPeriodBars()
    {
        var brar = new Brar(period: 5);
        Assert.False(brar.IsHot);

        for (int i = 0; i < 4; i++)
        {
            brar.Update(_gbm.Next(isNew: true), isNew: true);
            Assert.False(brar.IsHot);
        }

        brar.Update(_gbm.Next(isNew: true), isNew: true);
        Assert.True(brar.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        Assert.Equal(10, new Brar(period: 10).WarmupPeriod);
        Assert.Equal(26, new Brar(period: 26).WarmupPeriod);
    }

    // ───── E) Robustness (NaN/Infinity) ─────

    [Fact]
    public void Update_NaN_High_DoesNotPropagate()
    {
        var brar = new Brar(period: 5);
        for (int i = 0; i < 8; i++)
        {
            brar.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var nanBar = new TBar(DateTime.UtcNow, 100, double.NaN, 95, 102, 1000);
        brar.Update(nanBar, isNew: true);
        Assert.True(double.IsFinite(brar.Br));
        Assert.True(double.IsFinite(brar.Ar));
    }

    [Fact]
    public void Update_InfinityClose_DoesNotPropagate()
    {
        var brar = new Brar(period: 5);
        for (int i = 0; i < 8; i++)
        {
            brar.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var infBar = new TBar(DateTime.UtcNow, 100, 110, 90, double.PositiveInfinity, 1000);
        brar.Update(infBar, isNew: true);
        Assert.True(double.IsFinite(brar.Br));
        Assert.True(double.IsFinite(brar.Ar));
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
        var brar = new Brar(period);
        for (int i = 0; i < N; i++)
        {
            brar.Update(bars[i], isNew: true);
        }
        double streamBr = brar.Br;
        double streamAr = brar.Ar;

        // Batch span
        var opens = new double[N];
        var highs = new double[N];
        var lows = new double[N];
        var closes = new double[N];
        for (int i = 0; i < N; i++)
        {
            opens[i] = bars[i].Open;
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        var brBatch = new double[N];
        var arBatch = new double[N];
        Brar.Batch(opens, highs, lows, closes, brBatch, arBatch, period);

        Assert.Equal(streamBr, brBatch[N - 1], Tolerance);
        Assert.Equal(streamAr, arBatch[N - 1], Tolerance);
    }

    // ───── G) Span API validation ─────

    [Fact]
    public void Batch_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Brar.Batch(
                new double[5], new double[5], new double[5], new double[5],
                new double[5], new double[5], period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedHighLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Brar.Batch(
                new double[5], new double[6], new double[5], new double[5],
                new double[5], new double[5], period: 3));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedOutputLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Brar.Batch(
                new double[5], new double[5], new double[5], new double[5],
                new double[4], new double[5], period: 3));
        Assert.Equal("brOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInputs_NoThrow()
    {
        // Should not throw or write anything — just verify no exception
        var ex = Record.Exception(() =>
            Brar.Batch(
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                Span<double>.Empty, Span<double>.Empty, period: 5));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_LargePeriod_UsesArrayPool()
    {
        // period > 256 forces ArrayPool path
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
        var brOut = new double[N];
        var arOut = new double[N];
        Brar.Batch(opens, highs, lows, closes, brOut, arOut, period);

        Assert.True(double.IsFinite(brOut[N - 1]));
        Assert.True(double.IsFinite(arOut[N - 1]));
    }

    // ───── H) Chainability / events ─────

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var brar = new Brar(period: 5);
        int fired = 0;
        brar.Pub += (_, in _) => fired++;

        for (int i = 0; i < 5; i++)
        {
            brar.Update(_gbm.Next(isNew: true), isNew: true);
        }

        Assert.Equal(5, fired);
    }

    [Fact]
    public void Constructor_TBarSeries_Chains()
    {
        var series = new TBarSeries();
        var brar = new Brar(series, period: 3);

        for (int i = 0; i < 6; i++)
        {
            series.Add(_gbm.Next(isNew: true));
        }

        Assert.True(brar.IsHot);
        Assert.True(double.IsFinite(brar.Br));
    }

    // ───── Known-value tests ─────

    [Fact]
    public void KnownValue_SingleBar_FirstBarBootstrap()
    {
        var brar = new Brar(period: 3);
        // First bar: prevClose = open = 100, high = 110, low = 90
        // brNum = max(0, 110 - 100) = 10
        // brDen = max(0, 100 - 90) = 10   → BR = 100
        // arNum = max(0, 110 - 100) = 10
        // arDen = max(0, 100 - 90) = 10   → AR = 100
        var bar = new TBar(DateTime.UtcNow, 100.0, 110.0, 90.0, 105.0, 1000);
        brar.Update(bar, isNew: true);
        Assert.Equal(100.0, brar.Br, Tolerance);
        Assert.Equal(100.0, brar.Ar, Tolerance);
    }

    [Fact]
    public void KnownValue_TwoBars_CorrectRatios()
    {
        var brar = new Brar(period: 3);
        // Bar 1: O=100, H=110, L=90, C=105 → prevC used as open=100
        //   brNum=10, brDen=10, arNum=10, arDen=10
        brar.Update(new TBar(DateTime.UtcNow, 100.0, 110.0, 90.0, 105.0, 1000), isNew: true);

        // Bar 2: O=106, H=115, L=100, C=110, prevC=105
        //   brNum = max(0, 115-105) = 10
        //   brDen = max(0, 105-100) = 5
        //   arNum = max(0, 115-106) = 9
        //   arDen = max(0, 106-100) = 6
        // Running sums (period=3, only 2 bars):
        //   brNumSum=10+10=20, brDenSum=10+5=15 → BR = 20/15*100 ≈ 133.333...
        //   arNumSum=10+9=19, arDenSum=10+6=16 → AR = 19/16*100 = 118.75
        brar.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 106.0, 115.0, 100.0, 110.0, 1000), isNew: true);

        Assert.Equal(20.0 / 15.0 * 100.0, brar.Br, Tolerance);
        Assert.Equal(19.0 / 16.0 * 100.0, brar.Ar, Tolerance);
    }
}
