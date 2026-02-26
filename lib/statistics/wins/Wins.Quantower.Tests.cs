using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class WinsIndicatorTests
{
    [Fact]
    public void WinsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WinsIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(10.0, indicator.WinPct);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Wins - Winsorized Mean Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void WinsIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new WinsIndicator { Period = 20 };

        Assert.Equal(0, WinsIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void WinsIndicator_Initialize_CreatesInternalWins()
    {
        var indicator = new WinsIndicator { Period = 10, WinPct = 10.0 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Wins", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void WinsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WinsIndicator { Period = 5, WinPct = 10.0 };
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
