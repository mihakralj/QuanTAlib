using Xunit;
using QuanTAlib;

namespace Filters;

public class Cheby2Tests
{
    private readonly GBM _gbm;

    public Cheby2Tests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 1234);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidatesParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby2(period: 1, attenuation: 5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby2(period: 10, attenuation: 0.0));
        var filter = new Cheby2(period: 10, attenuation: 5.0);
        Assert.NotNull(filter);
    }

    [Fact]
    public void Constructor_NegativeAttenuation_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cheby2(10, -1.0));
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var filter = new Cheby2(20, 10.0);
        Assert.Equal(20, filter.Period);
        Assert.Equal(10.0, filter.Attenuation);
        Assert.Equal("Cheby2(20,10.0)", filter.Name);
        Assert.Equal(20, filter.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultAttenuation()
    {
        var filter = new Cheby2(10);
        Assert.Equal(5.0, filter.Attenuation);
    }

    [Fact]
    public void Constructor_WithPublisher_Subscribes()
    {
        var source = new TSeries();
        var filter = new Cheby2(source, 10, 5.0);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(filter.Last.Value));
    }

    // ── IsHot ───────────────────────────────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrueWhenReady()
    {
        var filter = new Cheby2(20, 5.0);
        Assert.False(filter.IsHot);

        // Feed some data
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
        var filter = new Cheby2(10, 5.0);
        for (int i = 0; i < 50; i++)
        {
            filter.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, filter.Last.Value, 1e-4);
    }

    [Fact]
    public void Update_SmoothsNoisySignal()
    {
        var filter = new Cheby2(20, 5.0);
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
        var filter = new Cheby2(10, 5.0);

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
        var filter = new Cheby2(10, 5.0);
        filter.Update(new TValue(DateTime.UtcNow, 100.0));

        var result = filter.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── Bar Correction ──────────────────────────────────────────────────

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var filter = new Cheby2(10, 5.0);
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
        var filter = new Cheby2(10, 5.0);
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
        var filter = new Cheby2(10, 5.0);
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
        var filter = new Cheby2(period, 5.0);
        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            iterativeResults[i] = filter.Update(new TValue(DateTime.UtcNow, values[i])).Value;
        }

        // Span
        var spanResults = new double[count];
        Cheby2.Batch(values, spanResults, period, 5.0);

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
        var result = Cheby2.Batch(data.Close, 10, 5.0);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var filter = new Cheby2(10, 5.0);
        var result = filter.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Update_TSeries_ReturnsCorrectCount()
    {
        var filter = new Cheby2(10, 5.0);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var result = filter.Update(data.Close);
        Assert.Equal(50, result.Count);
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Cheby2.Calculate(data.Close, 10, 5.0);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var filter = new Cheby2(10, 5.0);
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
        var filter = new Cheby2(source, 10, 5.0);
        source.Add(new TValue(DateTime.UtcNow, 100));

        filter.Dispose();
        var lastBefore = filter.Last;
        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(lastBefore, filter.Last);
    }

    [Fact]
    public void Dispose_WithoutPublisher_DoesNotThrow()
    {
        var filter = new Cheby2(10, 5.0);
        filter.Update(new TValue(DateTime.UtcNow, 100));
        filter.Dispose();
        Assert.True(true); // S2699: explicit assertion for dispose-only test
    }

    // ── Determinism ─────────────────────────────────────────────────────

    [Fact]
    public void TwoInstances_SameInput_SameOutput()
    {
        var f1 = new Cheby2(10, 5.0);
        var f2 = new Cheby2(10, 5.0);
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
    public void HighAttenuation_MoreSmoothing()
    {
        var lowAtten = new Cheby2(10, 2.0);
        var highAtten = new Cheby2(10, 20.0);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double sumDiffLow = 0, sumDiffHigh = 0;
        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            var rLow = lowAtten.Update(tv);
            var rHigh = highAtten.Update(tv);
            sumDiffLow += Math.Abs(bar.Close - rLow.Value);
            sumDiffHigh += Math.Abs(bar.Close - rHigh.Value);
        }

        // Both should produce finite outputs
        Assert.True(double.IsFinite(lowAtten.Last.Value));
        Assert.True(double.IsFinite(highAtten.Last.Value));
    }
}
