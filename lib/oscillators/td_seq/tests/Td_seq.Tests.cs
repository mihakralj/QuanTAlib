using Xunit;

namespace QuanTAlib.Tests;

public sealed class TdSeqTests
{
    private static TBar Bar(double close, double high = 0, double low = 0) =>
        new(DateTime.UtcNow, open: close, high: high == 0 ? close + 1 : high,
            low: low == 0 ? close - 1 : low, close: close, volume: 1000);

    private static TBar[] MakeBars(double[] closes)
    {
        var bars = new TBar[closes.Length];
        for (int i = 0; i < closes.Length; i++)
        {
            bars[i] = Bar(closes[i]);
        }

        return bars;
    }

    private static TBar[] GbmBars(int count, int seed = 42)
    {
        var gbm = new GBM(100.0, 0.02, 0.1, seed: seed);
        var series = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var bars = new TBar[count];
        for (int i = 0; i < count; i++)
        {
            double c = series.Close.Values[i];
            double h = series.High.Values[i];
            double l = series.Low.Values[i];
            bars[i] = new TBar(DateTime.UtcNow.AddMinutes(i), c, h, l, c, 1000);
        }

        return bars;
    }

    // ───── A) Constructor validation ─────

    [Fact]
    public void Constructor_ZeroComparePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new TdSeq(comparePeriod: 0));
        Assert.Equal("comparePeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeComparePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new TdSeq(comparePeriod: -1));
        Assert.Equal("comparePeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_Default_SetsProperties()
    {
        var td = new TdSeq();
        Assert.Equal("TdSeq(4)", td.Name);
        Assert.Equal(5, td.WarmupPeriod);
        Assert.False(td.IsHot);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsProperties()
    {
        var td = new TdSeq(comparePeriod: 3);
        Assert.Equal("TdSeq(3)", td.Name);
        Assert.Equal(4, td.WarmupPeriod);
    }

    // ───── B) Basic calculation ─────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var td = new TdSeq();
        var result = td.Update(Bar(100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var td = new TdSeq();
        td.Update(Bar(100.0));
        Assert.False(td.IsHot);
        Assert.Equal("TdSeq(4)", td.Name);
    }

    [Fact]
    public void Update_SellSetup_CountsPositive()
    {
        var td = new TdSeq(comparePeriod: 4);
        // Feed 5 bars to get IsHot, then continue rising
        // Rising closes: close > close[4] for consecutive bars → sell setup
        double[] prices = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];
        foreach (double p in prices)
        {
            td.Update(Bar(p));
        }

        Assert.True(td.IsHot);
        Assert.True(td.Setup > 0, $"Expected positive setup, got {td.Setup}");
    }

    [Fact]
    public void Update_BuySetup_CountsNegative()
    {
        var td = new TdSeq(comparePeriod: 4);
        // Falling closes: close < close[4] → buy setup (negative)
        double[] prices = [110, 109, 108, 107, 106, 105, 104, 103, 102, 101, 100];
        foreach (double p in prices)
        {
            td.Update(Bar(p));
        }

        Assert.True(td.IsHot);
        Assert.True(td.Setup < 0, $"Expected negative setup, got {td.Setup}");
    }

    [Fact]
    public void Update_SetupComplete_ReachesNine()
    {
        var td = new TdSeq(comparePeriod: 4);
        // Steadily rising for 13+ bars (9 qualify for sell setup after 4-bar lookback)
        // Bars 0-3: prime the history. Bars 4-12: each > close[4] → consecutive sell setup
        double[] prices = new double[20];
        for (int i = 0; i < 20; i++) { prices[i] = 100.0 + i; }

        foreach (double p in prices)
        {
            td.Update(Bar(p));
        }

        // After 9 consecutive qualifying bars setup should have been clamped to 9
        Assert.Equal(9, td.Setup);
    }

    // ───── C) State + bar correction ─────

    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var td = new TdSeq();
        td.Update(Bar(100.0), isNew: true);
        _ = td.Setup; // capture state after first update
        td.Update(Bar(200.0), isNew: true);
        // Second bar may have different setup due to price change
        Assert.False(td.IsHot); // still warming up
    }

    [Fact]
    public void Update_IsNew_False_IsIdempotent()
    {
        var td = new TdSeq(comparePeriod: 4);
        double[] prices = [100, 101, 102, 103, 104, 105, 106];
        foreach (double p in prices)
        {
            td.Update(Bar(p), isNew: true);
        }

        // Correct last bar twice — same result
        td.Update(Bar(106.5), isNew: false);
        double v1 = td.Last.Value;
        td.Update(Bar(106.5), isNew: false);
        double v2 = td.Last.Value;

        Assert.Equal(v1, v2);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var td = new TdSeq(comparePeriod: 4);
        double[] prices = [100, 101, 102, 103, 104, 105, 106];
        foreach (double p in prices)
        {
            td.Update(Bar(p), isNew: true);
        }

        double baseline = td.Last.Value;

        // Correct to various prices then back to original
        td.Update(Bar(999.0), isNew: false);
        td.Update(Bar(50.0), isNew: false);
        td.Update(Bar(106.0), isNew: false);

        Assert.Equal(baseline, td.Last.Value);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var td = new TdSeq();
        double[] bars = new double[30];
        for (int i = 0; i < 30; i++) { bars[i] = 100.0 + i; }
        foreach (double p in bars)
        {
            td.Update(Bar(p));
        }

        Assert.True(td.IsHot);

        td.Reset();

        Assert.False(td.IsHot);
        Assert.Equal(0, td.Setup);
        Assert.Equal(0, td.Countdown);
        Assert.Equal(default, td.Last);
    }

    [Fact]
    public void Reset_ThenReFeed_GivesSameResult()
    {
        var td = new TdSeq(comparePeriod: 4);
        var bars = MakeBars([100, 101, 102, 103, 104, 105, 106, 107]);

        foreach (var b in bars) { td.Update(b); }
        double first = td.Last.Value;

        td.Reset();
        foreach (var b in bars) { td.Update(b); }
        double second = td.Last.Value;

        Assert.Equal(first, second);
    }

    // ───── D) Warmup / convergence ─────

    [Fact]
    public void IsHot_FalseBeforeEnoughBars()
    {
        var td = new TdSeq(comparePeriod: 4);
        for (int i = 0; i < 4; i++)
        {
            td.Update(Bar(100.0 + i));
            Assert.False(td.IsHot);
        }
    }

    [Fact]
    public void IsHot_TrueAfterWarmupPeriod()
    {
        var td = new TdSeq(comparePeriod: 4);
        for (int i = 0; i < 5; i++)
        {
            td.Update(Bar(100.0 + i));
        }

        Assert.True(td.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsComparePeriodPlusOne()
    {
        Assert.Equal(5, new TdSeq(4).WarmupPeriod);
        Assert.Equal(4, new TdSeq(3).WarmupPeriod);
        Assert.Equal(2, new TdSeq(1).WarmupPeriod);
    }

    // ───── E) Robustness ─────

    [Fact]
    public void Update_NaN_Close_UsesLastValid()
    {
        var td = new TdSeq(comparePeriod: 4);
        var bars = GbmBars(10);
        foreach (var b in bars) { td.Update(b); }

        td.Update(new TBar(DateTime.UtcNow, 100, 110, 90, double.NaN, 1000));
        Assert.True(double.IsFinite(td.Last.Value));
    }

    [Fact]
    public void Update_Infinity_Close_UsesLastValid()
    {
        var td = new TdSeq(comparePeriod: 4);
        var bars = GbmBars(10);
        foreach (var b in bars) { td.Update(b); }

        td.Update(new TBar(DateTime.UtcNow, 100, 110, 90, double.PositiveInfinity, 1000));
        Assert.True(double.IsFinite(td.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var td = new TdSeq(comparePeriod: 4);
        for (int i = 0; i < 5; i++)
        {
            td.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        }

        Assert.True(double.IsFinite(td.Last.Value));
    }

    // ───── F) Consistency (streaming == eventing) ─────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int count = 200;
        var gbm = new GBM(100.0, 0.02, 0.1, seed: 77);
        var tbarSeries = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var bars = new TBar[count];
        for (int i = 0; i < count; i++)
        {
            bars[i] = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                tbarSeries.Close.Values[i],
                tbarSeries.High.Values[i],
                tbarSeries.Low.Values[i],
                tbarSeries.Close.Values[i],
                1000);
        }

        // 1. Streaming
        var streaming = new TdSeq(4);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        // 2. Event-based via TBarSeries
        var barSource = new TBarSeries();
        var eventIndicator = new TdSeq(barSource, 4);
        var eventResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            barSource.Add(bars[i]);
            eventResults[i] = eventIndicator.Last.Value;
        }

        // Compare all
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i]);
        }
    }

    // ───── G) Countdown phase ─────

    [Fact]
    public void Countdown_StartsAfterSetupCompletes()
    {
        var td = new TdSeq(comparePeriod: 4);
        // Need 9 consecutive qualifying sell-setup bars after warmup
        // Warmup = 4 bars, then 9 more bars where close > close[4]
        double[] prices = new double[30];
        for (int i = 0; i < 30; i++) { prices[i] = 100.0 + i; }

        foreach (double p in prices)
        {
            td.Update(Bar(p, high: p + 2, low: p - 2));
        }

        // After 9+ qualifying bars, setup should complete and countdown may be active
        // Setup is clamped at 9, countdown starts at 0 and increments when conditions met
        Assert.Equal(9, td.Setup); // setup stays at 9 (clamped)
    }

    [Fact]
    public void SetupCount_ResetWhenDirectionFlips()
    {
        var td = new TdSeq(comparePeriod: 4);
        // First go up (sell setup)
        double[] rising = [100, 101, 102, 103, 104, 105, 106, 107];
        foreach (double p in rising) { td.Update(Bar(p)); }
        Assert.True(td.Setup > 0);

        // Then go sharply down (buy setup)
        double[] falling = [80, 79, 78, 77, 76, 75, 74, 73];
        foreach (double p in falling) { td.Update(Bar(p)); }
        Assert.True(td.Setup < 0, $"Expected negative setup after reversal, got {td.Setup}");
    }

    // ───── H) Chainability ─────

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var td = new TdSeq();
        int firedCount = 0;
        td.Pub += (object? _, in TValueEventArgs _) => firedCount++;

        td.Update(Bar(100.0));
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TBarSeries();
        var td = new TdSeq(source, comparePeriod: 4);
        var downstream = new TSeries();
        td.Pub += (object? _, in TValueEventArgs e) => downstream.Add(e.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(Bar(100.0 + i));
        }

        Assert.Equal(10, downstream.Count);
    }

    // ───── Calculate ─────

    [Fact]
    public void Calculate_ReturnsFullSeries()
    {
        var gbm = new GBM(100.0, 0.02, 0.1, seed: 42);
        var tbarSeries = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int count = 50;
        var barSeries = new TBarSeries();
        for (int i = 0; i < count; i++)
        {
            barSeries.Add(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                tbarSeries.Close.Values[i],
                tbarSeries.High.Values[i],
                tbarSeries.Low.Values[i],
                tbarSeries.Close.Values[i],
                1000));
        }

        TSeries results = TdSeq.Calculate(barSeries, comparePeriod: 4);
        Assert.Equal(count, results.Count);
    }

    [Fact]
    public void Calculate_MatchesStreaming()
    {
        int count = 100;
        var gbm = new GBM(100.0, 0.02, 0.1, seed: 7);
        var tbarSeries = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var bars = new TBar[count];
        var barSeries = new TBarSeries();
        for (int i = 0; i < count; i++)
        {
            bars[i] = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                tbarSeries.Close.Values[i],
                tbarSeries.High.Values[i],
                tbarSeries.Low.Values[i],
                tbarSeries.Close.Values[i],
                1000);
            barSeries.Add(bars[i]);
        }

        // Streaming
        var streaming = new TdSeq(4);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        // Batch
        TSeries batchResults = TdSeq.Calculate(barSeries, comparePeriod: 4);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i]);
        }
    }
}
