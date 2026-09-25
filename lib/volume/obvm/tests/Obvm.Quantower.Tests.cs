using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ObvmIndicatorTests
{
    [Fact]
    public void Constructor_DefaultValues()
    {
        var indicator = new ObvmIndicator();
        Assert.Equal(7, indicator.ObvmLength);
        Assert.Equal(10, indicator.SignalLength);
    }

    [Fact]
    public void ShortName_ContainsParameters()
    {
        var indicator = new ObvmIndicator();
        Assert.Contains("OBVM", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("7", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void MinHistoryDepths_ReturnsZero()
    {
        Assert.Equal(0, ObvmIndicator.MinHistoryDepths);
    }

    [Fact]
    public void Constructor_SetsSeparateWindow()
    {
        var indicator = new ObvmIndicator();
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var indicator = new ObvmIndicator();
        Assert.Equal("OBVM", indicator.Name);
    }

    [Fact]
    public void SourceCodeLink_IsValid()
    {
        var indicator = new ObvmIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("obvm", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DifferentObvmLength_UpdatesShortName()
    {
        var indicator = new ObvmIndicator { ObvmLength = 14 };
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentSignalLength_UpdatesShortName()
    {
        var indicator = new ObvmIndicator { SignalLength = 21 };
        Assert.Contains("21", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowColdValues_DefaultTrue()
    {
        var indicator = new ObvmIndicator();
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        var indicator = new ObvmIndicator();
        Assert.False(string.IsNullOrEmpty(indicator.Description));
    }
}
