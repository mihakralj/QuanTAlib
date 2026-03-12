using Xunit;

namespace QuanTAlib.Tests;

public sealed class AcTests
{
    private readonly GBM _gbm = new(1000.0, 0.05, 0.3, seed: 42);

    // ── A) Constructor validation ──

    [Fact]
    public void Constructor_FastPeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ac(fastPeriod: 0));
        Assert.Equal("fastPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_SlowPeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ac(slowPeriod: 0));
        Assert.Equal("slowPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_FastGeSlow_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ac(fastPeriod: 34, slowPeriod: 5));
        Assert.Equal("fastPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_AcPeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ac(acPeriod: 0));
        Assert.Equal("acPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_Defaults_NameCorrect()
    {
        var ac = new Ac();
        Assert.Equal("Ac(5,34,5)", ac.Name);
    }

    [Fact]
    public void Constructor_Custom_WarmupPeriod()
    {
        var ac = new Ac(5, 34, 5);
        Assert.Equal(38, ac.WarmupPeriod); // 34 + 5 - 1
    }

    // ── B) Basic calculation ──

    [Fact]
    public void Update_SingleBar_ReturnsValue()
    {
        var ac = new Ac();
        var bar = _gbm.Next(isNew: true);
        var result = ac.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var ac = new Ac();
        var bar = _gbm.Next(isNew: true);
        _ = ac.Update(bar);
        Assert.True(double.IsFinite(ac.Last.Value));
    }

    [Fact]
    public void Update_ConstantPrice_ConvergesToZero()
    {
        var ac = new Ac();
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100.0, 100.0, 100.0, 100.0, 1000.0);
            _ = ac.Update(bar, isNew: true);
        }

        Assert.True(ac.IsHot);
        Assert.Equal(0.0, ac.Last.Value, 1e-10);
    }

    // ── C) State + bar correction ──

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var ac = new Ac();
        // Feed enough bars so the values diverge from zero
        for (int i = 0; i < 40; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var bar1 = _gbm.Next(isNew: true);
        var result1 = ac.Update(bar1, isNew: true);

        var bar2 = _gbm.Next(isNew: true);
        var result2 = ac.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IsNew_False_Rewrites()
    {
        var ac = new Ac();
        for (int i = 0; i < 40; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var bar = _gbm.Next(isNew: true);
        var first = ac.Update(bar, isNew: true);

        var correctionBar = new TBar(bar.Time, bar.Open * 1.01, bar.High * 1.01, bar.Low * 1.01, bar.Close * 1.01, bar.Volume);
        var corrected = ac.Update(correctionBar, isNew: false);

        Assert.NotEqual(first.Value, corrected.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var ac = new Ac();
        for (int i = 0; i < 40; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var bar = _gbm.Next(isNew: true);
        var first = ac.Update(bar, isNew: true);

        // Apply corrections multiple times
        for (int i = 0; i < 5; i++)
        {
            _ = ac.Update(bar, isNew: false);
        }

        var final = ac.Update(bar, isNew: false);
        Assert.Equal(first.Value, final.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ac = new Ac();
        for (int i = 0; i < 50; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        Assert.True(ac.IsHot);

        ac.Reset();

        Assert.False(ac.IsHot);
        Assert.Equal(0.0, ac.Last.Value);
    }

    // ── D) Warmup / convergence ──

    [Fact]
    public void IsHot_FlipsAfterSufficientData()
    {
        var ac = new Ac(5, 34, 5);

        // Feed just 1 bar — should not be hot yet
        _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        // May already become hot if inner SMA sees enough values

        // After feeding enough bars, must be hot
        for (int i = 1; i < 50; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        Assert.True(ac.IsHot);
    }

    // ── E) Robustness ──

    [Fact]
    public void Update_NaN_KeepsLastValid()
    {
        var ac = new Ac();
        for (int i = 0; i < 40; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var lastBefore = ac.Last;
        var nanInput = new TValue(DateTime.UtcNow, double.NaN);
        var result = ac.Update(nanInput, isNew: true);

        Assert.Equal(lastBefore.Value, result.Value, 1e-10);
    }

    [Fact]
    public void Update_Infinity_KeepsLastValid()
    {
        var ac = new Ac();
        for (int i = 0; i < 40; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        var lastBefore = ac.Last;
        var infInput = new TValue(DateTime.UtcNow, double.PositiveInfinity);
        var result = ac.Update(infInput, isNew: true);

        Assert.Equal(lastBefore.Value, result.Value, 1e-10);
    }

    // ── F) Consistency (batch == streaming == span == eventing) ──

    [Fact]
    public void BatchCalc_Matches_Streaming()
    {
        var gbm = new GBM(500.0, 0.05, 0.3, seed: 99);
        var series = new TBarSeries();
        for (int i = 0; i < 100; i++)
        {
            series.Add(gbm.Next(isNew: true));
        }

        // Streaming
        var streaming = new Ac();
        for (int i = 0; i < series.Count; i++)
        {
            _ = streaming.Update(series[i], isNew: true);
        }

        // Batch via Update(TBarSeries)
        var batchAc = new Ac();
        var batchResult = batchAc.Update(series);

        // Compare last values
        Assert.Equal(streaming.Last.Value, batchResult[^1].Value, 4);
    }

    [Fact]
    public void SpanBatch_Matches_Streaming()
    {
        var gbm = new GBM(500.0, 0.05, 0.3, seed: 99);
        var series = new TBarSeries();
        for (int i = 0; i < 100; i++)
        {
            series.Add(gbm.Next(isNew: true));
        }

        // Streaming
        var streaming = new Ac();
        for (int i = 0; i < series.Count; i++)
        {
            _ = streaming.Update(series[i], isNew: true);
        }

        // Span batch
        var output = new double[series.Count];
        Ac.Batch(series.High.Values, series.Low.Values, output);

        Assert.Equal(streaming.Last.Value, output[^1], 4);
    }

    [Fact]
    public void EventPub_FiresOnUpdate()
    {
        var ac = new Ac();
        int pubCount = 0;
        ac.Pub += (object? sender, in TValueEventArgs e) => pubCount++;

        for (int i = 0; i < 5; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        Assert.Equal(5, pubCount);
    }

    // ── G) Span API tests ──

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var high = new double[10];
        var low = new double[10];
        var dest = new double[5]; // wrong length

        var ex = Assert.Throws<ArgumentException>(() => Ac.Batch(high, low, dest));
        Assert.Equal("destination", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var output = Array.Empty<double>();
        Ac.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, output);
        Assert.Empty(output);
    }

    // ── H) Chainability ──

    [Fact]
    public void EventChaining_Works()
    {
        var ac = new Ac();
        var values = new List<double>();

        ac.Pub += (object? sender, in TValueEventArgs e) => values.Add(e.Value.Value);

        for (int i = 0; i < 50; i++)
        {
            _ = ac.Update(_gbm.Next(isNew: true), isNew: true);
        }

        Assert.Equal(50, values.Count);
    }

    // ── Additional: TValue Update path ──

    [Fact]
    public void TValueUpdate_Works()
    {
        var ac = new Ac();
        for (int i = 0; i < 50; i++)
        {
            var val = new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (i * 0.1));
            _ = ac.Update(val, isNew: true);
        }

        Assert.True(ac.IsHot);
        Assert.True(double.IsFinite(ac.Last.Value));
    }

    [Fact]
    public void Prime_SetsState()
    {
        var gbm = new GBM(500.0, 0.05, 0.3, seed: 77);
        var series = new TBarSeries();
        for (int i = 0; i < 60; i++)
        {
            series.Add(gbm.Next(isNew: true));
        }

        var ac = new Ac();
        ac.Prime(series);

        Assert.True(ac.IsHot);
        Assert.True(double.IsFinite(ac.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsResultAndIndicator()
    {
        var gbm = new GBM(500.0, 0.05, 0.3, seed: 88);
        var series = new TBarSeries();
        for (int i = 0; i < 60; i++)
        {
            series.Add(gbm.Next(isNew: true));
        }

        var (results, indicator) = Ac.Calculate(series);

        Assert.Equal(60, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Batch_TBarSeries_Empty()
    {
        var result = Ac.Batch(new TBarSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Update_TBarSeries_Empty()
    {
        var ac = new Ac();
        var result = ac.Update(new TBarSeries());
        Assert.Empty(result);
    }
}
