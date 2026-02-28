using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class RocpIndicatorTests
{
    [Fact]
    public void Constructor_InitializesDefaults()
    {
        var indicator = new RocpIndicator();
        Assert.Equal(9, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ROCP - Rate of Change Percentage", indicator.Name);
        Assert.Contains("100 × (current - past) / past", indicator.Description, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void ShortName_ReflectsPeriod()
    {
        var indicator = new RocpIndicator { Period = 14 };
        Assert.Equal("ROCP(14)", indicator.ShortName);
    }

    [Fact]
    public void MinHistoryDepths_IsPeriodPlusOne()
    {
        var indicator = new RocpIndicator { Period = 9 };
        Assert.Equal(10, indicator.MinHistoryDepths);
    }

    [Fact]
    public void MinHistoryDepths_MatchesWatchlistInterface()
    {
        var indicator = new RocpIndicator { Period = 17 };
        Assert.Equal(18, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void Period_CanBeSet()
    {
        var indicator = new RocpIndicator { Period = 20 };
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void Source_CanBeSet()
    {
        var indicator = new RocpIndicator { Source = SourceType.Open };
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void ShowColdValues_CanBeSet()
    {
        var indicator = new RocpIndicator { ShowColdValues = false };
        Assert.False(indicator.ShowColdValues);
    }
}
