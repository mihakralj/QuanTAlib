// TtmTrend: Mathematical property validation tests
// TTM Trend is a proprietary John Carter indicator — no external library equivalents exist.
// Validation uses mathematical property testing against known EMA behaviors.

namespace QuanTAlib.Tests;

using Xunit;

public class TtmTrendValidationTests
{
    private const int DefaultPeriod = 6;
    private const int TestDataLength = 500;

    [Fact]
    public void TtmTrend_EmaOutput_IsFiniteForGbmData()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ttm = new TtmTrend(DefaultPeriod);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = ttm.Update(bars[i], isNew: true);
            Assert.True(double.IsFinite(result.Value),
                $"TtmTrend output must be finite at bar {i}, got {result.Value}");
        }
    }

    [Fact]
    public void TtmTrend_TrendDirection_OnlyValidValues()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ttm = new TtmTrend(DefaultPeriod);

        for (int i = 0; i < bars.Count; i++)
        {
            ttm.Update(bars[i], isNew: true);

            Assert.True(ttm.Trend is -1 or 0 or 1,
                $"Trend must be -1, 0, or 1 at bar {i}, got {ttm.Trend}");
        }
    }

    [Fact]
    public void TtmTrend_Strength_IsNonNegative()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ttm = new TtmTrend(DefaultPeriod);

        for (int i = 0; i < bars.Count; i++)
        {
            ttm.Update(bars[i], isNew: true);

            Assert.True(ttm.Strength >= 0,
                $"Strength must be >= 0 at bar {i}, got {ttm.Strength}");
        }
    }

    [Fact]
    public void TtmTrend_RisingSequence_BullishTrend()
    {
        var ttm = new TtmTrend(DefaultPeriod);
        double basePrice = 100.0;

        // Feed enough bars to warm up, then inject consistently rising prices
        for (int i = 0; i < 20; i++)
        {
            double price = basePrice + i * 2.0;
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                price - 0.5, price + 0.5, price - 0.5, price, 1000);
            ttm.Update(bar, isNew: true);
        }

        // After a consistently rising sequence, trend should be bullish
        Assert.Equal(1, ttm.Trend);
    }

    [Fact]
    public void TtmTrend_FallingSequence_BearishTrend()
    {
        var ttm = new TtmTrend(DefaultPeriod);
        double basePrice = 200.0;

        // Feed enough bars to warm up, then inject consistently falling prices
        for (int i = 0; i < 20; i++)
        {
            double price = basePrice - i * 2.0;
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                price + 0.5, price + 0.5, price - 0.5, price, 1000);
            ttm.Update(bar, isNew: true);
        }

        // After a consistently falling sequence, trend should be bearish
        Assert.Equal(-1, ttm.Trend);
    }

    [Fact]
    public void TtmTrend_ConstantPrice_ZeroStrength()
    {
        var ttm = new TtmTrend(DefaultPeriod);
        double price = 100.0;

        // Feed constant-price bars
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                price, price, price, price, 1000);
            ttm.Update(bar, isNew: true);
        }

        // Strength should be 0 for a constant series (no percent change)
        Assert.Equal(0.0, ttm.Strength, precision: 10);
    }

    [Fact]
    public void TtmTrend_EmaConvergesToConstant()
    {
        var ttm = new TtmTrend(DefaultPeriod);
        double targetPrice = 100.0;

        // Start at 50, abruptly switch to constant 100
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                50, 50, 50, 50, 1000);
            ttm.Update(bar, isNew: true);
        }

        // Now feed constant 100 for many bars
        for (int i = 5; i < 100; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                targetPrice, targetPrice, targetPrice, targetPrice, 1000);
            ttm.Update(bar, isNew: true);
        }

        // EMA output should converge to the target price
        Assert.Equal(targetPrice, ttm.Last.Value, precision: 6);
    }

    [Fact]
    public void TtmTrend_BatchAndStreaming_ProduceSameResults()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(TestDataLength, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch mode
        var batchResults = TtmTrend.Batch(bars, DefaultPeriod);

        // Streaming mode
        var streamTtm = new TtmTrend(DefaultPeriod);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var result = streamTtm.Update(bars[i], isNew: true);
            streamResults[i] = result.Value;
        }

        // Both must match
        Assert.Equal(batchResults.Count, bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], precision: 10);
        }
    }

    [Fact]
    public void TtmTrend_DifferentPeriods_ProduceDifferentEmaSmoothing()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ttm3 = new TtmTrend(period: 3);
        var ttm20 = new TtmTrend(period: 20);

        for (int i = 0; i < bars.Count; i++)
        {
            ttm3.Update(bars[i], isNew: true);
            ttm20.Update(bars[i], isNew: true);
        }

        // Different periods should produce different final values (except on trivially constant data)
        Assert.NotEqual(ttm3.Last.Value, ttm20.Last.Value);
    }

    [Fact]
    public void TtmTrend_IsHot_AfterWarmup()
    {
        var ttm = new TtmTrend(DefaultPeriod);

        // First bar: not hot
        var bar1 = new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000);
        ttm.Update(bar1, isNew: true);
        Assert.False(ttm.IsHot);

        // Second bar: should be hot (warmup period = 2)
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 101, 102, 100, 101, 1000);
        ttm.Update(bar2, isNew: true);
        Assert.True(ttm.IsHot);
    }

    [Fact]
    public void TtmTrend_BarCorrection_IsNewFalse_RestoresState()
    {
        var bars = new GBM(sigma: 0.5, seed: 123).Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ttm = new TtmTrend(DefaultPeriod);

        // Process 30 bars
        for (int i = 0; i < 30; i++)
        {
            ttm.Update(bars[i], isNew: true);
        }

        _ = ttm.Last.Value;

        // Update bar 30 (isNew=true) then correct it (isNew=false) with same value
        ttm.Update(bars[30], isNew: true);
        double afterNew = ttm.Last.Value;

        // Correct with isNew=false using same bar
        ttm.Update(bars[30], isNew: false);
        double afterCorrection = ttm.Last.Value;

        // Bar correction with same data should produce the same value
        Assert.Equal(afterNew, afterCorrection, precision: 10);
    }
}
