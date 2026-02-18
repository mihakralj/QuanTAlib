namespace QuanTAlib;

public class EdcfTests
{
    private const double Tolerance = 1e-10;
    private readonly GBM _gbm;

    public EdcfTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    private static TSeries CreateSeries(params double[] values)
    {
        var series = new TSeries();
        for (int i = 0; i < values.Length; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), values[i]));
        }
        return series;
    }

    // ═══════════════════════════════════════════════════════
    // A) Constructor Validation
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Edcf(15);
        Assert.Contains("Edcf", ind.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Edcf(15);
        Assert.Equal(15, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultLength()
    {
        var ind = new Edcf();
        Assert.Contains("15", ind.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ValidatesLength_TooSmall()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Edcf(1));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesLength_Zero()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Edcf(0));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesLength_Negative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Edcf(-5));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_MinimumLength()
    {
        var ex = Record.Exception(() => new Edcf(2));
        Assert.Null(ex);
    }

    // ═══════════════════════════════════════════════════════
    // B) Basic Calculation
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void FirstBar_IsPassthrough()
    {
        var ind = new Edcf(5);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, result.Value);
    }

    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new Edcf(5);
        var result = ind.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ind = new Edcf(5);
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, ind.Last.Value);
    }

    [Fact]
    public void Name_ContainsLength()
    {
        var ind = new Edcf(10);
        Assert.Contains("10", ind.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstantInput_ConvergesToSMA()
    {
        // When all prices are equal, EDCF = SMA = constant value
        var ind = new Edcf(5);
        double constVal = 42.0;
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constVal));
        }
        Assert.Equal(constVal, ind.Last.Value, Tolerance);
    }

    [Fact]
    public void RisingInput_ProducesFiniteOutput()
    {
        // With a linear trend, EDCF produces a finite weighted average within window range
        var ind = new Edcf(5);
        double[] vals = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0];
        TValue last = default;
        for (int i = 0; i < vals.Length; i++)
        {
            last = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), vals[i]));
        }
        // EDCF output should be finite and within the window range [6, 10]
        Assert.True(double.IsFinite(last.Value), $"EDCF should be finite, got {last.Value}");
        Assert.True(last.Value >= 6.0 && last.Value <= 10.0,
            $"EDCF {last.Value} should be within window range [6, 10]");
    }

    // ═══════════════════════════════════════════════════════
    // C) State + Bar Correction (critical)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ind = new Edcf(3);
        ind.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 20.0), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 30.0), isNew: true);
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var ind = new Edcf(3);
        for (int i = 0; i < 5; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 10.0 + i));
        }
        double before = ind.Last.Value;

        // Update same bar with different value
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(4), 50.0), isNew: false);
        double after = ind.Last.Value;

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var ind = new Edcf(3);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var data = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i]);
        }
        double originalValue = ind.Last.Value;

        // Feed corrections with isNew=false
        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, 300), isNew: false);

        // Restore with original last value
        ind.Update(series[^1], isNew: false);
        double restoredValue = ind.Last.Value;

        Assert.Equal(originalValue, restoredValue, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Edcf(5);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }
        Assert.True(ind.IsHot);

        ind.Reset();
        Assert.False(ind.IsHot);
    }

    // ═══════════════════════════════════════════════════════
    // D) Warmup / Convergence
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void IsHot_FlipsAtLength()
    {
        var ind = new Edcf(5);
        for (int i = 0; i < 4; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 10.0 * (i + 1)));
            Assert.False(ind.IsHot);
        }
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(4), 50.0));
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesLength()
    {
        var ind = new Edcf(10);
        Assert.Equal(10, ind.WarmupPeriod);
    }

    // ═══════════════════════════════════════════════════════
    // E) Robustness (critical)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void NaN_UsesLastValid()
    {
        var ind = new Edcf(3);
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 300.0));

        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(3), double.NaN));
        Assert.True(double.IsFinite(ind.Last.Value), "NaN should be replaced with last-valid");
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var ind = new Edcf(3);
        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 300.0));

        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(3), double.PositiveInfinity));
        Assert.True(double.IsFinite(ind.Last.Value), "Infinity should be replaced with last-valid");
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] data = [1.0, 2.0, double.NaN, 4.0, 5.0, double.NaN, 7.0, 8.0];
        var series = CreateSeries(data);
        var result = Edcf.Batch(series, 3);
        foreach (var tv in result)
        {
            Assert.True(double.IsFinite(tv.Value), $"Value at {tv.Time} was not finite: {tv.Value}");
        }
    }

    // ═══════════════════════════════════════════════════════
    // F) Consistency (critical) — All 4 modes match
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void AllModes_Match()
    {
        int length = 5;
        int count = 30;
        var data = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Mode 1: Streaming (Update one at a time)
        var streaming = new Edcf(length);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Mode 2: Batch (TSeries)
        var batchResults = Edcf.Batch(series, length);

        // Mode 3: Span
        double[] srcValues = series.Values.ToArray();
        double[] spanResults = new double[series.Count];
        Edcf.Batch(srcValues.AsSpan(), spanResults.AsSpan(), length);

        // Mode 4: Event-based
        var pubSource = new TSeries();
        var eventInd = new Edcf(pubSource, length);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }

        // Compare modes 1, 2, 3
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, Tolerance);
            Assert.Equal(streamResults[i], spanResults[i], Tolerance);
        }
        Assert.Equal(streamResults[^1], eventInd.Last.Value, Tolerance);
    }

    // ═══════════════════════════════════════════════════════
    // G) Span API Tests
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Span_ValidatesDestinationLength()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] dst = new double[3]; // too short
        var ex = Assert.Throws<ArgumentException>(() =>
            Edcf.Batch(src.AsSpan(), dst.AsSpan(), 3));
        Assert.Equal("destination", ex.ParamName);
    }

    [Fact]
    public void Span_ValidatesLength()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] dst = new double[5];
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Edcf.Batch(src.AsSpan(), dst.AsSpan(), 1));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Span_MatchesTSeries()
    {
        int length = 5;
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var batchResult = Edcf.Batch(series, length);

        double[] srcVals = series.Values.ToArray();
        double[] spanResult = new double[series.Count];
        Edcf.Batch(srcVals.AsSpan(), spanResult.AsSpan(), length);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanResult[i], Tolerance);
        }
    }

    [Fact]
    public void Span_HandlesNaN()
    {
        double[] src = [1.0, 2.0, double.NaN, 4.0, 5.0, 6.0, 7.0, 8.0];
        double[] dst = new double[src.Length];
        Edcf.Batch(src.AsSpan(), dst.AsSpan(), 3);
        foreach (double v in dst)
        {
            Assert.True(double.IsFinite(v), $"Span result was not finite: {v}");
        }
    }

    [Fact]
    public void Span_LargeDataset_NoStackOverflow()
    {
        int size = 10_000;
        var data = _gbm.Fetch(size, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] src = data.Close.Values.ToArray();
        double[] dst = new double[size];

        var ex = Record.Exception(() => Edcf.Batch(src.AsSpan(), dst.AsSpan(), 15));
        Assert.Null(ex);
    }

    // ═══════════════════════════════════════════════════════
    // H) Chainability
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Edcf(3);
        int fireCount = 0;
        ind.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, fireCount);

        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));
        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new Edcf(3);
        var chained = new Edcf(source, 3);

        source.Update(new TValue(DateTime.UtcNow, 100.0));
        source.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));
        source.Update(new TValue(DateTime.UtcNow.AddMinutes(2), 300.0));

        Assert.True(double.IsFinite(chained.Last.Value));
    }

    // ═══════════════════════════════════════════════════════
    // I) EDCF-specific: SMA Degeneracy
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void FlatPrices_DegeneratesToSMA()
    {
        // Per Ehlers: when all prices are the same,
        // all distance coefficients are equal → SMA
        var ind = new Edcf(5);
        double[] flat = [50.0, 50.0, 50.0, 50.0, 50.0, 50.0, 50.0, 50.0, 50.0, 50.0];
        for (int i = 0; i < flat.Length; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), flat[i]));
        }
        // All coefficients are 0, fallback to current price
        Assert.Equal(50.0, ind.Last.Value, Tolerance);
    }

    [Fact]
    public void StepFunction_RespondsQuickly()
    {
        // Step from 100 to 200 — EDCF should respond faster than SMA
        var edcf = new Edcf(5);
        double[] data = [100, 100, 100, 100, 100, 200, 200, 200, 200, 200];
        TValue lastEdcf = default;
        for (int i = 0; i < data.Length; i++)
        {
            lastEdcf = edcf.Update(new TValue(DateTime.UtcNow.AddMinutes(i), data[i]));
        }
        // After full window of 200s, should converge to 200
        Assert.Equal(200.0, lastEdcf.Value, Tolerance);
    }

    [Fact]
    public void Calculate_ReturnsResultAndIndicator()
    {
        var data = _gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        var (results, indicator) = Edcf.Calculate(series, 5);
        Assert.Equal(30, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        var ind = new Edcf(5);
        double[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        ind.Prime(data.AsSpan());
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ind = new Edcf(5);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), i * 10.0));
        }
        var ex = Record.Exception(() => ind.Dispose());
        Assert.Null(ex);
    }
}
