using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AdxrIndicatorTests
{
    [Fact]
    public void AdxrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AdxrIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ADXR - Average Directional Movement Rating", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AdxrIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AdxrIndicator { Period = 20 };

        Assert.Equal(0, AdxrIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }


    [Fact]
    public void AdxrIndicator_Initialize_CreatesInternalAdxr()
    {
        var indicator = new AdxrIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (ADXR)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AdxrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AdxrIndicator { Period = 5 };
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
        double adxr = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(adxr));
    }
}
