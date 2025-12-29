using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AdxIndicatorTests
{
    [Fact]
    public void AdxIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AdxIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ADX - Average Directional Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AdxIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AdxIndicator { Period = 20 };

        Assert.Equal(0, AdxIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AdxIndicator_Initialize_CreatesInternalAdx()
    {
        var indicator = new AdxIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (ADX, +DI, -DI)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AdxIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AdxIndicator { Period = 5 };
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
        double adx = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(adx));
    }
}
