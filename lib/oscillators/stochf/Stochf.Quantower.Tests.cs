using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class StochfIndicatorTests
{
    [Fact]
    public void StochfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StochfIndicator();

        Assert.Equal(5, indicator.KLength);
        Assert.Equal(3, indicator.DPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("STOCHF", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void StochfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new StochfIndicator { KLength = 5, DPeriod = 3 };

        Assert.Equal(0, StochfIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void StochfIndicator_ShortName_IncludesParameters()
    {
        var indicator = new StochfIndicator { KLength = 5, DPeriod = 5 };
        indicator.Initialize();

        Assert.Contains("STOCHF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void StochfIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new StochfIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Stochf", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void StochfIndicator_Initialize_CreatesInternalStochf()
    {
        var indicator = new StochfIndicator { KLength = 5, DPeriod = 3 };

        indicator.Initialize();

        // After init, line series should exist (K, D)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void StochfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new StochfIndicator { KLength = 5, DPeriod = 3 };
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
    public void StochfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new StochfIndicator { KLength = 5, DPeriod = 3 };
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
