namespace QuanTAlib.Tests;

public class PolyfitTests
{
    // ── A) Constructor validation ─────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultParams_SetsName()
    {
        var p = new Polyfit(20);
        Assert.Equal("Polyfit(20,2)", p.Name);
        Assert.Equal(20, p.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ExplicitDegree_SetsName()
    {
        var p = new Polyfit(10, 3);
        Assert.Equal("Polyfit(10,3)", p.Name);
    }

    [Fact]
    public void Constructor_PeriodLessThan2_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Polyfit(1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Polyfit(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_DegreeZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Polyfit(10, 0));
        Assert.Equal("degree", ex.ParamName);
    }

    [Fact]
    public void Constructor_DegreeClampedToPeriodMinus1()
    {
        // degree=10 with period=5 → clamped to 4
        var p = new Polyfit(5, 10);
        Assert.Equal("Polyfit(5,4)", p.Name);
    }

    [Fact]
    public void Constructor_ChainingSubscribes()
    {
        var src = new Sma(3);
        var p = new Polyfit(src, 5, 2);
        Assert.Equal("Polyfit(5,2)", p.Name);
    }

    // ── B) Basic calculation ──────────────────────────────────────────────────

    [Fact]
    public void BasicCalc_ReturnsFiniteAfterWarmup()
    {
        var p = new Polyfit(5, 2);
        var gbm = new GBM(100, 0.05, 0.2, seed: 1);
        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next();
            p.Update(new TValue(bar.Time, bar.Close));
        }
        Assert.True(p.IsHot);
        Assert.True(double.IsFinite(p.Last.Value));
    }

    [Fact]
    public void BasicCalc_LinearInput_Degree1_MatchesLinearTrend()
    {
        // For perfectly linear data y=i with period=5, degree=1,
        // the linear fit should reproduce the last value y=4 (value at i=4).
        var p = new Polyfit(5, 1);
        for (int i = 0; i < 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), (double)i));
        }
        // Linear regression: slope=1, passes through points 0..4
        // P(1.0 normalized) = y at x=1.0 = 4.0
        Assert.Equal(4.0, p.Last.Value, 1e-9);
    }

    [Fact]
    public void BasicCalc_ConstantInput_ReturnsConstant()
    {
        var p = new Polyfit(5, 2);
        for (int i = 0; i < 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 42.0));
        }
        Assert.Equal(42.0, p.Last.Value, 1e-9);
    }

    [Fact]
    public void BasicCalc_NotHotBeforeWarmup()
    {
        var p = new Polyfit(5, 2);
        Assert.False(p.IsHot);
        p.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.False(p.IsHot);
    }

    // ── C) State + bar correction (isNew) ────────────────────────────────────

    [Fact]
    public void IsNewTrue_AdvancesBuffer()
    {
        var p = new Polyfit(5, 2);
        var gbm = new GBM(100, 0.05, 0.2, seed: 2);
        for (int i = 0; i < 5; i++)
        {
            var bar = gbm.Next();
            p.Update(new TValue(bar.Time, bar.Close));
        }
        double v1 = p.Last.Value;

        // Adding a new bar with extreme value changes the result
        p.Update(new TValue(DateTime.UtcNow.AddSeconds(5), 200.0));
        double v2 = p.Last.Value;
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IsNewFalse_CorrectsBars_RestoresExactly()
    {
        var p = new Polyfit(5, 2);
        double[] vals = [10.0, 20.0, 30.0, 40.0, 50.0];
        for (int i = 0; i < 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        double original = p.Last.Value;

        // Overwrite current bar with different value
        p.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 9999.0), isNew: false);
        Assert.NotEqual(original, p.Last.Value);

        // Restore — must exactly match original
        p.Update(new TValue(DateTime.UtcNow.AddSeconds(4), vals[4]), isNew: false);
        Assert.Equal(original, p.Last.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_FinalMatchesOriginal()
    {
        var p = new Polyfit(5, 2);
        double[] vals = [10, 20, 30, 40, 50];
        for (int i = 0; i < 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        double original = p.Last.Value;

        for (int iter = 0; iter < 5; iter++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 999.0), isNew: false);
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 50.0), isNew: false);
        }
        Assert.Equal(original, p.Last.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var p = new Polyfit(5, 2);
        for (int i = 0; i < 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), (double)(i + 1) * 10));
        }
        Assert.True(p.IsHot);

        p.Reset();
        Assert.False(p.IsHot);
        Assert.Equal(default, p.Last);
    }

    // ── D) Warmup / convergence ───────────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var p = new Polyfit(4, 2);
        for (int i = 0; i < 3; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 10.0));
            Assert.False(p.IsHot);
        }
        p.Update(new TValue(DateTime.UtcNow.AddSeconds(3), 10.0));
        Assert.True(p.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesConstructorPeriod()
    {
        var p = new Polyfit(12, 3);
        Assert.Equal(12, p.WarmupPeriod);
    }

    // ── E) Robustness: NaN / Infinity ─────────────────────────────────────────

    [Fact]
    public void NaN_SubstitutesLastValid()
    {
        var p = new Polyfit(5, 2);
        for (int i = 0; i < 4; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 10.0 + i));
        }
        p.Update(new TValue(DateTime.UtcNow.AddSeconds(4), double.NaN));
        Assert.True(double.IsFinite(p.Last.Value));
    }

    [Fact]
    public void Infinity_SubstitutesLastValid()
    {
        var p = new Polyfit(5, 2);
        for (int i = 0; i < 4; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 10.0));
        }
        p.Update(new TValue(DateTime.UtcNow.AddSeconds(4), double.PositiveInfinity));
        Assert.True(double.IsFinite(p.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] src = [10, 20, double.NaN, 30, 40, double.NaN, 50];
        double[] dst = new double[src.Length];
        Polyfit.Batch(src, dst, period: 5, degree: 2);
        // All outputs should be finite (NaN substituted by last valid)
        for (int i = 0; i < src.Length; i++)
        {
            Assert.True(double.IsFinite(dst[i]) || dst[i] == 0);
        }
    }

    // ── F) Consistency: batch == streaming == span == eventing ───────────────

    [Fact]
    public void AllModes_Consistent()
    {
        int period = 7;
        int degree = 2;
        int dataLen = 40;
        var gbm = new GBM(100, 0.05, 0.2, seed: 99);
        var series = new TSeries();
        for (int i = 0; i < dataLen; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        // 1. Batch (TSeries)
        var batchResult = Polyfit.Batch(series, period, degree);

        // 2. Streaming (separate GBM reset to same seed)
        var streaming = new Polyfit(period, degree);
        for (int i = 0; i < dataLen; i++)
        {
            streaming.Update(series[i]);
        }

        // 3. Span
        double[] spanOut = new double[dataLen];
        Polyfit.Batch(series.Values, spanOut.AsSpan(), period, degree);

        // Compare batch vs span for all hot values
        for (int i = period - 1; i < dataLen; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOut[i], 1e-9);
        }

        // Final value: streaming == batch
        Assert.Equal(batchResult[dataLen - 1].Value, streaming.Last.Value, 1e-9);
    }

    // ── G) Span API ───────────────────────────────────────────────────────────

    [Fact]
    public void SpanAPI_WrongLength_Throws()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] dst = new double[4];
        var ex = Assert.Throws<ArgumentException>(() =>
            Polyfit.Batch(src.AsSpan(), dst.AsSpan(), period: 3, degree: 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanAPI_PeriodLessThan2_Throws()
    {
        double[] src = [1, 2, 3];
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Polyfit.Batch(src.AsSpan(), dst.AsSpan(), period: 1, degree: 2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanAPI_DegreeLessThan1_Throws()
    {
        double[] src = [1, 2, 3];
        double[] dst = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Polyfit.Batch(src.AsSpan(), dst.AsSpan(), period: 3, degree: 0));
        Assert.Equal("degree", ex.ParamName);
    }

    [Fact]
    public void SpanAPI_LargeData_NoStackOverflow()
    {
        int n = 2000;
        double[] src = new double[n];
        var gbm = new GBM(100, 0.05, 0.2, seed: 7);
        for (int i = 0; i < n; i++)
        {
            src[i] = gbm.Next().Close;
        }
        double[] dst = new double[n];
        // period=300 > StackallocThreshold(256) → uses ArrayPool path
        Polyfit.Batch(src.AsSpan(), dst.AsSpan(), period: 300, degree: 2);
        Assert.True(double.IsFinite(dst[n - 1]));
    }

    [Fact]
    public void SpanAPI_MatchesTSeries()
    {
        int period = 6;
        int degree = 2;
        var gbm = new GBM(100, 0.05, 0.2, seed: 55);
        var series = new TSeries();
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var batchResult = Polyfit.Batch(series, period, degree);
        double[] spanOut = new double[30];
        Polyfit.Batch(series.Values, spanOut.AsSpan(), period, degree);

        for (int i = period - 1; i < 30; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOut[i], 1e-9);
        }
    }

    // ── H) Chainability ───────────────────────────────────────────────────────

    [Fact]
    public void EventFires_OnUpdate()
    {
        var p = new Polyfit(3, 1);
        int eventCount = 0;
        p.Pub += (_, in args) => eventCount++;
        for (int i = 0; i < 3; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 10.0));
        }
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void Chaining_WorksCorrectly()
    {
        var sma = new Sma(3);
        var poly = new Polyfit(sma, 5, 2);
        Assert.False(poly.IsHot);
        for (int i = 0; i < 7; i++)
        {
            sma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 10.0 + i));
        }
        Assert.True(poly.IsHot);
    }

    // ── I) Degree=1 matches LSMA / linear regression ─────────────────────────

    [Fact]
    public void Degree1_MatchesLinearRegression()
    {
        int period = 5;
        var poly = new Polyfit(period, 1);
        var lsma = new Lsma(period);

        var gbm = new GBM(100, 0.05, 0.2, seed: 42);
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next();
            var tv = new TValue(bar.Time, bar.Close);
            poly.Update(tv);
            lsma.Update(tv);
        }
        // Degree=1 polynomial fit == linear regression endpoint
        Assert.Equal(lsma.Last.Value, poly.Last.Value, 1e-6);
    }

    // ── J) Quadratic captures curvature ──────────────────────────────────────

    [Fact]
    public void Degree2_QuadraticData_MatchesExact()
    {
        // Data: y_i = (i/(n-1))^2 for i=0..n-1, n=5
        // Quadratic fit should be exact → P(1.0) = 1.0^2 = 1.0
        var p = new Polyfit(5, 2);
        for (int i = 0; i < 5; i++)
        {
            double xi = i / 4.0;
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), xi * xi));
        }
        Assert.Equal(1.0, p.Last.Value, 1e-9);
    }

    // ── K) Prime() – stateful priming ─────────────────────────────────────────

    [Fact]
    public void Prime_SetsState()
    {
        var p = new Polyfit(5, 2);
        double[] primeData = [10.0, 20.0, 30.0, 40.0, 50.0];
        p.Prime(primeData);
        Assert.True(p.IsHot);
        Assert.True(double.IsFinite(p.Last.Value));
    }

    // ── L) Calculate static method ────────────────────────────────────────────

    [Fact]
    public void Calculate_StaticMethod_ReturnsBoth()
    {
        var gbm = new GBM(100, 0.05, 0.2, seed: 7);
        var series = new TSeries();
        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }
        var (results, indicator) = Polyfit.Calculate(series, period: 10, degree: 2);
        Assert.NotNull(results);
        Assert.NotNull(indicator);
        Assert.Equal(25, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── M) Various degrees ────────────────────────────────────────────────────

    [Fact]
    public void Degree3_Cubic_ReturnsFinite()
    {
        var p = new Polyfit(10, 3);
        var gbm = new GBM(100, 0.05, 0.2, seed: 101);
        for (int i = 0; i < 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), gbm.Next().Close));
        }
        Assert.True(p.IsHot);
        Assert.True(double.IsFinite(p.Last.Value));
    }

    [Fact]
    public void Degree6_MaxDegree_ReturnsFinite()
    {
        var p = new Polyfit(10, 6);
        var gbm = new GBM(100, 0.05, 0.2, seed: 202);
        for (int i = 0; i < 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow.AddSeconds(i), gbm.Next().Close));
        }
        Assert.True(p.IsHot);
        Assert.True(double.IsFinite(p.Last.Value));
    }

    // ── N) Update(TSeries) round-trip ────────────────────────────────────────

    [Fact]
    public void UpdateTSeries_MatchesBatch()
    {
        int period = 8;
        int degree = 2;
        var gbm = new GBM(100, 0.05, 0.2, seed: 77);
        var series = new TSeries();
        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var p = new Polyfit(period, degree);
        var result = p.Update(series);
        var batchResult = Polyfit.Batch(series, period, degree);

        for (int i = 0; i < 30; i++)
        {
            Assert.Equal(batchResult[i].Value, result[i].Value, 1e-9);
        }
    }
}
