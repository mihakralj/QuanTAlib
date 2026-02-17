using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class QuantileIndicatorTests
{
    [Fact]
    public void QuantileIndicator_Constructor_DefaultValues()
    {
        var indicator = new QuantileIndicator();
        Assert.Equal(14, indicator.Period);
        Assert.Equal(0.5, indicator.QuantileLevel);
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void QuantileIndicator_MinHistoryDepths()
    {
        var indicator = new QuantileIndicator { Period = 20 };
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void QuantileIndicator_Initialize_CreatesInternalQuantile()
    {
        var indicator = new QuantileIndicator { Period = 10, QuantileLevel = 0.75 };
        indicator.Initialize();

        Assert.Equal("Quantile 10 (0.75)", indicator.ShortName);
    }

    [Fact]
    public void QuantileIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new QuantileIndicator { Period = 5, QuantileLevel = 0.75 };
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
        double quantile = indicator.LinesSeries[0].GetValue(0);

        // Quantile of a trending series should be finite
        Assert.True(double.IsFinite(quantile));
    }
}
