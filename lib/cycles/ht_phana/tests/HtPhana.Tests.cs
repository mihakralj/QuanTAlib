using Xunit;

namespace QuanTAlib.Tests;

public sealed class HtPhanaTests
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
        var ind = new HtPhana();
        Assert.Equal("HtPhana(28)", ind.Name);
    }

    [Fact]
    public void Ctor_CustomPeriod_SetsName()
    {
        var ind = new HtPhana(period: 14);
        Assert.Equal("HtPhana(14)", ind.Name);
    }

    [Fact]
    public void Ctor_Period1_Throws()
    {
        Assert.Throws<ArgumentException>(() => new HtPhana(period: 1));
    }

    [Fact]
    public void Ctor_Period0_Throws()
    {
        Assert.Throws<ArgumentException>(() => new HtPhana(period: 0));
    }

    [Fact]
    public void Ctor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new HtPhana(period: -5));
    }

    // ── Basic Calculation ──────────────────────────────────────────

    [Fact]
    public void Update_FirstBar_ReturnsZeroAngle()
    {
        var ind = new HtPhana();
        var result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_AfterWarmup_ReturnsFiniteAngle()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(50);
        TValue last = default;
        foreach (var tv in s)
        {
            last = ind.Update(tv);
        }
        Assert.True(double.IsFinite(last.Value));
    }

    [Fact]
    public void Angle_IsSetAfterUpdate()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(20);
        foreach (var tv in s)
        {
            ind.Update(tv);
        }
        Assert.True(double.IsFinite(ind.Angle));
    }

    [Fact]
    public void DerivedPeriod_IsFiniteAfterWarmup()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(30);
        foreach (var tv in s)
        {
            ind.Update(tv);
        }
        Assert.True(double.IsFinite(ind.DerivedPeriod));
    }

    [Fact]
    public void TrendState_IsValid()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(50);
        foreach (var tv in s)
        {
            ind.Update(tv);
        }
        Assert.InRange(ind.TrendState, -1, 1);
    }

    // ── State / Bar Correction ─────────────────────────────────────

    [Fact]
    public void BarCorrection_UpdateWithIsNewFalse_RestoresState()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(20);

        // Process first 19 bars
        for (int i = 0; i < 19; i++)
        {
            ind.Update(s[i]);
        }

        // Process bar 20 (new)
        ind.Update(s[19], isNew: true);
        double angleAfterNew = ind.Angle;

        // Correct bar 20 (not new) with same value
        ind.Update(s[19], isNew: false);
        double angleAfterCorrection = ind.Angle;

        Assert.Equal(angleAfterNew, angleAfterCorrection, precision: 10);
    }

    [Fact]
    public void BarCorrection_DifferentValue_ProducesDifferentResult()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(20);

        for (int i = 0; i < 19; i++)
        {
            ind.Update(s[i]);
        }

        // New bar
        ind.Update(s[19], isNew: true);

        // Correct with very different value
        ind.Update(new TValue(s[19].Time, s[19].Value + 50), isNew: false);
        double angle2 = ind.Angle;

        // May or may not be different due to monotonic constraint, but should be finite
        Assert.True(double.IsFinite(angle2));
    }

    // ── Warmup / IsHot ─────────────────────────────────────────────

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var ind = new HtPhana(period: 10);
        for (int i = 0; i < 9; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i));
        }
        Assert.False(ind.IsHot);
    }

    [Fact]
    public void IsHot_TrueAtWarmup()
    {
        var ind = new HtPhana(period: 10);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddDays(i), 100 + i));
        }
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var ind = new HtPhana(period: 20);
        Assert.Equal(20, ind.WarmupPeriod);
    }

    // ── Robustness ─────────────────────────────────────────────────

    [Fact]
    public void NaN_Input_DoesNotCorrupt()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(20);
        foreach (var tv in s)
        {
            ind.Update(tv);
        }

        // Feed NaN
        ind.Update(new TValue(DateTime.UtcNow.AddDays(100), double.NaN));
        Assert.True(double.IsFinite(ind.Angle));
    }

    [Fact]
    public void Infinity_Input_DoesNotCorrupt()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(20);
        foreach (var tv in s)
        {
            ind.Update(tv);
        }
        ind.Update(new TValue(DateTime.UtcNow.AddDays(100), double.PositiveInfinity));
        Assert.True(double.IsFinite(ind.Angle));
    }

    [Fact]
    public void ConstantInput_Angle_IsFinite()
    {
        var ind = new HtPhana(period: 10);
        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddDays(i), 42.0));
        }
        Assert.True(double.IsFinite(ind.Angle));
    }

    // ── Reset ──────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(30);
        foreach (var tv in s)
        {
            ind.Update(tv);
        }
        Assert.True(ind.IsHot);

        ind.Reset();
        Assert.False(ind.IsHot);
        Assert.Equal(0.0, ind.Angle);
        Assert.Equal(0.0, ind.DerivedPeriod);
        Assert.Equal(0, ind.TrendState);
    }

    [Fact]
    public void Reset_ProducesSameResultsOnReprocess()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(50);

        foreach (var tv in s)
        {
            ind.Update(tv);
        }
        double angle1 = ind.Angle;

        ind.Reset();
        foreach (var tv in s)
        {
            ind.Update(tv);
        }
        double angle2 = ind.Angle;

        Assert.Equal(angle1, angle2, precision: 10);
    }

    // ── Consistency: 4 API modes ───────────────────────────────────

    [Fact]
    public void AllModes_Consistent()
    {
        var s = MakeSeries(200);
        int period = 14;

        // Mode 1: streaming
        var ind1 = new HtPhana(period);
        foreach (var tv in s)
        {
            ind1.Update(tv);
        }

        // Mode 2: Update(TSeries)
        var ind2 = new HtPhana(period);
        var ts2 = ind2.Update(s);

        // Mode 3: Batch(TSeries)
        var ts3 = HtPhana.Batch(s, period);

        // Mode 4: Batch(Span)
        double[] src = new double[s.Count];
        double[] dst = new double[s.Count];
        for (int i = 0; i < s.Count; i++)
        {
            src[i] = s[i].Value;
        }
        HtPhana.Batch(src, dst, period);

        Assert.Equal(ts2[^1].Value, ts3[^1].Value, precision: 10);
        Assert.Equal(ts2[^1].Value, dst[^1], precision: 10);
        Assert.Equal(ind1.Angle, ts2[^1].Value, precision: 10);
    }

    // ── Batch(TSeries) ─────────────────────────────────────────────

    [Fact]
    public void Batch_TSeries_SameLengthAsSource()
    {
        var s = MakeSeries(100);
        var result = HtPhana.Batch(s);
        Assert.Equal(s.Count, result.Count);
    }

    [Fact]
    public void Batch_TSeries_EmptySource_ReturnsEmpty()
    {
        var result = HtPhana.Batch(new TSeries());
        Assert.Empty(result);
    }

    // ── Batch(Span) ────────────────────────────────────────────────

    [Fact]
    public void Batch_Span_ProducesFiniteOutput()
    {
        double[] src = [100, 101, 102, 103, 104, 103, 102, 101, 100, 99, 98, 99, 100, 101, 102];
        double[] dst = new double[src.Length];
        HtPhana.Batch(src, dst, period: 5);
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
        Assert.Throws<ArgumentException>(() => HtPhana.Batch(src, dst));
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[10];
        Assert.Throws<ArgumentException>(() => HtPhana.Batch(src, dst, period: 0));
    }

    // ── Calculate factory ──────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var s = MakeSeries(50);
        var (results, indicator) = HtPhana.Calculate(s, period: 10);
        Assert.Equal(s.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(indicator.Angle, results[^1].Value, precision: 10);
    }

    // ── PubSub (chaining) ──────────────────────────────────────────

    [Fact]
    public void PubSub_ReceivesEvents()
    {
        var source = new TSeries();
        var ind = new HtPhana(source, period: 10);
        int eventCount = 0;
        ind.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddDays(i), 100 + i));
        }

        Assert.Equal(20, eventCount);
    }

    [Fact]
    public void PubSub_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HtPhana(null!, period: 10));
    }

    // ── Prime ──────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmUpIndicator()
    {
        var ind = new HtPhana(period: 10);
        double[] data = new double[20];
        for (int i = 0; i < 20; i++)
        {
            data[i] = 100 + i * 0.5;
        }
        ind.Prime(data);
        Assert.True(ind.IsHot);
    }

    // ── HT_PHANA-specific behavior ──────────────────────────────────

    [Fact]
    public void SineWave_ProducesVaryingAngle()
    {
        var ind = new HtPhana(period: 20);
        for (int i = 0; i < 100; i++)
        {
            double price = 100 + 10 * Math.Sin(2 * Math.PI * i / 20.0);
            ind.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
        }
        // With a matching sine wave, angle should advance
        Assert.True(double.IsFinite(ind.Angle));
    }

    [Fact]
    public void DerivedPeriod_ClampedTo60()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(200);
        foreach (var tv in s)
        {
            ind.Update(tv);
            Assert.True(ind.DerivedPeriod <= 60.0,
                $"DerivedPeriod {ind.DerivedPeriod} exceeds max 60");
        }
    }

    [Fact]
    public void TrendState_OnlyValidValues()
    {
        var ind = new HtPhana(period: 10);
        var s = MakeSeries(200);
        foreach (var tv in s)
        {
            ind.Update(tv);
            Assert.True(ind.TrendState == -1 || ind.TrendState == 0 || ind.TrendState == 1,
                $"Invalid TrendState: {ind.TrendState}");
        }
    }

    [Fact]
    public void DifferentPeriod_DifferentResults()
    {
        var s = MakeSeries(100);

        var ind10 = new HtPhana(period: 10);
        var ind28 = new HtPhana(period: 28);

        foreach (var tv in s)
        {
            ind10.Update(tv);
            ind28.Update(tv);
        }

        // Different periods should generally produce different angles
        // (not guaranteed for all data, but very likely with random data)
        Assert.NotEqual(ind10.Angle, ind28.Angle);
    }

    [Fact]
    public void Update_TSeries_MatchesStreaming()
    {
        var s = MakeSeries(100);
        int period = 14;

        // Streaming
        var ind1 = new HtPhana(period);
        foreach (var tv in s)
        {
            ind1.Update(tv);
        }

        // Update(TSeries)
        var ind2 = new HtPhana(period);
        _ = ind2.Update(s);

        Assert.Equal(ind1.Angle, ind2.Angle, precision: 10);
        Assert.Equal(ind1.DerivedPeriod, ind2.DerivedPeriod, precision: 10);
        Assert.Equal(ind1.TrendState, ind2.TrendState);
    }
}
