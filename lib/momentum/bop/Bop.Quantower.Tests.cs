using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class BopIndicatorTests
{
    [Fact]
    public void BopIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BopIndicator();

        Assert.Equal("BOP - Balance of Power", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BopIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BopIndicator();

        Assert.Equal(0, indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BopIndicator_ShortName_IsBop()
    {
        var indicator = new BopIndicator();
        indicator.Initialize();

        Assert.Equal("BOP", indicator.ShortName);
    }

    [Fact]
    public void BopIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BopIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Bop.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void BopIndicator_Initialize_CreatesInternalBop()
    {
        var indicator = new BopIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (BOP)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BopIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BopIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 10, 20, 5, 15);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        double bop = indicator.LinesSeries[0].GetValue(0);

        // Open=10, High=20, Low=5, Close=15
        // Range=15, Diff=5, BOP=0.333...
        Assert.Equal(1.0/3.0, bop, 6);
    }
}
