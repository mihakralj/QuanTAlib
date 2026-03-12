using Xunit;
using QuanTAlib;

namespace Filters;

public class Cheby1Tests
{
    private readonly GBM _gbm;

    public Cheby1Tests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 1234);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidatesParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby1(period: 1, ripple: 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby1(period: 10, ripple: 0.0));
        var filter = new Cheby1(period: 10, ripple: 1.0);
        Assert.NotNull(filter);
    }

    [Fact]
    public void Constructor_NegativeRipple_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby1(10, -1.0));
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var filter = new Cheby1(20, 2.0);
        Assert.Equal(20, filter.Period);
        Assert.Equal(2.0, filter.Ripple);
        Assert.Equal("Cheby1(20,2.0)", filter.Name);
        Assert.Equal(20, filter.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultRipple()
    {
        var filter = new Cheby1(10);
        Assert.Equal(1.0, filter.Ripple);
    }

    [Fact]
    public void Constructor_WithPublisher_Subscribes()
    {
        var source = new TSeries();
        var filter = new Cheby1(source, 10, 1.0);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(filter.Last.Value));
    }

    // ── IsHot ───────────────────────────────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrueWhenReady()
    {
        var filter = new Cheby1(20, 1.0);
        Assert.False(filter.IsHot);

        // Feed some data - IsHot becomes true when Count >= 2
        for (int i = 0; i < 3; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        Assert.True(filter.IsHot);
    }

    // ── Update ──────────────────────────────────────────────────────────

    [Fact]
    public void Update_ConstantInput_ConvergesToConstant()
    {
        var filter = new Cheby1(10, 1.0);
        for (int i = 0; i < 50; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, filter.Last.Value, 1e-4);
    }

    [Fact]
    public void Update_SmoothsNoisySignal()
    {
        var filter = new Cheby1(20, 1.0);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            filter.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(filter.Last.Value));
    }

    // ── NaN handling ────────────────────────────────────────────────────

    [Fact]
    public void Update_HandlesNaNSafely()
    {
        var filter = new Cheby1(10, 1.0);

        // Initial good value
        filter.Update(new TValue(DateTime.UtcNow, 100.0));

        // Bad value
        var result = filter.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Should use last valid value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_HandlesInfinitySafely()
    {
        var filter = new Cheby1(10, 1.0);
        filter.Update(new TValue(DateTime.UtcNow, 100.0));

        var result = filter.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── Bar Correction ──────────────────────────────────────────────────

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var filter = new Cheby1(10, 1.0);
        var time = DateTime.UtcNow;

        // Add some history
        for (int i = 0; i < 5; i++)
        {
            filter.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        double valBefore = filter.Last.Value;

        // 1. New update
        filter.Update(new TValue(time.AddSeconds(5), 200.0), isNew: true);
        double valAfterNew = filter.Last.Value;

        // 2. Correction (isNew=false) with different value
        filter.Update(new TValue(time.AddSeconds(5), 150.0), isNew: false);
        double valAfterCorrection = filter.Last.Value;

        Assert.NotEqual(valBefore, valAfterNew);
        Assert.NotEqual(valAfterNew, valAfterCorrection);

        // 3. Correction back to original value (isNew=false)
        filter.Update(new TValue(time.AddSeconds(5), 200.0), isNew: false);
        double valRestored = filter.Last.Value;

        Assert.Equal(valAfterNew, valRestored, 1e-10);
    }

    // ── Reset ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var filter = new Cheby1(10, 1.0);
        for (int i = 0; i < 10; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(filter.IsHot);

        filter.Reset();
        Assert.False(filter.IsHot);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var filter = new Cheby1(10, 1.0);
        for (int i = 0; i < 20; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        var firstResult = filter.Last.Value;

        filter.Reset();
        for (int i = 0; i < 20; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.Equal(firstResult, filter.Last.Value, 1e-10);
    }

    // ── Batch ───────────────────────────────────────────────────────────

    [Fact]
    public void SpanBatch_MatchesIterative()
    {
        const int period = 10;
        int count = 100;
        var data = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var values = data.Close.Values.ToArray();

        // Iterative
        var filter = new Cheby1(period, 1.0);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            iterativeResults[i] = filter.Update(new TValue(DateTime.UtcNow, values[i])).Value;
        }

        // Span
        var spanResults = new double[count];
        Cheby1.Batch(values, spanResults, period, 1.0);

        // Compare
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void Batch_TSeries_Static()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var result = Cheby1.Batch(data.Close, 10, 1.0);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var filter = new Cheby1(10, 1.0);
        var result = filter.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Update_TSeries_ReturnsCorrectCount()
    {
        var filter = new Cheby1(10, 1.0);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var result = filter.Update(data.Close);
        Assert.Equal(50, result.Count);
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Cheby1.Calculate(data.Close, 10, 1.0);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var filter = new Cheby1(10, 1.0);
        var values = new double[20];
        for (int i = 0; i < 20; i++)
        {
            values[i] = 100 + i;
        }

        filter.Prime(values);
        Assert.True(filter.IsHot);
    }

    // ── Dispose ─────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_WithPublisher_Unsubscribes()
    {
        var source = new TSeries();
        var filter = new Cheby1(source, 10, 1.0);
        source.Add(new TValue(DateTime.UtcNow, 100));

        filter.Dispose();
        var lastBefore = filter.Last;
        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(lastBefore, filter.Last);
    }

    [Fact]
    public void Dispose_WithoutPublisher_DoesNotThrow()
    {
        var filter = new Cheby1(10, 1.0);
        filter.Update(new TValue(DateTime.UtcNow, 100));
        filter.Dispose();
        Assert.True(true); // S2699: explicit assertion for dispose-only test
    }

    // ── Determinism ─────────────────────────────────────────────────────

    [Fact]
    public void TwoInstances_SameInput_SameOutput()
    {
        var f1 = new Cheby1(10, 1.0);
        var f2 = new Cheby1(10, 1.0);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            var r1 = f1.Update(tv);
            var r2 = f2.Update(tv);
            Assert.Equal(r1.Value, r2.Value);
        }
    }

    // ── Filter behavior ─────────────────────────────────────────────────

    [Fact]
    public void DifferentRipple_ProduceDifferentOutputs()
    {
        var lowRipple = new Cheby1(10, 0.5);
        var highRipple = new Cheby1(10, 3.0);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double lastLow = 0, lastHigh = 0;
        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            lastLow = lowRipple.Update(tv).Value;
            lastHigh = highRipple.Update(tv).Value;
        }

        // Different ripple parameters should generally produce different outputs
        Assert.NotEqual(lastLow, lastHigh, 1e-6);
    }
}
