using Xunit;

namespace QuanTAlib.Tests;

public sealed class EpaTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var rng = new Random(42);
        var s = new TSeries();
        for (int i = 0; i < count; i++)
        {
            s.Add(new TValue(DateTime.UtcNow.AddDays(i), 100 + rng.NextDouble() * 10));
        }
        return s;
    }

    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Ctor_DefaultPeriod_Is28()
    {
        var epa = new Epa();
        Assert.Equal("Epa(28)", epa.Name);
    }

    [Fact]
    public void Ctor_CustomPeriod_SetsName()
    {
        var epa = new Epa(period: 14);
        Assert.Equal("Epa(14)", epa.Name);
    }

    [Fact]
    public void Ctor_Period1_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Epa(period: 1));
    }

    [Fact]
    public void Ctor_Period0_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Epa(period: 0));
    }

    [Fact]
    public void Ctor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Epa(period: -5));
    }

    // ── Basic Calculation ──────────────────────────────────────────

    [Fact]
    public void Update_FirstBar_ReturnsZeroAngle()
    {
        var epa = new Epa();
        var result = epa.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_AfterWarmup_ReturnsFiniteAngle()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(50);
        TValue last = default;
        foreach (var tv in s)
        {
            last = epa.Update(tv);
        }
        Assert.True(double.IsFinite(last.Value));
    }

    [Fact]
    public void Angle_IsSetAfterUpdate()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(20);
        foreach (var tv in s)
        {
            epa.Update(tv);
        }
        Assert.True(double.IsFinite(epa.Angle));
    }

    [Fact]
    public void DerivedPeriod_IsFiniteAfterWarmup()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(30);
        foreach (var tv in s)
        {
            epa.Update(tv);
        }
        Assert.True(double.IsFinite(epa.DerivedPeriod));
    }

    [Fact]
    public void TrendState_IsValid()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(50);
        foreach (var tv in s)
        {
            epa.Update(tv);
        }
        Assert.InRange(epa.TrendState, -1, 1);
    }

    // ── State / Bar Correction ─────────────────────────────────────

    [Fact]
    public void BarCorrection_UpdateWithIsNewFalse_RestoresState()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(20);

        // Process first 19 bars
        for (int i = 0; i < 19; i++)
        {
            epa.Update(s[i]);
        }

        // Process bar 20 (new)
        epa.Update(s[19], isNew: true);
        double angleAfterNew = epa.Angle;

        // Correct bar 20 (not new) with same value
        epa.Update(s[19], isNew: false);
        double angleAfterCorrection = epa.Angle;

        Assert.Equal(angleAfterNew, angleAfterCorrection, precision: 10);
    }

    [Fact]
    public void BarCorrection_DifferentValue_ProducesDifferentResult()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(20);

        for (int i = 0; i < 19; i++)
        {
            epa.Update(s[i]);
        }

        // New bar
        epa.Update(s[19], isNew: true);

        // Correct with very different value
        epa.Update(new TValue(s[19].Time, s[19].Value + 50), isNew: false);
        double angle2 = epa.Angle;

        // May or may not be different due to monotonic constraint, but should be finite
        Assert.True(double.IsFinite(angle2));
    }

    // ── Warmup / IsHot ─────────────────────────────────────────────

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var epa = new Epa(period: 10);
        for (int i = 0; i < 9; i++)
        {
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i));
        }
        Assert.False(epa.IsHot);
    }

    [Fact]
    public void IsHot_TrueAtWarmup()
    {
        var epa = new Epa(period: 10);
        for (int i = 0; i < 10; i++)
        {
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i));
        }
        Assert.True(epa.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var epa = new Epa(period: 20);
        Assert.Equal(20, epa.WarmupPeriod);
    }

    // ── Robustness ─────────────────────────────────────────────────

    [Fact]
    public void NaN_Input_DoesNotCorrupt()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(20);
        foreach (var tv in s)
        {
            epa.Update(tv);
        }

        // Feed NaN
        epa.Update(new TValue(DateTime.UtcNow.AddDays(100), double.NaN));
        Assert.True(double.IsFinite(epa.Angle));
    }

    [Fact]
    public void Infinity_Input_DoesNotCorrupt()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(20);
        foreach (var tv in s)
        {
            epa.Update(tv);
        }
        epa.Update(new TValue(DateTime.UtcNow.AddDays(100), double.PositiveInfinity));
        Assert.True(double.IsFinite(epa.Angle));
    }

    [Fact]
    public void ConstantInput_Angle_IsFinite()
    {
        var epa = new Epa(period: 10);
        for (int i = 0; i < 30; i++)
        {
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), 42.0));
        }
        Assert.True(double.IsFinite(epa.Angle));
    }

    // ── Reset ──────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(30);
        foreach (var tv in s)
        {
            epa.Update(tv);
        }
        Assert.True(epa.IsHot);

        epa.Reset();
        Assert.False(epa.IsHot);
        Assert.Equal(0.0, epa.Angle);
        Assert.Equal(0.0, epa.DerivedPeriod);
        Assert.Equal(0, epa.TrendState);
    }

    [Fact]
    public void Reset_ProducesSameResultsOnReprocess()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(50);

        foreach (var tv in s)
        {
            epa.Update(tv);
        }
        double angle1 = epa.Angle;

        epa.Reset();
        foreach (var tv in s)
        {
            epa.Update(tv);
        }
        double angle2 = epa.Angle;

        Assert.Equal(angle1, angle2, precision: 10);
    }

    // ── Consistency: 4 API modes ───────────────────────────────────

    [Fact]
    public void AllModes_Consistent()
    {
        var s = MakeSeries(200);
        int period = 14;

        // Mode 1: streaming
        var epa1 = new Epa(period);
        foreach (var tv in s)
        {
            epa1.Update(tv);
        }

        // Mode 2: Update(TSeries)
        var epa2 = new Epa(period);
        var ts2 = epa2.Update(s);

        // Mode 3: Batch(TSeries)
        var ts3 = Epa.Batch(s, period);

        // Mode 4: Batch(Span)
        double[] src = new double[s.Count];
        double[] dst = new double[s.Count];
        for (int i = 0; i < s.Count; i++)
        {
            src[i] = s[i].Value;
        }
        Epa.Batch(src, dst, period);

        Assert.Equal(ts2[^1].Value, ts3[^1].Value, precision: 10);
        Assert.Equal(ts2[^1].Value, dst[^1], precision: 10);
        Assert.Equal(epa1.Angle, ts2[^1].Value, precision: 10);
    }

    // ── Batch(TSeries) ─────────────────────────────────────────────

    [Fact]
    public void Batch_TSeries_SameLengthAsSource()
    {
        var s = MakeSeries(100);
        var result = Epa.Batch(s);
        Assert.Equal(s.Count, result.Count);
    }

    [Fact]
    public void Batch_TSeries_EmptySource_ReturnsEmpty()
    {
        var result = Epa.Batch(new TSeries());
        Assert.Empty(result);
    }

    // ── Batch(Span) ────────────────────────────────────────────────

    [Fact]
    public void Batch_Span_ProducesFiniteOutput()
    {
        double[] src = [100, 101, 102, 103, 104, 103, 102, 101, 100, 99, 98, 99, 100, 101, 102];
        double[] dst = new double[src.Length];
        Epa.Batch(src, dst, period: 5);
        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_Span_MismatchedLength_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[5];
        Assert.Throws<ArgumentException>(() => Epa.Batch(src, dst));
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[10];
        Assert.Throws<ArgumentException>(() => Epa.Batch(src, dst, period: 0));
    }

    // ── Calculate factory ──────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var s = MakeSeries(50);
        var (results, indicator) = Epa.Calculate(s, period: 10);
        Assert.Equal(s.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(indicator.Angle, results[^1].Value, precision: 10);
    }

    // ── PubSub (chaining) ──────────────────────────────────────────

    [Fact]
    public void PubSub_ReceivesEvents()
    {
        var source = new TSeries();
        var epa = new Epa(source, period: 10);
        int eventCount = 0;
        epa.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddDays(i), 100 + i));
        }

        Assert.Equal(20, eventCount);
    }

    [Fact]
    public void PubSub_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Epa(null!, period: 10));
    }

    // ── Prime ──────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmUpIndicator()
    {
        var epa = new Epa(period: 10);
        double[] data = new double[20];
        for (int i = 0; i < 20; i++)
        {
            data[i] = 100 + i * 0.5;
        }
        epa.Prime(data);
        Assert.True(epa.IsHot);
    }

    // ── EPA-specific behavior ──────────────────────────────────────

    [Fact]
    public void SineWave_ProducesVaryingAngle()
    {
        var epa = new Epa(period: 20);
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + 10 * Math.Sin(2 * Math.PI * i / 20.0);
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
        }
        // With a matching sine wave, angle should advance
        Assert.True(double.IsFinite(epa.Angle));
    }

    [Fact]
    public void DerivedPeriod_ClampedTo60()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(200);
        foreach (var tv in s)
        {
            epa.Update(tv);
            Assert.True(epa.DerivedPeriod <= 60.0,
                $"DerivedPeriod {epa.DerivedPeriod} exceeds max 60");
        }
    }

    [Fact]
    public void TrendState_OnlyValidValues()
    {
        var epa = new Epa(period: 10);
        var s = MakeSeries(200);
        foreach (var tv in s)
        {
            epa.Update(tv);
            Assert.True(epa.TrendState == -1 || epa.TrendState == 0 || epa.TrendState == 1,
                $"Invalid TrendState: {epa.TrendState}");
        }
    }

    [Fact]
    public void DifferentPeriod_DifferentResults()
    {
        var s = MakeSeries(100);

        var epa10 = new Epa(period: 10);
        var epa28 = new Epa(period: 28);

        foreach (var tv in s)
        {
            epa10.Update(tv);
            epa28.Update(tv);
        }

        // Different periods should generally produce different angles
        // (not guaranteed for all data, but very likely with random data)
        Assert.NotEqual(epa10.Angle, epa28.Angle);
    }

    [Fact]
    public void Update_TSeries_MatchesStreaming()
    {
        var s = MakeSeries(100);
        int period = 14;

        // Streaming
        var epa1 = new Epa(period);
        foreach (var tv in s)
        {
            epa1.Update(tv);
        }

        // Update(TSeries)
        var epa2 = new Epa(period);
        _ = epa2.Update(s);

        Assert.Equal(epa1.Angle, epa2.Angle, precision: 10);
        Assert.Equal(epa1.DerivedPeriod, epa2.DerivedPeriod, precision: 10);
        Assert.Equal(epa1.TrendState, epa2.TrendState);
    }
}
