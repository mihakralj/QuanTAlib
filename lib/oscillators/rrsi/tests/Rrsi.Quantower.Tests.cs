using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class RrsiIndicatorTests
{
    [Fact]
    public void Indicator_DefaultParams()
    {
        var indicator = new RrsiIndicator();
        Assert.Equal(10, indicator.SmoothLength);
        Assert.Equal(10, indicator.RsiLength);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("RRSI", indicator.Name, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void Indicator_CustomParams()
    {
        var indicator = new RrsiIndicator { SmoothLength = 8, RsiLength = 14 };
        Assert.Equal(8, indicator.SmoothLength);
        Assert.Equal(14, indicator.RsiLength);
    }

    [Fact]
    public void Indicator_ShortName_Format()
    {
        var indicator = new RrsiIndicator { SmoothLength = 8, RsiLength = 14 };
        Assert.Equal("RRSI (8,14)", indicator.ShortName);
    }

    [Fact]
    public void Indicator_SourceCodeLink_Valid()
    {
        var indicator = new RrsiIndicator();
        Assert.Contains("Rrsi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void Indicator_HasLineSeries()
    {
        var indicator = new RrsiIndicator();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Indicator_ImplementsIWatchlist()
    {
        var indicator = new RrsiIndicator();
        Assert.IsAssignableFrom<IWatchlistIndicator>(indicator);
    }

    [Fact]
    public void Indicator_MinHistoryDepths_IsZero()
    {
        Assert.Equal(0, RrsiIndicator.MinHistoryDepths);
    }
}
