namespace QuanTAlib.Tests;

public sealed class SakTests
{
    // ── A) Constructor validation ─────────────────────────────────────────

    [Fact]
    public void Sak_Constructor_Period_NonSma_ThrowsIfTooSmall()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sak("EMA", period: 2));
        Assert.Equal("period", ex.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Sak("Butter", period: 1));
        Assert.Equal("period", ex2.ParamName);

        // period == 3 should be fine
        var sak = new Sak("EMA", period: 3);
        Assert.NotNull(sak);
    }

    [Fact]
    public void Sak_Constructor_N_ThrowsIfLessThanOne()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sak("SMA", period: 20, n: 0));
        Assert.Equal("n", ex.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Sak("EMA", period: 20, n: 0));
        Assert.Equal("n", ex2.ParamName);
    }

    [Fact]
    public void Sak_Constructor_Delta_ThrowsIfBandwidthTooLarge()
    {
        // delta/period > 0.25 → invalid for BP/BS
        var ex = Assert.Throws<ArgumentException>(() => new Sak("BP", period: 20, delta: 6.0));
        Assert.Equal("delta", ex.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new Sak("BS", period: 20, delta: 6.0));
        Assert.Equal("delta", ex2.ParamName);

        // delta = 0.1, period = 20 → 0.1/20 = 0.005 ≤ 0.25 → fine
        var sak = new Sak("BP", period: 20, delta: 0.1);
        Assert.NotNull(sak);
    }

    [Fact]
    public void Sak_Constructor_UnknownFilterType_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sak("UNKNOWN", period: 20));
        Assert.Equal("filterType", ex.ParamName);
    }

    // ── B) Basic calculation ──────────────────────────────────────────────

    [Theory]
    [InlineData("EMA")]
    [InlineData("SMA")]
    [InlineData("Gauss")]
    [InlineData("Butter")]
    [InlineData("Smooth")]
    [InlineData("HP")]
    [InlineData("2PHP")]
    [InlineData("BP")]
    [InlineData("BS")]
    public void Sak_AllModes_ReturnsFiniteValue(string mode)
    {
        var sak = new Sak(mode, period: 10, n: 5, delta: 0.1);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var result = sak.Update(new TValue(now.AddSeconds(i), 100.0 + i));
            Assert.True(double.IsFinite(result.Value), $"Mode={mode} bar={i} produced non-finite value");
        }
    }

    [Fact]
    public void Sak_EMA_KnownValueCheck()
    {
        // EMA SAK: alpha = (cos(2π/10) + sin(2π/10) - 1) / cos(2π/10)
        // Constant input 100 → should converge to 100
        var sak = new Sak("EMA", period: 10);
        var now = DateTime.UtcNow;
        TValue result = default;
        for (int i = 0; i < 200; i++)
        {
            result = sak.Update(new TValue(now.AddSeconds(i), 100.0));
        }
        Assert.Equal(100.0, result.Value, 1e-6);
    }

    [Fact]
    public void Sak_BP_KnownValueCheck()
    {
        // Default BP (period=20, delta=0.1): constant input → output should converge toward 0
        var sak = new Sak("BP", period: 20, delta: 0.1);
        var now = DateTime.UtcNow;
        TValue result = default;
        for (int i = 0; i < 500; i++)
        {
            result = sak.Update(new TValue(now.AddSeconds(i), 100.0));
        }
        // BP is a band-pass; constant DC should be attenuated toward 0
        Assert.True(Math.Abs(result.Value) < 1.0, $"BP constant input did not converge near 0 (got {result.Value})");
    }

    [Fact]
    public void Sak_SMA_CorrectAverage()
    {
        var sak = new Sak("SMA", period: 20, n: 3);
        var now = DateTime.UtcNow;
        sak.Update(new TValue(now, 10.0));
        sak.Update(new TValue(now.AddSeconds(1), 20.0));
        var result = sak.Update(new TValue(now.AddSeconds(2), 30.0));
        // SMA(3) of 10,20,30 = 20
        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void Sak_Name_IsCorrect()
    {
        var sak = new Sak("BP", period: 20);
        Assert.Equal("Sak(BP,20)", sak.Name);
    }

    // ── C) State + bar correction ─────────────────────────────────────────

    [Fact]
    public void Sak_IsNew_True_AdvancesState()
    {
        var sak = new Sak("EMA", period: 10);
        var now = DateTime.UtcNow;

        sak.Update(new TValue(now, 100.0), isNew: true);
        double v1 = sak.Last.Value;

        sak.Update(new TValue(now.AddSeconds(1), 110.0), isNew: true);
        double v2 = sak.Last.Value;

        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void Sak_IsNew_False_RewritesLastBar()
    {
        var sak = new Sak("EMA", period: 10);
        var now = DateTime.UtcNow;

        sak.Update(new TValue(now, 100.0), isNew: true);
        sak.Update(new TValue(now.AddSeconds(1), 110.0), isNew: true);
        double beforeUpdate = sak.Last.Value;

        sak.Update(new TValue(now.AddSeconds(1), 120.0), isNew: false);
        double afterUpdate = sak.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Sak_IterativeCorrections_RestoreOriginalValue()
    {
        var sak = new Sak("Butter", period: 10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            sak.Update(tenthInput, isNew: true);
        }
        double afterTen = sak.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            sak.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        double restored = sak.Update(tenthInput, isNew: false).Value;
        Assert.Equal(afterTen, restored, 1e-10);
    }

    [Fact]
    public void Sak_Reset_ClearsState()
    {
        var sak = new Sak("EMA", period: 10);
        var now = DateTime.UtcNow;

        sak.Update(new TValue(now, 100.0));
        sak.Update(new TValue(now.AddSeconds(1), 105.0));

        sak.Reset();

        Assert.Equal(0.0, sak.Last.Value);
        Assert.False(sak.IsHot);

        sak.Update(new TValue(now.AddSeconds(2), 50.0));
        Assert.NotEqual(0.0, sak.Last.Value);
    }

    // ── D) Warmup / IsHot ────────────────────────────────────────────────

    [Fact]
    public void Sak_IIR_IsHot_AfterThreeBars()
    {
        var sak = new Sak("EMA", period: 10);
        Assert.False(sak.IsHot);

        var now = DateTime.UtcNow;
        sak.Update(new TValue(now, 100.0));
        sak.Update(new TValue(now.AddSeconds(1), 100.0));
        Assert.False(sak.IsHot);

        sak.Update(new TValue(now.AddSeconds(2), 100.0));
        Assert.True(sak.IsHot);
    }

    [Fact]
    public void Sak_SMA_IsHot_AfterNBars()
    {
        const int n = 5;
        var sak = new Sak("SMA", period: 20, n: n);
        Assert.False(sak.IsHot);

        var now = DateTime.UtcNow;
        for (int i = 0; i < n - 1; i++)
        {
            sak.Update(new TValue(now.AddSeconds(i), 100.0));
            Assert.False(sak.IsHot);
        }

        sak.Update(new TValue(now.AddSeconds(n - 1), 100.0));
        Assert.True(sak.IsHot);
    }

    [Fact]
    public void Sak_WarmupPeriod_IIR_IsThree()
    {
        var sak = new Sak("Gauss", period: 20);
        Assert.Equal(3, sak.WarmupPeriod);
    }

    [Fact]
    public void Sak_WarmupPeriod_SMA_IsN()
    {
        var sak = new Sak("SMA", period: 20, n: 7);
        Assert.Equal(7, sak.WarmupPeriod);
    }

    // ── E) Robustness ────────────────────────────────────────────────────

    [Fact]
    public void Sak_NaN_Input_UsesLastValidValue()
    {
        var sak = new Sak("EMA", period: 10);
        var now = DateTime.UtcNow;

        sak.Update(new TValue(now, 100.0));
        sak.Update(new TValue(now.AddSeconds(1), 110.0));

        var result = sak.Update(new TValue(now.AddSeconds(2), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Sak_Infinity_Input_UsesLastValidValue()
    {
        var sak = new Sak("Butter", period: 10);
        var now = DateTime.UtcNow;

        sak.Update(new TValue(now, 100.0));
        sak.Update(new TValue(now.AddSeconds(1), 110.0));

        var result = sak.Update(new TValue(now.AddSeconds(2), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));

        result = sak.Update(new TValue(now.AddSeconds(3), double.NegativeInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Sak_BatchNaN_IsFiniteOutput()
    {
        var sak = new Sak("EMA", period: 10);
        var data = new double[] { 100, double.NaN, 102, double.NaN, double.NaN, 105 };
        var now = DateTime.UtcNow;
        foreach (var d in data)
        {
            var r = sak.Update(new TValue(now, d));
            Assert.True(double.IsFinite(r.Value));
        }
    }

    // ── F) Consistency (batch == streaming == span == eventing) ──────────

    [Theory]
    [InlineData("EMA")]
    [InlineData("SMA")]
    [InlineData("Gauss")]
    [InlineData("Butter")]
    [InlineData("Smooth")]
    [InlineData("HP")]
    [InlineData("2PHP")]
    [InlineData("BP")]
    [InlineData("BS")]
    public void Sak_AllModes_AllApiModes_Match(string mode)
    {
        const int period = 10;
        const int n = 5;
        const double delta = 0.1;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch (TSeries)
        var batchResult = Sak.Calculate(series, mode, period, n, delta).Results;
        double expected = batchResult.Last.Value;

        // 2. Span
        var srcArray = series.Values.ToArray();
        var outArray = new double[srcArray.Length];
        Sak.Calculate(srcArray.AsSpan(), outArray.AsSpan(), mode, period, n, delta);
        double spanResult = outArray[^1];

        // 3. Streaming
        var streaming = new Sak(mode, period, n, delta);
        for (int i = 0; i < series.Count; i++)
        {
            streaming.Update(series[i]);
        }
        double streamingResult = streaming.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventing = new Sak(pubSource, mode, period, n, delta);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventing.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    // ── G) Span API ───────────────────────────────────────────────────────

    [Fact]
    public void Sak_Span_ThrowsOnLengthMismatch()
    {
        var src = new double[10];
        var out_ = new double[9];
        var ex = Assert.Throws<ArgumentException>(() =>
            Sak.Calculate(src.AsSpan(), out_.AsSpan(), "EMA", 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Sak_Span_HandlesEmpty()
    {
        // Should not throw
        Sak.Calculate(ReadOnlySpan<double>.Empty, Span<double>.Empty, "EMA", 10);
        Assert.True(true);
    }

    [Fact]
    public void Sak_Span_HandlesNaN()
    {
        var src = new double[] { 100, double.NaN, 102, 103, 104 };
        var output = new double[5];
        Sak.Calculate(src.AsSpan(), output.AsSpan(), "EMA", 3);
        foreach (var v in output)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Sak_Span_LargeData_NoStackOverflow()
    {
        const int size = 10_000;
        var src = new double[size];
        var output = new double[size];
        for (int i = 0; i < size; i++)
        {
            src[i] = 100.0 + (i * 0.01);
        }

        // Should not throw StackOverflowException
        Sak.Calculate(src.AsSpan(), output.AsSpan(), "BP", 20);
        Assert.True(double.IsFinite(output[^1]));
    }

    // ── H) Chainability ───────────────────────────────────────────────────

    [Fact]
    public void Sak_Pub_Fires_OnUpdate()
    {
        var sak = new Sak("EMA", period: 10);
        int fireCount = 0;
        sak.Pub += (_, in _) => fireCount++;

        var now = DateTime.UtcNow;
        sak.Update(new TValue(now, 100.0));
        sak.Update(new TValue(now.AddSeconds(1), 105.0));

        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void Sak_EventChaining_Works()
    {
        var source = new TSeries();
        var sakEma = new Sak(source, "EMA", period: 10);

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(now.AddSeconds(i), 100.0 + i));
        }

        Assert.True(double.IsFinite(sakEma.Last.Value));
        Assert.True(sakEma.IsHot);
    }
}
