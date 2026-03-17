using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class HwcIndicatorTests
{
    [Fact]
    public void HwcIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HwcIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(1.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("HWC", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HwcIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HwcIndicator();
        Assert.Equal(0, HwcIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void HwcIndicator_ShortName_IncludesParameters()
    {
        var indicator = new HwcIndicator { Period = 20, Multiplier = 1.5 };
        indicator.Initialize();

        Assert.Contains("HWC", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void HwcIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new HwcIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Hwc", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void HwcIndicator_Initialize_CreatesThreeLineSeries()
    {
        var indicator = new HwcIndicator { Period = 20, Multiplier = 1.0 };
        indicator.Initialize();

        // Upper + Middle + Lower
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void HwcIndicator_SeparateWindow_False()
    {
        var indicator = new HwcIndicator();
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void HwcIndicator_CustomParams_ShortName()
    {
        var indicator = new HwcIndicator { Period = 10, Multiplier = 2.0 };
        indicator.Initialize();

        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.0", indicator.ShortName, StringComparison.Ordinal);
    }
}
