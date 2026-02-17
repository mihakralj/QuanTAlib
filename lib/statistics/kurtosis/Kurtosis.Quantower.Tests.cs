using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class KurtosisIndicatorTests
{
    [Fact]
    public void KurtosisIndicator_Constructor_SetsDefaults()
    {
        var indicator = new KurtosisIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.False(indicator.IsPopulation);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Kurtosis - Excess Kurtosis", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void KurtosisIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new KurtosisIndicator { Period = 20 };

        Assert.Equal(0, KurtosisIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void KurtosisIndicator_Initialize_CreatesInternalKurtosis()
    {
        var indicator = new KurtosisIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Kurtosis", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void KurtosisIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new KurtosisIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double kurtosis = indicator.LinesSeries[0].GetValue(0);

        // Kurtosis of a linear trend should be finite
        Assert.True(double.IsFinite(kurtosis));
    }
}
