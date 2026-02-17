using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class JbIndicatorTests
{
    [Fact]
    public void JbIndicator_Constructor_SetsDefaults()
    {
        var indicator = new JbIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("JB - Jarque-Bera Test", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void JbIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new JbIndicator { Period = 20 };

        Assert.Equal(0, JbIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void JbIndicator_Initialize_CreatesInternalJb()
    {
        var indicator = new JbIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Equal(4, indicator.LinesSeries.Count);
        Assert.Equal("JB", indicator.LinesSeries[0].Name);
        Assert.Equal("10%", indicator.LinesSeries[1].Name);
        Assert.Equal("5%", indicator.LinesSeries[2].Name);
        Assert.Equal("1%", indicator.LinesSeries[3].Name);
    }

    [Fact]
    public void JbIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new JbIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double jb = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(jb));
    }

    [Fact]
    public void JbIndicator_DifferentSourceTypes()
    {
        var indicator = new JbIndicator { Period = 5, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double jb = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(jb));
    }

    [Fact]
    public void JbIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new JbIndicator { Period = 30 };
        Assert.Equal("JB 30", indicator.ShortName);
    }

    [Fact]
    public void JbIndicator_NewBar_UpdatesValue()
    {
        var indicator = new JbIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        _ = indicator.LinesSeries[0].GetValue(0);

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 200, 210, 190, 205);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double valueAfter = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(valueAfter));
    }

    [Fact]
    public void JbIndicator_CriticalValueLines_AreSet()
    {
        var indicator = new JbIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Critical value lines should be set
        Assert.Equal(4.605, indicator.LinesSeries[1].GetValue(0), 3);
        Assert.Equal(5.991, indicator.LinesSeries[2].GetValue(0), 3);
        Assert.Equal(9.210, indicator.LinesSeries[3].GetValue(0), 3);
    }
}
