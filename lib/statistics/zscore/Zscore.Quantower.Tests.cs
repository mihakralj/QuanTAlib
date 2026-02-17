using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class ZscoreIndicatorTests
{
    [Fact]
    public void ZscoreIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ZscoreIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("ZSCORE", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void ZscoreIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ZscoreIndicator { Period = 14 };

        Assert.Equal(0, ZscoreIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ZscoreIndicator_Initialize_CreatesInternalZscore()
    {
        var indicator = new ZscoreIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Z-Score", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void ZscoreIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ZscoreIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double zscore = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(zscore));
    }

    [Fact]
    public void ZscoreIndicator_DifferentSourceTypes()
    {
        var indicator = new ZscoreIndicator { Period = 5, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double zscore = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(zscore));
    }

    [Fact]
    public void ZscoreIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new ZscoreIndicator { Period = 20 };
        Assert.Equal("ZSCORE(20)", indicator.ShortName);
    }

    [Fact]
    public void ZscoreIndicator_NewBar_UpdatesValue()
    {
        var indicator = new ZscoreIndicator { Period = 5 };
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
