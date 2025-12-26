using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class CovarianceIndicatorTests
{
    [Fact]
    public void CovarianceIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CovarianceIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.False(indicator.IsPopulation);
        Assert.Equal(SourceType.Close, indicator.Source1);
        Assert.Equal(SourceType.Open, indicator.Source2);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Covariance", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CovarianceIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new CovarianceIndicator { Period = 20 };

        Assert.Equal(2, CovarianceIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(2, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CovarianceIndicator_Initialize_CreatesInternalCovariance()
    {
        var indicator = new CovarianceIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Covariance", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void CovarianceIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CovarianceIndicator { Period = 5 };
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
        double cov = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(cov));
    }
}
