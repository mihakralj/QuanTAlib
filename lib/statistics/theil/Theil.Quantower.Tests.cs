using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class TheilIndicatorTests
{
    [Fact]
    public void TheilIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TheilIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Theil - Theil T Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void TheilIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new TheilIndicator { Period = 14 };

        Assert.Equal(0, TheilIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TheilIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new TheilIndicator { Period = 20 };
        Assert.Equal("Theil 20", indicator.ShortName);
    }

    [Fact]
    public void TheilIndicator_Initialize_CreatesInternalTheil()
    {
        var indicator = new TheilIndicator { Period = 10 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Theil", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void TheilIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TheilIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double theil = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(theil));
        Assert.True(theil >= -1e-10, $"Expected non-negative Theil, got {theil}");
    }

    [Fact]
    public void TheilIndicator_NewBar_UpdatesValue()
    {
        var indicator = new TheilIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        _ = indicator.LinesSeries[0].GetValue(0);

        indicator.HistoricalData.AddBar(now.AddMinutes(10), 200, 210, 190, 205);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double valueAfter = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(valueAfter));
    }

    [Fact]
    public void TheilIndicator_DifferentSourceTypes()
    {
        var indicator = new TheilIndicator { Period = 5, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double theil = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(theil));
    }
}
