using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class TrimIndicatorTests
{
    [Fact]
    public void TrimIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TrimIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(10.0, indicator.TrimPct);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Trim - Trimmed Mean Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void TrimIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new TrimIndicator { Period = 20 };

        Assert.Equal(0, TrimIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TrimIndicator_Initialize_CreatesInternalTrim()
    {
        var indicator = new TrimIndicator { Period = 10, TrimPct = 10.0 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Trim", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void TrimIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TrimIndicator { Period = 5, TrimPct = 10.0 };
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
