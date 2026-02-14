using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class CkstopIndicatorTests
{
    [Fact]
    public void CkstopIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CkstopIndicator();

        Assert.Equal(10, indicator.AtrPeriod);
        Assert.Equal(1.0, indicator.Multiplier);
        Assert.Equal(9, indicator.StopPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("CKSTOP", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CkstopIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CkstopIndicator { AtrPeriod = 10 };

        Assert.Equal(0, CkstopIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CkstopIndicator_ShortName_IncludesParameters()
    {
        var indicator = new CkstopIndicator { AtrPeriod = 10, Multiplier = 1.0, StopPeriod = 9 };
        indicator.Initialize();

        Assert.Contains("CKSTOP", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("9", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CkstopIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CkstopIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ckstop", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CkstopIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new CkstopIndicator { AtrPeriod = 10 };

        indicator.Initialize();

        // After init, line series should exist (StopLong + StopShort)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void CkstopIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CkstopIndicator { AtrPeriod = 5, Multiplier = 1.0, StopPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double stopLong = indicator.LinesSeries[0].GetValue(0);
        double stopShort = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(stopLong));
        Assert.True(double.IsFinite(stopShort));
    }

    [Fact]
    public void CkstopIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CkstopIndicator { AtrPeriod = 5, Multiplier = 1.0, StopPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double stopLong = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(stopLong));
    }

    [Fact]
    public void CkstopIndicator_TwoLineSeries_ArePresent()
    {
        var indicator = new CkstopIndicator { AtrPeriod = 5, Multiplier = 1.0, StopPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // StopLong is index 0 (green), StopShort is index 1 (red)
        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
    }

    [Fact]
    public void CkstopIndicator_Description_IsSet()
    {
        var indicator = new CkstopIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("stop", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
