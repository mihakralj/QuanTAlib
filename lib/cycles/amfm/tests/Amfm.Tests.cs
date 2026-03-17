using Xunit;

namespace QuanTAlib.Tests;

public sealed class AmfmTests
{
    private const double Tolerance = 1e-9;

    private static TBarSeries GenerateBars(int count, int seed = 42)
    {
        var gbm = new GBM(100.0, 0.05, 0.2, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromHours(1));
    }

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_DefaultPeriod_IsValid()
    {
        var amfm = new Amfm();
        Assert.Equal("Amfm(30)", amfm.Name);
        Assert.Equal(30, amfm.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Amfm(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Amfm(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var amfm = new Amfm(period: 10);
        Assert.Equal("Amfm(10)", amfm.Name);
        Assert.Equal(12, amfm.WarmupPeriod); // max(12, 10) = 12
    }

    [Fact]
    public void Constructor_LargePeriod_WarmupEqualsPeriod()
    {
        var amfm = new Amfm(period: 50);
        Assert.Equal(50, amfm.WarmupPeriod); // max(12, 50) = 50
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var amfm = new Amfm(period: 10);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        var result = amfm.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Am_IsNonNegative()
    {
        var amfm = new Amfm(period: 10);
        var bars = GenerateBars(100);
        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
            Assert.True(amfm.Am >= 0.0, $"AM should be non-negative at bar {i}, got {amfm.Am}");
        }
    }

    [Fact]
    public void Fm_IsBounded()
    {
        var amfm = new Amfm(period: 30);
        var bars = GenerateBars(500);
        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
            if (i >= amfm.WarmupPeriod)
            {
                Assert.True(amfm.Fm >= -2.0 && amfm.Fm <= 2.0,
                    $"FM should be approximately bounded at bar {i}, got {amfm.Fm}");
            }
        }
    }

    [Fact]
    public void ConstantPrice_AmConvergesToZero()
    {
        var amfm = new Amfm(period: 10);
        for (int i = 0; i < 100; i++)
        {
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), 100, 100, 100, 100, 1000));
        }
        Assert.True(amfm.Am < 1e-10, $"AM should be ~0 for constant price, got {amfm.Am}");
    }

    [Fact]
    public void ConstantPrice_FmConvergesToZero()
    {
        var amfm = new Amfm(period: 10);
        for (int i = 0; i < 100; i++)
        {
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), 100, 100, 100, 100, 1000));
        }
        Assert.True(Math.Abs(amfm.Fm) < 1e-10, $"FM should be ~0 for constant price, got {amfm.Fm}");
    }

    // ───── C) Behavioral tests ─────

    [Fact]
    public void Uptrend_FmPositive()
    {
        var amfm = new Amfm(period: 10);
        // Strong uptrend: Close always > Open
        for (int i = 0; i < 50; i++)
        {
            double open = 100 + i;
            double close = open + 2;
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), open, close + 1, open - 0.5, close, 1000));
        }
        Assert.True(amfm.Fm > 0, $"FM should be positive in uptrend, got {amfm.Fm}");
        Assert.True(amfm.Am > 0, $"AM should be positive in uptrend, got {amfm.Am}");
    }

    [Fact]
    public void Downtrend_FmNegative()
    {
        var amfm = new Amfm(period: 10);
        // Strong downtrend: Close always < Open
        for (int i = 0; i < 50; i++)
        {
            double open = 200 - i;
            double close = open - 2;
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), open, open + 0.5, close - 1, close, 1000));
        }
        Assert.True(amfm.Fm < 0, $"FM should be negative in downtrend, got {amfm.Fm}");
        Assert.True(amfm.Am > 0, $"AM should be positive in downtrend, got {amfm.Am}");
    }

    [Fact]
    public void Ascending_Descending_OppositeFm()
    {
        var amfmUp = new Amfm(period: 10);
        var amfmDown = new Amfm(period: 10);

        for (int i = 0; i < 50; i++)
        {
            double baseUp = 100.0 + i;
            double baseDown = 200.0 - i;
            amfmUp.Update(new TBar(DateTime.UtcNow.AddHours(i), baseUp, baseUp + 3, baseUp - 0.5, baseUp + 2, 1000));
            amfmDown.Update(new TBar(DateTime.UtcNow.AddHours(i), baseDown, baseDown + 0.5, baseDown - 3, baseDown - 2, 1000));
        }

        Assert.True(amfmUp.Fm > 0 && amfmDown.Fm < 0,
            $"Opposite trends should give opposite FM signs: up={amfmUp.Fm}, down={amfmDown.Fm}");
    }

    // ───── D) IsHot warmup ─────

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var amfm = new Amfm(period: 30);
        for (int i = 0; i < 29; i++)
        {
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), 100, 105, 95, 102, 1000));
            Assert.False(amfm.IsHot, $"Should not be hot at bar {i}");
        }
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        var amfm = new Amfm(period: 30);
        for (int i = 0; i < 31; i++)
        {
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), 100, 105, 95, 102, 1000));
        }
        Assert.True(amfm.IsHot);
    }

    // ───── E) Bar correction (isNew) ─────

    [Fact]
    public void BarCorrection_IsNew_False_RestoresState()
    {
        var amfm = new Amfm(period: 10);
        var bars = GenerateBars(30);

        // Process bars 0..28
        for (int i = 0; i < 29; i++)
        {
            amfm.Update(bars[i]);
        }

        // Process bar 29 as new
        amfm.Update(bars[29]);
        double am1 = amfm.Am;
        double fm1 = amfm.Fm;

        // Re-process bar 29 as correction (isNew=false) — same value
        amfm.Update(bars[29], isNew: false);
        double am2 = amfm.Am;
        double fm2 = amfm.Fm;

        Assert.Equal(am1, am2, Tolerance);
        Assert.Equal(fm1, fm2, Tolerance);
    }

    [Fact]
    public void BarCorrection_DifferentValue_Changes()
    {
        var amfm = new Amfm(period: 10);

        // Use deterministic bars where close != open (non-zero deriv)
        for (int i = 0; i < 29; i++)
        {
            double o = 100.0 + i;
            double c = o + 2.0; // positive deriv
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), o, c + 1, o - 1, c, 1000));
        }

        // Bar 29: positive deriv
        var bar29 = new TBar(DateTime.UtcNow.AddHours(29), 130, 135, 128, 133, 1000);
        amfm.Update(bar29);
        double fm1 = amfm.Fm;
        double am1 = amfm.Am;

        // Correct with zero-deriv bar (open == close) — opposite of original
        var corrected = new TBar(bar29.Time, 130, 135, 128, 130, 1000);
        amfm.Update(corrected, isNew: false);
        double fm2 = amfm.Fm;
        double am2 = amfm.Am;

        // At least one of AM or FM must differ
        Assert.True(fm1 != fm2 || am1 != am2,
            $"Bar correction should change output: FM {fm1} vs {fm2}, AM {am1} vs {am2}");
    }

    // ───── F) NaN/Inf handling ─────

    [Fact]
    public void NaN_Input_ProducesFiniteOutput()
    {
        var amfm = new Amfm(period: 10);

        // Warm up with valid data
        for (int i = 0; i < 15; i++)
        {
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), 100, 105, 95, 102, 1000));
        }

        // Feed NaN
        amfm.Update(new TBar(DateTime.UtcNow.AddHours(20), double.NaN, 105, 95, double.NaN, 1000));
        Assert.True(double.IsFinite(amfm.Am));
        Assert.True(double.IsFinite(amfm.Fm));
    }

    [Fact]
    public void Inf_Input_ProducesFiniteOutput()
    {
        var amfm = new Amfm(period: 10);

        for (int i = 0; i < 15; i++)
        {
            amfm.Update(new TBar(DateTime.UtcNow.AddHours(i), 100, 105, 95, 102, 1000));
        }

        amfm.Update(new TBar(DateTime.UtcNow.AddHours(20), double.PositiveInfinity, 105, 95, double.NegativeInfinity, 1000));
        Assert.True(double.IsFinite(amfm.Am));
        Assert.True(double.IsFinite(amfm.Fm));
    }

    // ───── G) Reset ─────

    [Fact]
    public void Reset_ClearsState()
    {
        var amfm = new Amfm(period: 10);
        var bars = GenerateBars(30);

        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
        }

        amfm.Reset();
        Assert.False(amfm.IsHot);
    }

    [Fact]
    public void Reset_SameResultsAfterReplay()
    {
        var amfm = new Amfm(period: 10);
        var bars = GenerateBars(30);

        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
        }
        double am1 = amfm.Am;
        double fm1 = amfm.Fm;

        amfm.Reset();
        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
        }
        double am2 = amfm.Am;
        double fm2 = amfm.Fm;

        Assert.Equal(am1, am2, Tolerance);
        Assert.Equal(fm1, fm2, Tolerance);
    }

    // ───── H) Streaming vs Batch ─────

    [Fact]
    public void StreamingMatchesBatch()
    {
        var bars = GenerateBars(200);
        int period = 20;

        // Streaming
        var amfm = new Amfm(period);
        double[] streamAm = new double[bars.Count];
        double[] streamFm = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
            streamAm[i] = amfm.Am;
            streamFm[i] = amfm.Fm;
        }

        // Batch
        var opens = new double[bars.Count];
        var closes = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            opens[i] = bars[i].Open;
            closes[i] = bars[i].Close;
        }
        var batchAm = new double[bars.Count];
        var batchFm = new double[bars.Count];
        Amfm.Batch(opens, closes, batchAm, batchFm, period);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamAm[i], batchAm[i], Tolerance);
            Assert.Equal(streamFm[i], batchFm[i], Tolerance);
        }
    }

    // ───── I) UpdateAll ─────

    [Fact]
    public void UpdateAll_ReturnsDualSeries()
    {
        var bars = GenerateBars(50);
        var amfm = new Amfm(period: 10);
        var (am, fm) = amfm.UpdateAll(bars);

        Assert.Equal(50, am.Count);
        Assert.Equal(50, fm.Count);
    }

    [Fact]
    public void UpdateAll_EmptySource_ReturnsEmpty()
    {
        var amfm = new Amfm(period: 10);
        var (am, fm) = amfm.UpdateAll(new TBarSeries());

        Assert.Empty(am);
        Assert.Empty(fm);
    }

    // ───── J) Calculate ─────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(50);
        var (results, indicator) = Amfm.Calculate(bars, 10);

        Assert.Equal(50, results.Am.Count);
        Assert.Equal(50, results.Fm.Count);
        Assert.True(indicator.IsHot);
    }

    // ───── K) Batch validation ─────

    [Fact]
    public void Batch_MismatchedLengths_Throws()
    {
        var open = new double[10];
        var close = new double[5];
        var am = new double[10];
        var fm = new double[10];
        Assert.Throws<ArgumentException>(() => Amfm.Batch(open, close, am, fm));
    }

    [Fact]
    public void Batch_ZeroPeriod_Throws()
    {
        var open = new double[10];
        var close = new double[10];
        var am = new double[10];
        var fm = new double[10];
        Assert.Throws<ArgumentException>(() => Amfm.Batch(open, close, am, fm, period: 0));
    }

    [Fact]
    public void Batch_EmptySpans_NoThrow()
    {
        var ex = Record.Exception(() =>
            Amfm.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                Span<double>.Empty, Span<double>.Empty));
        Assert.Null(ex);
    }

    // ───── L) Event subscription ─────

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var amfm = new Amfm(period: 10);
        int eventCount = 0;
        amfm.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        var bars = GenerateBars(20);
        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
        }
        Assert.Equal(20, eventCount);
    }

    [Fact]
    public void TBarSeries_Constructor_Primes()
    {
        var bars = GenerateBars(50);
        var amfm = new Amfm(bars, period: 10);
        Assert.True(amfm.IsHot);
    }

    // ───── M) Different periods ─────

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(100)]
    public void DifferentPeriods_AllFinite(int period)
    {
        var amfm = new Amfm(period);
        var bars = GenerateBars(200);
        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i]);
            Assert.True(double.IsFinite(amfm.Am), $"AM not finite at bar {i}");
            Assert.True(double.IsFinite(amfm.Fm), $"FM not finite at bar {i}");
        }
    }
}
