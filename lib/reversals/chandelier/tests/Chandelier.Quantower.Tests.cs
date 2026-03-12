using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class ChandelierIndicatorTests
{
    [Fact]
    public void ChandelierIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ChandelierIndicator();

        Assert.Equal(22, indicator.Period);
        Assert.Equal(3.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("CHANDELIER", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ChandelierIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ChandelierIndicator { Period = 22 };

        Assert.Equal(0, ChandelierIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ChandelierIndicator_ShortName_IncludesParameters()
    {
        var indicator = new ChandelierIndicator { Period = 22, Multiplier = 3.0 };
        indicator.Initialize();

        Assert.Contains("CHANDELIER", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("22", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ChandelierIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ChandelierIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Chandelier", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ChandelierIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new ChandelierIndicator { Period = 22 };

        indicator.Initialize();

        // After init, line series should exist (ExitLong + ExitShort)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void ChandelierIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ChandelierIndicator { Period = 5, Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double exitLong = indicator.LinesSeries[0].GetValue(0);
        double exitShort = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(exitLong));
        Assert.True(double.IsFinite(exitShort));
    }

    [Fact]
    public void ChandelierIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ChandelierIndicator { Period = 5, Multiplier = 1.0 };
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

        double exitLong = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(exitLong));
    }

    [Fact]
    public void ChandelierIndicator_TwoLineSeries_ArePresent()
    {
        var indicator = new ChandelierIndicator { Period = 5, Multiplier = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // ExitLong is index 0 (green), ExitShort is index 1 (red)
        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
    }

    [Fact]
    public void ChandelierIndicator_Description_IsSet()
    {
        var indicator = new ChandelierIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("exit", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
