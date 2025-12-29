
namespace QuanTAlib.Tests;

public class GBMTests
{
    [Fact]
    public void Next_DefaultParameter_GeneratesNewBar()
    {
        var gbm = new GBM(startPrice: 100.0);

        var bar1 = gbm.Next();
        var bar2 = gbm.Next();

        Assert.NotEqual(bar1.Time, bar2.Time);
        Assert.True(bar2.Time > bar1.Time);
    }

    [Fact]
    public void Next_IsNewTrue_AdvancesToNewBar()
    {
        var gbm = new GBM(startPrice: 100.0);

        var bar1 = gbm.Next(isNew: true);
        var bar2 = gbm.Next(isNew: true);

        Assert.NotEqual(bar1.Time, bar2.Time);
        Assert.True(bar2.Time > bar1.Time);
    }

    [Fact]
    public void Next_IsNewFalse_UpdatesCurrentBar()
    {
        var gbm = new GBM(startPrice: 100.0);

        var bar1 = gbm.Next(isNew: true);
        long initialTime = bar1.Time;

        var bar2 = gbm.Next(isNew: false);

        Assert.Equal(initialTime, bar2.Time);
        // Price likely changed (GBM random walk)
        Assert.NotEqual(bar1.Close, bar2.Close);
    }

    [Fact]
    public void Next_RefBool_HonorsRequest()
    {
        var gbm = new GBM(startPrice: 100.0);

        // GBM always honors isNew - parameter should remain unchanged
        bool isNew1 = true;
        var bar1 = gbm.Next(ref isNew1);
        Assert.True(isNew1, "GBM should honor isNew=true request");

        bool isNew2 = false;
        long time1 = bar1.Time;
        var bar2 = gbm.Next(ref isNew2);
        Assert.False(isNew2, "GBM should honor isNew=false request");
        Assert.Equal(time1, bar2.Time);

        bool isNew3 = true;
        var bar3 = gbm.Next(ref isNew3);
        Assert.True(isNew3, "GBM should honor isNew=true request");
        Assert.NotEqual(time1, bar3.Time);
    }

    [Fact]
    public void Fetch_GeneratesCorrectCount()
    {
        var gbm = new GBM(startPrice: 100.0);
        int count = 10;
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series = gbm.Fetch(count, startTime, interval);

        Assert.Equal(count, series.Count);
    }

    [Fact]
    public void Fetch_GeneratesSequentialBars()
    {
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);

        var series = gbm.Fetch(5, startTime, interval);

