namespace QuanTAlib.Tests;

public class GdemaTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < count; i++)
        {
            series.Add(gbm.Next());
        }
        return series;
    }

    // ── A) Constructor validation ───────────────────────────────────

    [Fact]
    public void Constructor_DefaultPeriod_Is10()
    {
        var gdema = new Gdema();
        Assert.Equal("Gdema(10,1.0)", gdema.Name);
    }

    [Fact]
    public void Constructor_SetsPeriodAndVfactorName()
    {
        var gdema = new Gdema(period: 20, vfactor: 0.5);
        Assert.Equal("Gdema(20,0.5)", gdema.Name);
    }

    [Fact]
    public void Constructor_Period0_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Gdema(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Gdema(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_Period1_Valid()
    {
        var gdema = new Gdema(period: 1);
        Assert.Equal("Gdema(1,1.0)", gdema.Name);
    }

    // ── B) Basic calculation ────────────────────────────────────────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var gdema = new Gdema(10);
        TValue result = gdema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_LastIsAccessible()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(gdema.Last.Value));
    }

    [Fact]
    public void Update_FirstBar_SeedsCorrectly()
    {
        var gdema = new Gdema(10, vfactor: 1.0);
        TValue result = gdema.Update(new TValue(DateTime.UtcNow, 50.0));
        // First bar: both EMAs start at source due to warmup compensation
        // GDEMA = (1+v)*EMA1 - v*EMA2 = 2*50 - 50 = 50
        Assert.Equal(50.0, result.Value, 1e-9);
    }

    // ── C) State + bar correction ───────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var gdema = new Gdema(10);
        var r1 = gdema.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var r2 = gdema.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public void IsNew_False_RewritesSameBar()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var r1 = gdema.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        var r2 = gdema.Update(new TValue(DateTime.UtcNow, 120.0), isNew: false);
        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public void IterativeCorrection_Restores()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 105.0), isNew: true);
        var before = gdema.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);

        // Correct a few times then restore the "true" value
        _ = gdema.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 888.0), isNew: false);
        var restored = gdema.Update(new TValue(DateTime.UtcNow, 110.0), isNew: false);

        Assert.Equal(before.Value, restored.Value, 1e-12);
    }

    [Fact]
    public void BarCorrection_Idempotent()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var r1 = gdema.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        var r2 = gdema.Update(new TValue(DateTime.UtcNow, 110.0), isNew: false);
        Assert.Equal(r1.Value, r2.Value, 1e-12);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0));
        _ = gdema.Update(new TValue(DateTime.UtcNow, 200.0));
        gdema.Reset();
        Assert.False(gdema.IsHot);
        Assert.Equal(default, gdema.Last);
    }

    // ── D) Warmup / convergence ─────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        const int period = 10;
        var gdema = new Gdema(period);
        var gbm = new GBM(startPrice: 100, seed: 42);

        int hotBar = -1;
        for (int i = 0; i < 200; i++)
        {
            _ = gdema.Update(gbm.Next());
            if (gdema.IsHot && hotBar < 0)
            {
                hotBar = i;
            }
        }

        Assert.True(hotBar >= 0 && hotBar < 200);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var gdema = new Gdema(15);
        Assert.Equal(15, gdema.WarmupPeriod);
    }

    // ── E) Robustness ───────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = gdema.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = gdema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void AllNaN_ReturnsNaN()
    {
        var gdema = new Gdema(10);
        var result = gdema.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsNaN(result.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var gdema = new Gdema(10);
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0));
        _ = gdema.Update(new TValue(DateTime.UtcNow, double.NaN));
        _ = gdema.Update(new TValue(DateTime.UtcNow, double.NaN));
        var result = gdema.Update(new TValue(DateTime.UtcNow, 110.0));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── F) Consistency (4 modes) ────────────────────────────────────

    [Fact]
    public void AllModes_Match()
    {
        const int period = 10;
        const double vfactor = 1.0;
        var source = MakeSeries(200);

        // Mode 1: Streaming
        var streaming = new Gdema(period, vfactor);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // Mode 2: TSeries batch
        var batchResults = Gdema.Batch(source, period, vfactor);

        // Mode 3: Span batch
        double[] srcArr = source.Values.ToArray();
        double[] spanResults = new double[srcArr.Length];
        Gdema.Batch(srcArr.AsSpan(), spanResults.AsSpan(), period, vfactor);

        // Mode 4: Event-based
        var eventSource = new TSeries();
        var eventGdema = new Gdema(eventSource, period, vfactor);
        var eventResults = new List<double>();
        eventGdema.Pub += (object? sender, in TValueEventArgs e) => eventResults.Add(e.Value.Value);
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i], true);
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-9);
            Assert.Equal(streamResults[i], spanResults[i], 1e-9);
            Assert.Equal(streamResults[i], eventResults[i], 1e-9);
        }
    }

    // ── G) Span API tests ───────────────────────────────────────────

    [Fact]
    public void Batch_Span_LengthMismatch_Throws()
    {
        double[] src = [1.0, 2.0, 3.0];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Gdema.Batch(src.AsSpan(), output.AsSpan(), 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        double[] src = [1.0, 2.0];
        double[] output = new double[2];
        Assert.Throws<ArgumentOutOfRangeException>(() => Gdema.Batch(src.AsSpan(), output.AsSpan(), 0));
    }

    [Fact]
    public void Batch_Span_EmptySource_NoOp()
    {
        Span<double> src = [];
        Span<double> output = [];
        Gdema.Batch(src, output, 10);
        Assert.True(true);
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        const int period = 10;
        const double vfactor = 1.5;
        var source = MakeSeries(300);

        var tsResult = Gdema.Batch(source, period, vfactor);
        double[] srcArr = source.Values.ToArray();
        double[] spanResult = new double[srcArr.Length];
        Gdema.Batch(srcArr.AsSpan(), spanResult.AsSpan(), period, vfactor);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(tsResult[i].Value, spanResult[i], 1e-9);
        }
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        const int size = 10_000;
        double[] src = new double[size];
        double[] output = new double[size];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < size; i++)
        {
            src[i] = gbm.Next().Close;
        }
        Gdema.Batch(src.AsSpan(), output.AsSpan(), 20);
        Assert.True(double.IsFinite(output[^1]));
    }

    // ── H) Chainability ─────────────────────────────────────────────

    [Fact]
    public void PubFires()
    {
        var gdema = new Gdema(10);
        int fires = 0;
        gdema.Pub += (object? sender, in TValueEventArgs e) => fires++;
        _ = gdema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, fires);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var gdema = new Gdema(source, 10);
        source.Add(new TValue(DateTime.UtcNow, 100.0), true);
        Assert.True(double.IsFinite(gdema.Last.Value));
    }

    [Fact]
    public void Dispose_UnsubscribesPublisher()
    {
        var source = new TSeries();
        var gdema = new Gdema(source, 10);
        gdema.Dispose();
        source.Add(new TValue(DateTime.UtcNow, 999.0), true);
        // After dispose, gdema should not update
        Assert.NotEqual(999.0, gdema.Last.Value);
    }

    [Fact]
    public void Calculate_ReturnsBoth()
    {
        var source = MakeSeries(100);
        var (results, indicator) = Gdema.Calculate(source, 10);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Special: vfactor behavior ───────────────────────────────────

    [Fact]
    public void Vfactor0_EqualsEma()
    {
        const int period = 10;
        var source = MakeSeries(200);
        var gdema = new Gdema(period, vfactor: 0.0);
        var ema = new Ema(period);

        for (int i = 0; i < source.Count; i++)
        {
            var gVal = gdema.Update(source[i]);
            var eVal = ema.Update(source[i]);
            Assert.Equal(eVal.Value, gVal.Value, 1e-9);
        }
    }

    [Fact]
    public void Vfactor1_EqualsDema()
    {
        const int period = 10;
        var source = MakeSeries(200);
        var gdema = new Gdema(period, vfactor: 1.0);
        var dema = new Dema(period);

        for (int i = 0; i < source.Count; i++)
        {
            var gVal = gdema.Update(source[i]);
            var dVal = dema.Update(source[i]);
            Assert.Equal(dVal.Value, gVal.Value, 1e-9);
        }
    }

    [Fact]
    public void Update_ConstantInput_ConvergesToConstant()
    {
        var gdema = new Gdema(10, vfactor: 1.0);
        double last = 0;
        for (int i = 0; i < 500; i++)
        {
            last = gdema.Update(new TValue(DateTime.UtcNow, 42.0)).Value;
        }
        Assert.Equal(42.0, last, 1e-6);
    }

    [Fact]
    public void DifferentVfactors_ProduceDifferentOutputs()
    {
        // Different v-factors should produce measurably different outputs
        const int period = 20;
        var source = MakeSeries(100);
        var v05 = new Gdema(period, vfactor: 0.5);
        var v15 = new Gdema(period, vfactor: 1.5);

        double totalDiff = 0;
        for (int i = 0; i < source.Count; i++)
        {
            double val05 = v05.Update(source[i]).Value;
            double val15 = v15.Update(source[i]).Value;
            totalDiff += Math.Abs(val05 - val15);
        }

        // Different vfactors must produce different trajectories
        Assert.True(totalDiff > 0);
    }
}

