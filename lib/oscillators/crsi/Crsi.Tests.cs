using Xunit;

namespace QuanTAlib.Tests;

public sealed class CrsiTests
{
    private const double Tolerance = 1e-10;

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_RsiPeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Crsi(rsiPeriod: 0));
        Assert.Equal("rsiPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_RsiPeriodNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Crsi(rsiPeriod: -1));
        Assert.Equal("rsiPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_StreakPeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Crsi(streakPeriod: 0));
        Assert.Equal("streakPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_StreakPeriodNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Crsi(streakPeriod: -5));
        Assert.Equal("streakPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_RankPeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Crsi(rankPeriod: 0));
        Assert.Equal("rankPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_RankPeriodNegative_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Crsi(rankPeriod: -10));
        Assert.Equal("rankPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidDefaults_SetsProperties()
    {
        var crsi = new Crsi();
        Assert.Equal(3, crsi.RsiPeriod);
        Assert.Equal(2, crsi.StreakPeriod);
        Assert.Equal(100, crsi.RankPeriod);
        Assert.Equal("Crsi(3,2,100)", crsi.Name);
        Assert.False(crsi.IsHot);
    }

    [Fact]
    public void Constructor_CustomPeriods_SetsProperties()
    {
        var crsi = new Crsi(rsiPeriod: 5, streakPeriod: 3, rankPeriod: 50);
        Assert.Equal(5, crsi.RsiPeriod);
        Assert.Equal(3, crsi.StreakPeriod);
        Assert.Equal(50, crsi.RankPeriod);
        Assert.Equal("Crsi(5,3,50)", crsi.Name);
    }

    [Fact]
    public void BatchSpan_RsiPeriodZero_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Crsi.Batch(src, out1, rsiPeriod: 0));
        Assert.Equal("rsiPeriod", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_StreakPeriodZero_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Crsi.Batch(src, out1, streakPeriod: 0));
        Assert.Equal("streakPeriod", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_RankPeriodZero_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Crsi.Batch(src, out1, rankPeriod: 0));
        Assert.Equal("rankPeriod", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_MismatchedLength_ThrowsArgumentException()
    {
        var src = new double[] { 1, 2, 3 };
        var out1 = new double[4];
        var ex = Assert.Throws<ArgumentException>(() => Crsi.Batch(src, out1));
        Assert.Equal("output", ex.ParamName);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        var result = crsi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_OutputInRange0To100()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3, seed: 99);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars.Close)
        {
            var v = crsi.Update(bar).Value;
            Assert.True(v >= 0.0 && v <= 100.0, $"CRSI={v} out of [0,100]");
        }
    }

    [Fact]
    public void Update_NameAccessible()
    {
        var crsi = new Crsi(3, 2, 100);
        crsi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal("Crsi(3,2,100)", crsi.Name);
    }

    [Fact]
    public void Update_IsHotFalseBeforeWarmup()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        for (int i = 0; i < 4; i++)
        {
            crsi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(crsi.IsHot);
        }
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        var t = DateTime.UtcNow;
        crsi.Update(new TValue(t, 100.0), isNew: true);
        var v1 = crsi.Last;
        crsi.Update(new TValue(t.AddMinutes(1), 105.0), isNew: true);
        var v2 = crsi.Last;

        // Two distinct bars — Last values can differ
        Assert.NotEqual(default, v1);
        Assert.NotEqual(default, v2);
    }

    [Fact]
    public void Update_IsNew_False_RollsBack()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        double[] prices = [100, 102, 104, 103, 105, 107];
        var t = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            crsi.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
        }

        // Correction — produce different value
        crsi.Update(new TValue(t.AddMinutes(prices.Length), 150.0), isNew: false);
        var corrected1 = crsi.Last.Value;

        // Same correction again must produce same result (idempotent)
        crsi.Update(new TValue(t.AddMinutes(prices.Length), 150.0), isNew: false);
        var corrected2 = crsi.Last.Value;

        Assert.Equal(corrected1, corrected2, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        double[] prices = [100, 102, 98, 105, 103, 107];
        var t = DateTime.UtcNow;
        for (int i = 0; i < prices.Length; i++)
        {
            crsi.Update(new TValue(t.AddMinutes(i), prices[i]), isNew: true);
        }

        double baseline = crsi.Last.Value;

        // Two bad corrections, then restore original
        crsi.Update(new TValue(t.AddMinutes(prices.Length), 999.0), isNew: false);
        crsi.Update(new TValue(t.AddMinutes(prices.Length), 888.0), isNew: false);
        crsi.Update(new TValue(t.AddMinutes(prices.Length), prices[^1]), isNew: false);

        Assert.Equal(baseline, crsi.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        var t = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            crsi.Update(new TValue(t.AddMinutes(i), 100.0 + i));
        }

        Assert.True(crsi.IsHot);
        crsi.Reset();
        Assert.False(crsi.IsHot);
        Assert.Equal(default, crsi.Last);
    }

    // ───── D) Warmup / convergence ─────

    [Fact]
    public void IsHot_FlipsAfterRankPeriodBars()
    {
        int rankPeriod = 5;
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: rankPeriod);
        var t = DateTime.UtcNow;

        // rankPeriod-1 bars: still cold
        for (int i = 0; i < rankPeriod - 1; i++)
        {
            crsi.Update(new TValue(t.AddMinutes(i), 100.0 + i));
            Assert.False(crsi.IsHot);
        }

        // rankPeriod bar: hot
        crsi.Update(new TValue(t.AddMinutes(rankPeriod - 1), 100.0 + rankPeriod - 1));
        Assert.True(crsi.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsAccessible()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 100);
        Assert.True(crsi.WarmupPeriod > 0);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        var t = DateTime.UtcNow;
        for (int i = 0; i < 8; i++)
        {
            crsi.Update(new TValue(t.AddMinutes(i), 100.0 + i));
        }

        crsi.Update(new TValue(t.AddMinutes(8), double.NaN));
        Assert.True(double.IsFinite(crsi.Last.Value));
        Assert.True(crsi.Last.Value >= 0.0 && crsi.Last.Value <= 100.0);
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        var t = DateTime.UtcNow;
        for (int i = 0; i < 8; i++)
        {
            crsi.Update(new TValue(t.AddMinutes(i), 100.0 + i));
        }

        crsi.Update(new TValue(t.AddMinutes(8), double.PositiveInfinity));
        Assert.True(double.IsFinite(crsi.Last.Value));

        crsi.Update(new TValue(t.AddMinutes(9), double.NegativeInfinity));
        Assert.True(double.IsFinite(crsi.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var crsi = new Crsi(rsiPeriod: 3, streakPeriod: 2, rankPeriod: 5);
        var t = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            crsi.Update(new TValue(t.AddMinutes(i), double.NaN));
        }

        Assert.True(double.IsFinite(crsi.Last.Value));
    }

    // ───── F) Consistency (4 modes match) ─────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int rsiPeriod = 3;
        int streakPeriod = 2;
        int rankPeriod = 20;
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 77);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // 1. Streaming
        var streaming = new Crsi(rsiPeriod, streakPeriod, rankPeriod);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // 2. Batch TSeries
        TSeries batchSeries = Crsi.Batch(source, rsiPeriod, streakPeriod, rankPeriod);

        // 3. Batch Span
        var spanOutput = new double[source.Count];
        Crsi.Batch(source.Values, spanOutput, rsiPeriod, streakPeriod, rankPeriod);

        // 4. Event-based
        var eventSource = new TSeries();
        var eventIndicator = new Crsi(eventSource, rsiPeriod, streakPeriod, rankPeriod);
        var eventResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], Tolerance);
            Assert.Equal(streamResults[i], spanOutput[i], Tolerance);
            Assert.Equal(streamResults[i], eventResults[i], Tolerance);
        }
    }

    // ───── G) Span API tests ─────

    [Fact]
    public void BatchSpan_EmptySource_DoesNotThrow()
    {
        var src = Array.Empty<double>();
        var out1 = Array.Empty<double>();
        // Should not throw and output remains empty
        Crsi.Batch(src, out1);
        Assert.Empty(out1);
    }

    [Fact]
    public void BatchSpan_OutputInRange0to100()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 55);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var src = bars.Close.Values;
        var out1 = new double[src.Length];

        Crsi.Batch(src, out1, rsiPeriod: 3, streakPeriod: 2, rankPeriod: 20);

        for (int i = 0; i < out1.Length; i++)
        {
            Assert.True(out1[i] >= 0.0 && out1[i] <= 100.0, $"Span output[{i}]={out1[i]} out of range");
        }
    }

    [Fact]
    public void BatchSpan_LargeData_NoStackOverflow()
    {
        int n = 10_000;
        var src = new double[n];
        var out1 = new double[n];
        for (int i = 0; i < n; i++)
        {
            src[i] = 100.0 + i * 0.01;
        }

        // rankPeriod > 256 to exercise ArrayPool path
        Crsi.Batch(src, out1, rsiPeriod: 3, streakPeriod: 2, rankPeriod: 500);

        for (int i = 0; i < n; i++)
        {
            Assert.True(out1[i] >= 0.0 && out1[i] <= 100.0);
        }
    }

    // ───── H) Chainability ─────

    [Fact]
    public void EventChaining_PubFires()
    {
        int rsiPeriod = 3;
        int streakPeriod = 2;
        int rankPeriod = 5;
        var sourceTs = new TSeries();
        var crsi = new Crsi(sourceTs, rsiPeriod, streakPeriod, rankPeriod);

        int count = 0;
        crsi.Pub += (_, in _) => count++;

        var t = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            sourceTs.Add(new TValue(t.AddMinutes(i), 100.0 + i));
        }

        Assert.Equal(10, count);
    }
}
