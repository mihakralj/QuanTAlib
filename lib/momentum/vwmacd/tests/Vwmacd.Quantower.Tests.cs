using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class VwmacdIndicatorTests
{
    [Fact]
    public void VwmacdIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VwmacdIndicator();

        Assert.Equal(12, indicator.FastPeriod);
        Assert.Equal(26, indicator.SlowPeriod);
        Assert.Equal(9, indicator.SignalPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("VWMACD", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VwmacdIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new VwmacdIndicator();
        Assert.Equal(0, VwmacdIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void VwmacdIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VwmacdIndicator { FastPeriod = 12, SlowPeriod = 26, SignalPeriod = 9 };
        indicator.Initialize();

        Assert.Contains("VWMACD", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("12", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("26", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VwmacdIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new VwmacdIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Vwmacd", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void VwmacdIndicator_Initialize_CreatesThreeLineSeries()
    {
        var indicator = new VwmacdIndicator { FastPeriod = 12, SlowPeriod = 26, SignalPeriod = 9 };
        indicator.Initialize();

        // VWMACD + Signal + Histogram
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void VwmacdIndicator_SeparateWindow_True()
    {
        var indicator = new VwmacdIndicator();
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void VwmacdIndicator_CustomParams_ShortName()
    {
        var indicator = new VwmacdIndicator { FastPeriod = 5, SlowPeriod = 10, SignalPeriod = 3 };
        indicator.Initialize();

        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("3", indicator.ShortName, StringComparison.Ordinal);
    }
}
