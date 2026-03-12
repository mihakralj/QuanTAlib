using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class StochIndicatorTests
{
    [Fact]
    public void StochIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StochIndicator();

        Assert.Equal(14, indicator.KLength);
        Assert.Equal(3, indicator.DPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("STOCH", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void StochIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new StochIndicator { KLength = 14, DPeriod = 3 };

        Assert.Equal(0, StochIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void StochIndicator_ShortName_IncludesParameters()
    {
        var indicator = new StochIndicator { KLength = 14, DPeriod = 5 };
        indicator.Initialize();

        Assert.Contains("STOCH", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void StochIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new StochIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Stoch", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void StochIndicator_Initialize_CreatesInternalStoch()
    {
        var indicator = new StochIndicator { KLength = 14, DPeriod = 3 };

        indicator.Initialize();

        // After init, line series should exist (K, D)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void StochIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new StochIndicator { KLength = 5, DPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double k = indicator.LinesSeries[0].GetValue(0);
        double d = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(k));
        Assert.True(double.IsFinite(d));
    }

    [Fact]
    public void StochIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new StochIndicator { KLength = 5, DPeriod = 3 };
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

        double k = indicator.LinesSeries[0].GetValue(0);
        double d = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(k));
        Assert.True(double.IsFinite(d));
    }
}
