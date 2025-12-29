using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class SkewIndicatorTests
{
    [Fact]
    public void SkewIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SkewIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.False(indicator.IsPopulation);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Skew - Skewness", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void SkewIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SkewIndicator { Period = 20 };

        Assert.Equal(0, SkewIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SkewIndicator_Initialize_CreatesInternalSkew()
    {
        var indicator = new SkewIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Skew", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void SkewIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SkewIndicator { Period = 5 };
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
        double skew = indicator.LinesSeries[0].GetValue(0);

        // Skew of a linear trend (100, 101, 102...) is 0 (symmetric)
        Assert.True(double.IsFinite(skew));
        Assert.Equal(0, skew, 9);
    }
}
