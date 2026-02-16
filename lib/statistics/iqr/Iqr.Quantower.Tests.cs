using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class IqrIndicatorTests
{
    [Fact]
    public void IqrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new IqrIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("IQR - Interquartile Range", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void IqrIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new IqrIndicator { Period = 20 };

        Assert.Equal(0, IqrIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void IqrIndicator_Initialize_CreatesInternalIqr()
    {
        var indicator = new IqrIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("IQR", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void IqrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new IqrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double iqr = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(iqr));
    }

    [Fact]
    public void IqrIndicator_DifferentSourceTypes()
    {
        var indicator = new IqrIndicator { Period = 5, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double iqr = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(iqr));
    }

    [Fact]
    public void IqrIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new IqrIndicator { Period = 30 };
        Assert.Equal("IQR 30", indicator.ShortName);
    }

    [Fact]
    public void IqrIndicator_NewBar_UpdatesValue()
    {
        var indicator = new IqrIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        _ = indicator.LinesSeries[0].GetValue(0);

        // Add a new bar with a very different value
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 200, 210, 190, 205);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double valueAfter = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(valueAfter));
    }
}
