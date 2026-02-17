using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ModeIndicatorTests
{
    [Fact]
    public void ModeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ModeIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Mode - Statistical Mode (Most Frequent Value)", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void ModeIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ModeIndicator { Period = 14 };

        Assert.Equal(0, ModeIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ModeIndicator_Initialize_CreatesInternalMode()
    {
        var indicator = new ModeIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Mode", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void ModeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ModeIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data with repeating close prices to produce a mode
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + (i % 3); // cycles 100, 101, 102, 100, 101, ...
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 5, close - 5, close);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double mode = indicator.LinesSeries[0].GetValue(0);

        // Mode of cycling values should be finite
        Assert.True(double.IsFinite(mode));
    }
}
