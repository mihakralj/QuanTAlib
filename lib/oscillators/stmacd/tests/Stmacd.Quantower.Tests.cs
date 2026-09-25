using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class StmacdIndicatorTests
{
    [Fact]
    public void Indicator_HasCorrectName()
    {
        var ind = new StmacdIndicator();
        Assert.Equal("STMACD", ind.Name);
    }

    [Fact]
    public void Indicator_HasCorrectDescription()
    {
        var ind = new StmacdIndicator();
        Assert.Equal("Stochastic MACD Oscillator (Apirine)", ind.Description);
    }

    [Fact]
    public void Indicator_HasCorrectShortName()
    {
        var ind = new StmacdIndicator();
        Assert.Equal("STMACD 45,12,26,9", ind.ShortName);
    }

    [Fact]
    public void Indicator_SeparateWindow_IsTrue()
    {
        var ind = new StmacdIndicator();
        Assert.True(ind.SeparateWindow);
    }

    [Fact]
    public void Indicator_DefaultPeriods()
    {
        var ind = new StmacdIndicator();
        Assert.Equal(45, ind.Periods);
    }

    [Fact]
    public void Indicator_DefaultFastLength()
    {
        var ind = new StmacdIndicator();
        Assert.Equal(12, ind.FastLength);
    }

    [Fact]
    public void Indicator_DefaultSlowLength()
    {
        var ind = new StmacdIndicator();
        Assert.Equal(26, ind.SlowLength);
    }

    [Fact]
    public void Indicator_DefaultSignalLength()
    {
        var ind = new StmacdIndicator();
        Assert.Equal(9, ind.SignalLength);
    }

    [Fact]
    public void Indicator_ShowColdValues_DefaultTrue()
    {
        var ind = new StmacdIndicator();
        Assert.True(ind.ShowColdValues);
    }

    [Fact]
    public void Indicator_HasSourceCodeLink()
    {
        var ind = new StmacdIndicator();
        Assert.Contains("Stmacd.cs", ind.SourceCodeLink, StringComparison.Ordinal);
    }
}
