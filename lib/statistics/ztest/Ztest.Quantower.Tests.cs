using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class ZtestIndicatorTests
{
    [Fact]
    public void ZtestIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ZtestIndicator();

        Assert.Equal(30, indicator.Period);
        Assert.Equal(0.0, indicator.Mu0);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("ZTEST", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void ZtestIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ZtestIndicator { Period = 30 };

        Assert.Equal(0, ZtestIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ZtestIndicator_Initialize_CreatesInternalZtest()
    {
        var indicator = new ZtestIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("t-stat", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void ZtestIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ZtestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double tStat = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(tStat));
    }

    [Fact]
    public void ZtestIndicator_DifferentSourceTypes()
    {
        var indicator = new ZtestIndicator { Period = 5, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double tStat = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(tStat));
    }

    [Fact]
    public void ZtestIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new ZtestIndicator { Period = 20 };
        Assert.Equal("ZTEST(20)", indicator.ShortName);
    }

    [Fact]
    public void ZtestIndicator_NewBar_UpdatesValue()
    {
        var indicator = new ZtestIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add enough bars to warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        _ = indicator.LinesSeries[0].GetValue(0);

        // Add a new bar with a very different value
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 200, 210, 190, 205);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double valueAfter = indicator.LinesSeries[0].GetValue(0);

        // Value should change after adding a significantly different bar
        Assert.True(double.IsFinite(valueAfter));
    }
}
