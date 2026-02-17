using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PercentileIndicatorTests
{
    [Fact]
    public void PercentileIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PercentileIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(50.0, indicator.Percent);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Percentile - Rolling Percentile", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void PercentileIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PercentileIndicator { Period = 14 };

        Assert.Equal(0, PercentileIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PercentileIndicator_Initialize_CreatesInternalPercentile()
    {
        var indicator = new PercentileIndicator { Period = 10, Percent = 25.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Percentile", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void PercentileIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PercentileIndicator { Period = 5, Percent = 75.0 };
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
        double percentile = indicator.LinesSeries[0].GetValue(0);

        // Percentile of a trending series should be finite
        Assert.True(double.IsFinite(percentile));
    }
}
