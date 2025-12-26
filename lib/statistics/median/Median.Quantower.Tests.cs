using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class MedianIndicatorTests
{
    [Fact]
    public void MedianIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MedianIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Median - Rolling Median", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void MedianIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new MedianIndicator { Period = 20 };

        Assert.Equal(0, MedianIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MedianIndicator_Initialize_CreatesInternalMedian()
    {
        var indicator = new MedianIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Median", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void MedianIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MedianIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double median = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(median));
    }
}
