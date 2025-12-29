using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class StdDevIndicatorTests
{
    [Fact]
    public void StdDevIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StdDevIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.False(indicator.IsPopulation);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("StdDev - Standard Deviation", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void StdDevIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new StdDevIndicator { Period = 20 };

        Assert.Equal(0, StdDevIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void StdDevIndicator_Initialize_CreatesInternalStdDev()
    {
        var indicator = new StdDevIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("StdDev", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void StdDevIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new StdDevIndicator { Period = 5 };
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
        double stdDev = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(stdDev));
    }
}
