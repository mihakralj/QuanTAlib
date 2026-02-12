using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class SmiIndicatorTests
{
    [Fact]
    public void SmiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SmiIndicator();

        Assert.Equal(10, indicator.KPeriod);
        Assert.Equal(3, indicator.KSmooth);
        Assert.Equal(3, indicator.DSmooth);
        Assert.True(indicator.Blau);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SMI", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SmiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SmiIndicator { KPeriod = 14, KSmooth = 5, DSmooth = 5 };

        Assert.Equal(0, SmiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SmiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new SmiIndicator { KPeriod = 14, KSmooth = 5, DSmooth = 5 };
        indicator.Initialize();

        Assert.Contains("SMI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SmiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SmiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Smi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SmiIndicator_Initialize_CreatesInternalSmi()
    {
        var indicator = new SmiIndicator { KPeriod = 10, KSmooth = 3, DSmooth = 3 };

        indicator.Initialize();

        // After init, line series should exist (K, D)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void SmiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SmiIndicator { KPeriod = 5, KSmooth = 3, DSmooth = 3 };
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
    public void SmiIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SmiIndicator { KPeriod = 5, KSmooth = 3, DSmooth = 3 };
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
