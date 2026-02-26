using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class WavgIndicatorTests
{
    [Fact]
    public void WavgIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WavgIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Wavg - Linearly Weighted Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void WavgIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new WavgIndicator { Period = 14 };

        Assert.Equal(0, WavgIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void WavgIndicator_Initialize_CreatesInternalWavg()
    {
        var indicator = new WavgIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Wavg", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void WavgIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WavgIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + Math.Sin(i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 2, close - 2, close);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }
}
