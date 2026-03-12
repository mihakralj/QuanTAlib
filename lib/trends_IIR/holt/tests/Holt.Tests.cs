namespace QuanTAlib.Tests;

public class HoltTests
{
    private readonly GBM _gbm = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);

    // === A) Constructor validation ===

    [Fact]
    public void Holt_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Holt(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Holt_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Holt(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Holt_GammaTooLow_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Holt(10, gamma: -0.1));
        Assert.Equal("gamma", ex.ParamName);
    }

    [Fact]
    public void Holt_GammaTooHigh_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Holt(10, gamma: 1.1));
        Assert.Equal("gamma", ex.ParamName);
    }

    [Fact]
    public void Holt_ValidConstruction_SetsName()
    {
        var holt = new Holt(10);
        Assert.Equal("Holt(10)", holt.Name);
    }

    [Fact]
    public void Holt_ValidConstruction_WithGamma_SetsName()
    {
        var holt = new Holt(10, gamma: 0.3);
        Assert.Equal("Holt(10,0.30)", holt.Name);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Holt_Update_ReturnsTValue()
    {
        var holt = new Holt(10);
        TValue result = holt.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Holt_FirstBar_ReturnsInput()
    {
        var holt = new Holt(10);
        TValue result = holt.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, 10);
    }

    [Fact]
    public void Holt_Last_IsAccessible()
    {
        var holt = new Holt(10);
        holt.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(holt.Last.Value));
    }

    // === C) State + bar correction ===

    [Fact]
    public void Holt_IsNew_True_AdvancesState()
    {
        var holt = new Holt(5);
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < series.Count; i++)
        {
            holt.Update(series[i]);
        }

        double valueAfterAll = holt.Last.Value;
        holt.Update(new TValue(DateTime.UtcNow, 999.0), isNew: true);
        Assert.NotEqual(valueAfterAll, holt.Last.Value);
    }

    [Fact]
    public void Holt_IsNew_False_RollsBack()
    {
        var holt = new Holt(5);
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < series.Count; i++)
        {
            holt.Update(series[i]);
        }

        double baseline = holt.Last.Value;
        holt.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        _ = holt.Last.Value;

        // Correction should produce a different value from baseline (999 != last close)
        // But the state should have been rolled back first
        holt.Update(series[^1], isNew: false);
        Assert.Equal(baseline, holt.Last.Value, 10);
    }

    [Fact]
    public void Holt_IterativeCorrections_Restore()
    {
        var holt = new Holt(5);
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < series.Count - 1; i++)
        {
            holt.Update(series[i]);
        }

        // Feed last bar as new
        holt.Update(series[^1], isNew: true);
        double expected = holt.Last.Value;

        // Correct it multiple times
        for (int j = 0; j < 5; j++)
        {
            holt.Update(series[^1], isNew: false);
        }

        Assert.Equal(expected, holt.Last.Value, 10);
    }

    [Fact]
    public void Holt_Reset_ClearsState()
    {
        var holt = new Holt(10);
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < series.Count; i++)
        {
            holt.Update(series[i]);
        }

        Assert.True(holt.IsHot);
        holt.Reset();
        Assert.False(holt.IsHot);
        Assert.Equal(default, holt.Last);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void Holt_IsHot_FlipsAtWarmup()
    {
        var holt = new Holt(10);
        var bars = _gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        for (int i = 0; i < 9; i++)
        {
            holt.Update(series[i]);
            Assert.False(holt.IsHot);
        }

        holt.Update(series[9]);
        Assert.True(holt.IsHot);
    }

    [Fact]
    public void Holt_WarmupPeriod_MatchesPeriod()
    {
        var holt = new Holt(15);
        Assert.Equal(15, holt.WarmupPeriod);
    }

    // === E) Robustness ===

    [Fact]
    public void Holt_NaN_UsesLastValid()
    {
        var holt = new Holt(5);
        holt.Update(new TValue(DateTime.UtcNow, 100.0));
        holt.Update(new TValue(DateTime.UtcNow, 101.0));
        _ = holt.Last.Value;

        holt.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(holt.Last.Value));
    }

    [Fact]
    public void Holt_Infinity_UsesLastValid()
    {
        var holt = new Holt(5);
        holt.Update(new TValue(DateTime.UtcNow, 100.0));
        holt.Update(new TValue(DateTime.UtcNow, 101.0));

        holt.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(holt.Last.Value));
    }

    [Fact]
    public void Holt_BatchNaN_Safe()
    {
        double[] src = [100, double.NaN, 102, double.NaN, 104];
        double[] dst = new double[5];
        Holt.Batch(src, dst, 3);
        for (int i = 0; i < dst.Length; i++)
        {
            Assert.True(double.IsFinite(dst[i]), $"dst[{i}] is not finite");
        }
    }

    // === F) Consistency (4 modes) ===

    [Fact]
    public void Holt_AllModes_Match()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        int period = 10;

        // Mode 1: Streaming
        var holtStream = new Holt(period);
        for (int i = 0; i < series.Count; i++)
        {
            holtStream.Update(series[i]);
        }
        double streamResult = holtStream.Last.Value;

        // Mode 2: Batch TSeries
        TSeries batchResult = Holt.Batch(series, period);
        double batchLast = batchResult[^1].Value;

        // Mode 3: Span
        double[] src = new double[series.Count];
        double[] dst = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            src[i] = series[i].Value;
        }
        Holt.Batch(src, dst, period);
        double spanLast = dst[^1];

        // Mode 4: Event-based
        var holtEvent = new Holt(period);
        double eventResult = 0;
        holtEvent.Pub += (object? s, in TValueEventArgs e) => { eventResult = e.Value.Value; };
        for (int i = 0; i < series.Count; i++)
        {
            holtEvent.Update(series[i]);
        }

        Assert.Equal(streamResult, batchLast, 10);
        Assert.Equal(streamResult, spanLast, 10);
        Assert.Equal(streamResult, eventResult, 10);
    }

    // === G) Span API tests ===

    [Fact]
    public void Holt_Span_MismatchedLength_Throws()
    {
        double[] src = [1, 2, 3];
        double[] dst = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Holt.Batch(src, dst, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Holt_Span_EmptyInput_NoOutput()
    {
        double[] src = [];
        double[] dst = [];
        Holt.Batch(src, dst, 5);
        Assert.Empty(dst);
    }

    [Fact]
    public void Holt_Span_ZeroPeriod_Throws()
    {
        double[] src = [1, 2, 3];
        double[] dst = new double[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => Holt.Batch(src, dst, 0));
    }

    [Fact]
    public void Holt_Span_MatchesTSeries()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        int period = 10;

        TSeries bts = Holt.Batch(series, period);

        double[] src = new double[series.Count];
        double[] dst = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            src[i] = series[i].Value;
        }
        Holt.Batch(src, dst, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(bts[i].Value, dst[i], 10);
        }
    }

    // === H) Chainability ===

    [Fact]
    public void Holt_Pub_Fires()
    {
        var holt = new Holt(5);
        int count = 0;
        holt.Pub += (object? s, in TValueEventArgs e) => { count++; };

        holt.Update(new TValue(DateTime.UtcNow, 100.0));
        holt.Update(new TValue(DateTime.UtcNow, 101.0));
        Assert.Equal(2, count);
    }

    [Fact]
    public void Holt_EventChaining_Works()
    {
        var holt1 = new Holt(5);
        var holt2 = new Holt(holt1, 3);

        holt1.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(holt2.Last.Value));
    }

    // === Holt-specific tests ===

    [Fact]
    public void Holt_ConstantInput_ConvergesToLevel()
    {
        var holt = new Holt(10);
        double constant = 50.0;

        for (int i = 0; i < 200; i++)
        {
            holt.Update(new TValue(DateTime.UtcNow, constant));
        }

        // With constant input, trend -> 0, level -> constant, output -> constant
        Assert.Equal(constant, holt.Last.Value, 6);
    }

    [Fact]
    public void Holt_Calculate_ReturnsHotInstance()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Holt.Calculate(bars.Close, 10);

        Assert.True(indicator.IsHot);
        Assert.Equal(100, results.Count);
    }

    [Fact]
    public void Holt_Prime_SetsState()
    {
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var holtPrimed = new Holt(10);
        double[] values = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            values[i] = series[i].Value;
        }
        holtPrimed.Prime(values);

        var holtStreamed = new Holt(10);
        for (int i = 0; i < series.Count; i++)
        {
            holtStreamed.Update(series[i]);
        }

        Assert.Equal(holtStreamed.Last.Value, holtPrimed.Last.Value, 10);
    }

    [Fact]
    public void Holt_GammaZero_EqualsAutoGamma()
    {
        var bars = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var holt0 = new Holt(10, gamma: 0);
        var holtAuto = new Holt(10);

        for (int i = 0; i < series.Count; i++)
        {
            holt0.Update(series[i]);
            holtAuto.Update(series[i]);
        }

        Assert.Equal(holt0.Last.Value, holtAuto.Last.Value, 15);
    }

    [Fact]
    public void Holt_DifferentGamma_ProducesDifferentOutput()
    {
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var holt1 = new Holt(10, gamma: 0.1);
        var holt2 = new Holt(10, gamma: 0.9);

        for (int i = 0; i < series.Count; i++)
        {
            holt1.Update(series[i]);
            holt2.Update(series[i]);
        }

        Assert.NotEqual(holt1.Last.Value, holt2.Last.Value);
    }
}
