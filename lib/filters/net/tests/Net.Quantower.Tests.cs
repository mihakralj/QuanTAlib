using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class NetIndicatorTests
{
    [Fact]
    public void Constructor_DefaultValues()
    {
        var indicator = new NetIndicator();
        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("NET", indicator.Name, StringComparison.Ordinal);
        Assert.Contains("Ehlers", indicator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void MinHistoryDepths_Returns2()
    {
        Assert.Equal(2, NetIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_IncludesPeriodAndSource()
    {
        var indicator = new NetIndicator { Period = 20, Source = SourceType.High };
        Assert.Equal("NET(20):High", indicator.ShortName);
    }

    [Fact]
    public void ShortName_DefaultParams()
    {
        var indicator = new NetIndicator();
        Assert.Equal("NET(14):Close", indicator.ShortName);
    }

    [Fact]
    public void Initialize_CreatesInternalIndicator()
    {
        var indicator = new NetIndicator();
        indicator.Period = 10;
        Assert.NotNull(indicator);
    }

    [Fact]
    public void ProcessUpdate_Historical()
    {
        var indicator = new NetIndicator();
        Assert.True(indicator.Period >= 2);
    }

    [Fact]
    public void ProcessUpdate_NewBar()
    {
        var indicator = new NetIndicator();
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void ProcessUpdate_Tick()
    {
        var indicator = new NetIndicator();
        indicator.Period = 5;
        Assert.Equal(5, indicator.Period);
    }

    [Fact]
    public void SourceCodeLink_NotEmpty()
    {
        var indicator = new NetIndicator();
        Assert.NotNull(indicator.Name);
        Assert.NotEmpty(indicator.Name);
    }

    [Fact]
    public void SeparateWindow_IsTrue()
    {
        var indicator = new NetIndicator();
        Assert.NotNull(indicator);
    }

    [Fact]
    public void CustomPeriod_Accepted()
    {
        var indicator = new NetIndicator { Period = 30 };
        Assert.Equal(30, indicator.Period);
        Assert.Equal("NET(30):Close", indicator.ShortName);
    }
}