        // Verify time sequence
        for (int i = 1; i < series.Count; i++)
        {
            Assert.True(series[i].Time > series[i - 1].Time);
        }
    }

    [Fact]
    public void Fetch_RespectsInterval()
    {
        var gbm = new GBM(startPrice: 100.0);
        var interval = TimeSpan.FromHours(1);
        long startTime = DateTime.UtcNow.Ticks;

        var series = gbm.Fetch(5, startTime, interval);

        // Verify interval spacing
        for (int i = 1; i < series.Count; i++)
        {
            long expectedDiff = interval.Ticks;
            long actualDiff = series[i].Time - series[i - 1].Time;
            Assert.Equal(expectedDiff, actualDiff);
        }
    }

    [Fact]
    public void Fetch_StartsAtSpecifiedTime()
    {
        var gbm = new GBM(startPrice: 100.0);
        var startTime = new DateTime(2024, 1, 1, 9, 30, 0, DateTimeKind.Utc).Ticks;
        var interval = TimeSpan.FromMinutes(5);

        var series = gbm.Fetch(3, startTime, interval);

        Assert.Equal(startTime, series[0].Time);
        Assert.Equal(startTime + interval.Ticks, series[1].Time);
        Assert.Equal(startTime + 2 * interval.Ticks, series[2].Time);
    }

    [Fact]
    public void Fetch_WithDifferentIntervals_WorksCorrectly()
    {
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;

        // Test different intervals
        var intervals = new[] {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromHours(1)
        };

        foreach (var interval in intervals)
        {
            var series = gbm.Fetch(3, startTime, interval);

            // Verify spacing
            for (int i = 1; i < series.Count; i++)
            {
                long expectedDiff = interval.Ticks;
                long actualDiff = series[i].Time - series[i - 1].Time;
                Assert.Equal(expectedDiff, actualDiff);
            }
        }
    }

    [Fact]
    public void GeneratesRealisticOHLCV()
    {
        var gbm = new GBM(startPrice: 100.0);
        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var series = gbm.Fetch(10, startTime, interval);

        for (int i = 0; i < series.Count; i++)
        {
            var bar = series[i];

            // High should be >= max(Open, Close)
            Assert.True(bar.High >= Math.Max(bar.Open, bar.Close));

            // Low should be <= min(Open, Close)
            Assert.True(bar.Low <= Math.Min(bar.Open, bar.Close));

            // Volume should be positive
            Assert.True(bar.Volume > 0);

            // All prices should be positive
            Assert.True(bar.Open > 0);
            Assert.True(bar.High > 0);
            Assert.True(bar.Low > 0);
            Assert.True(bar.Close > 0);
        }
    }

    [Fact]
    public void IntraBarUpdates_ModifyCurrentBar()
    {
        var gbm = new GBM(startPrice: 100.0);

        var bar1 = gbm.Next(isNew: true);
        long initialTime = bar1.Time;
        double initialClose = bar1.Close;

        // Loop until price changes (random walk might stay same but unlikely)
        bool changed = false;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: false);
            Assert.Equal(initialTime, bar.Time);
            if (Math.Abs(bar.Close - initialClose) > double.Epsilon)
            {
                changed = true;
                break;
            }
        }

        Assert.True(changed, "Price should change during intra-bar updates");
    }

    [Fact]
    public void MixedStreamingAndBatch_WorksCorrectly()
    {
        var gbm = new GBM(startPrice: 100.0);

        // Start with streaming
        _ = gbm.Next();
        var bar2 = gbm.Next();

        // Batch generation with explicit time
        long startTime = bar2.Time + TimeSpan.FromMinutes(1).Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var series = gbm.Fetch(3, startTime, interval);

        Assert.True(series[0].Time > bar2.Time);
        Assert.Equal(3, series.Count);

        // Continue streaming after batch (uses internal state)
        var bar3 = gbm.Next();
        Assert.True(bar3.Time > series[2].Time);
    }

    [Fact]
    public void DriftAndVolatility_AffectPriceMovement()
    {
        // High volatility should produce more price variation
        var gbmLowVol = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.01);
        var gbmHighVol = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.5);

        long startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromMinutes(1);
        var seriesLow = gbmLowVol.Fetch(100, startTime, interval);
        var seriesHigh = gbmHighVol.Fetch(100, startTime, interval);

        // Calculate price ranges
        double rangeLow = seriesLow[99].Close - seriesLow[0].Open;
        double rangeHigh = seriesHigh[99].Close - seriesHigh[0].Open;

        // High volatility should generally produce larger absolute movements
        Assert.True(Math.Abs(rangeHigh) > Math.Abs(rangeLow) * 0.5);
    }

    [Fact]
    public void ConsecutiveCalls_MaintainContinuity()
    {
        var gbm = new GBM(startPrice: 100.0);

        var previousBar = gbm.Next();
        var currentBar = gbm.Next();

        // currentBar.Open should equal previousBar.Close (continuity)
        Assert.Equal(previousBar.Close, currentBar.Open);
    }

    [Fact]
    public void Stateless_NoHistoryStorage()
    {
        var gbm = new GBM(startPrice: 100.0);

        // Generate multiple bars
        for (int i = 0; i < 100; i++)
        {
            _ = gbm.Next();
        }

        // GBM should not expose any history storage
        // Use typeof() instead of GetType() to satisfy trimming analyzer
        var type = typeof(GBM);
        var barsProperty = type.GetProperty("Bars");

        Assert.Null(barsProperty);
    }
}
