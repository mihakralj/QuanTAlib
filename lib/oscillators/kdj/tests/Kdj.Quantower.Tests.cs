using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class KdjIndicatorTests
{
    [Fact]
    public void KdjIndicator_Constructor_SetsDefaults()
    {
        var indicator = new KdjIndicator();

        Assert.Equal(9, indicator.Length);
        Assert.Equal(3, indicator.Signal);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("KDJ", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void KdjIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new KdjIndicator { Length = 14, Signal = 5 };

        Assert.Equal(0, KdjIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void KdjIndicator_ShortName_IncludesParameters()
    {
        var indicator = new KdjIndicator { Length = 14, Signal = 5 };
        indicator.Initialize();

        Assert.Contains("KDJ", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void KdjIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new KdjIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Kdj.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void KdjIndicator_Initialize_CreatesInternalKdj()
    {
        var indicator = new KdjIndicator { Length = 9, Signal = 3 };

        indicator.Initialize();

        // After init, line series should exist (K, D, J)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void KdjIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new KdjIndicator { Length = 5, Signal = 3 };
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
        double j = indicator.LinesSeries[2].GetValue(0);

        Assert.True(double.IsFinite(k));
        Assert.True(double.IsFinite(d));
        Assert.True(double.IsFinite(j));
    }

    [Fact]
    public void KdjIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new KdjIndicator { Length = 5, Signal = 3 };
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
        double j = indicator.LinesSeries[2].GetValue(0);

        Assert.True(double.IsFinite(k));
        Assert.True(double.IsFinite(d));
        Assert.True(double.IsFinite(j));
    }
}
