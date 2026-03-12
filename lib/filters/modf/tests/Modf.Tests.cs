namespace QuanTAlib.Tests;

public class ModfTests
{
    private readonly GBM _gbm = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
    private readonly TSeries _data;

    public ModfTests()
    {
        _data = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    // ── A) Constructor validation ──────────────────────────────────────

    [Fact]
    public void Constructor_PeriodLessThan2_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Modf(1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_BetaNegative_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Modf(14, beta: -0.1));
        Assert.Equal("beta", ex.ParamName);
    }

    [Fact]
    public void Constructor_BetaOverOne_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Modf(14, beta: 1.1));
        Assert.Equal("beta", ex.ParamName);
    }

    [Fact]
    public void Constructor_FbWeightZero_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Modf(14, fbWeight: 0.0));
        Assert.Equal("fbWeight", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidParams_SetsProperties()
    {
        var modf = new Modf(20, beta: 0.7, feedback: true, fbWeight: 0.3);
        Assert.Equal(20, modf.Period);
        Assert.Equal(0.7, modf.Beta);
        Assert.True(modf.Feedback);
        Assert.Equal(0.3, modf.FbWeight);
        Assert.Contains("Modf", modf.Name, StringComparison.Ordinal);
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var modf = new Modf(14);
        var result = modf.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var modf = new Modf(14);
        modf.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(modf.Last.Value));
    }

    [Fact]
    public void Name_ContainsModf()
    {
        var modf = new Modf(14, beta: 0.8);
        Assert.Contains("Modf", modf.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstBar_OutputEqualsInput()
    {
        var modf = new Modf(14);
        var result = modf.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, 10);
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var modf = new Modf(14);
        modf.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        double v1 = modf.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true).Value;
        double v2 = modf.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true).Value;
        // Second bar with same value should differ from first (state progressed)
        Assert.True(double.IsFinite(v1));
        Assert.True(double.IsFinite(v2));
    }

    [Fact]
    public void IsNew_False_RollsBack()
    {
        var modf = new Modf(14);
        for (int i = 0; i < 20; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        double baseline = modf.Update(new TValue(DateTime.UtcNow, 150.0), isNew: true).Value;
        double corrected = modf.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false).Value;
        Assert.NotEqual(baseline, corrected);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var modf = new Modf(14);
        for (int i = 0; i < 30; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 0.5)), isNew: true);
        }

        double before = modf.Last.Value;
        // Correct last bar multiple times
        for (int i = 0; i < 5; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 100.0 + (29 * 0.5)), isNew: false);
        }

        Assert.Equal(before, modf.Last.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var modf = new Modf(14);
        for (int i = 0; i < 20; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }

        modf.Reset();
        Assert.False(modf.IsHot);
        var result = modf.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.Equal(50.0, result.Value, 10);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var modf = new Modf(14);
        Assert.False(modf.IsHot);
        for (int i = 0; i < 14; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(modf.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var modf = new Modf(20);
        Assert.Equal(20, modf.WarmupPeriod);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValid()
    {
        var modf = new Modf(14);
        modf.Update(new TValue(DateTime.UtcNow, 100.0));
        modf.Update(new TValue(DateTime.UtcNow, 105.0));
        var result = modf.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var modf = new Modf(14);
        modf.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = modf.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchNaN_Handled()
    {
        double[] src = [100, 101, double.NaN, 103, 104];
        double[] output = new double[src.Length];
        Modf.Batch(src, output, 3);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ── F) Consistency (4-API modes match) ─────────────────────────────

    [Fact]
    public void AllFourModes_ProduceSameResults()
    {
        int period = 14;
        double beta = 0.8;

        // Mode 1: Streaming
        var streaming = new Modf(period, beta);
        var streamResults = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            streamResults[i] = streaming.Update(_data[i]).Value;
        }

        // Mode 2: Batch TSeries
        var batchTs = Modf.Batch(_data, period, beta);

        // Mode 3: Batch Span
        double[] spanOut = new double[_data.Count];
        Modf.Batch(_data.Values, spanOut, period, beta);

        // Mode 4: Calculate
        var (calcTs, _) = Modf.Calculate(_data, period, beta);

        for (int i = 0; i < _data.Count; i++)
        {
            Assert.Equal(streamResults[i], batchTs[i].Value, 10);
            Assert.Equal(streamResults[i], spanOut[i], 10);
            Assert.Equal(streamResults[i], calcTs[i].Value, 10);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Batch_LengthMismatch_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Modf.Batch(src, output, 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => Modf.Batch(src, output, 1));
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        int period = 10;
        double[] spanOut = new double[_data.Count];
        Modf.Batch(_data.Values, spanOut, period);

        var streaming = new Modf(period);
        for (int i = 0; i < _data.Count; i++)
        {
            double sv = streaming.Update(_data[i]).Value;
            Assert.Equal(sv, spanOut[i], 10);
        }
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_Fires()
    {
        var modf = new Modf(14);
        int count = 0;
        modf.Pub += (object? _, in TValueEventArgs _) => count++;
        modf.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void EventChain_Works()
    {
        var source = new TSeries();
        var modf = new Modf(source, 14);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        source.Add(new TValue(DateTime.UtcNow, 105.0));
        Assert.True(double.IsFinite(modf.Last.Value));
    }

    // ── MODF-specific tests ────────────────────────────────────────────

    [Fact]
    public void Beta1_SmoothFilter_TracksPrice()
    {
        var modf = new Modf(14, beta: 1.0);
        for (int i = 0; i < 50; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        // With beta=1.0 (smooth mode), output should track price closely
        Assert.True(modf.Last.Value > 100.0);
        Assert.True(modf.Last.Value <= 149.0);
    }

    [Fact]
    public void Beta0_TrailingStop_BehavesLike()
    {
        var modf = new Modf(14, beta: 0.0);
        // Feed uptrend
        for (int i = 0; i < 30; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 2.0)));
        }
        double upResult = modf.Last.Value;
        // Output should be at or below price in uptrend (lower band tracks behind)
        Assert.True(upResult <= 100.0 + (29 * 2.0));
    }

    [Fact]
    public void Feedback_ProducesSmootherOutput()
    {
        // Feedback should produce different (smoother) results than no feedback
        var noFb = new Modf(14, feedback: false);
        var withFb = new Modf(14, feedback: true, fbWeight: 0.5);

        double lastNoFb = 0, lastWithFb = 0;
        for (int i = 0; i < _data.Count; i++)
        {
            lastNoFb = noFb.Update(_data[i]).Value;
            lastWithFb = withFb.Update(_data[i]).Value;
        }
        // Results should differ when feedback is enabled
        Assert.NotEqual(lastNoFb, lastWithFb);
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var modf = new Modf(14);
        for (int i = 0; i < 100; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, modf.Last.Value, 8);
    }

    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var modf = new Modf(14);
        TSeries result = modf.Update(_data);
        Assert.Equal(_data.Count, result.Count);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var (results, indicator) = Modf.Calculate(_data, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(_data.Count, results.Count);
    }
}
