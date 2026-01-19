using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class KalmanIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var indicator = new KalmanIndicator();

        Assert.Equal(0.01, indicator.Q);
        Assert.Equal(0.1, indicator.R);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal("Kalman - Kalman Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void Initialize_CreatesInternalIndicator()
    {
        var indicator = new KalmanIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ProcessUpdate_CalculatesValue()
    {
        var indicator = new KalmanIndicator();
        indicator.Initialize();

        indicator.HistoricalData.AddBar(DateTime.UtcNow, 100, 105, 95, 100);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }
}