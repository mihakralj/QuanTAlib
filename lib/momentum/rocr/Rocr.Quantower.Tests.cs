using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class RocrIndicatorTests
{
    [Fact]
    public void Constructor_InitializesDefaults()
    {
        var indicator = new RocrIndicator();
        Assert.Equal(9, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ROCR - Rate of Change Ratio", indicator.Name);
    }

    [Fact]
    public void ShortName_ReflectsPeriod()
    {
        var indicator = new RocrIndicator { Period = 14 };
        Assert.Equal("ROCR(14)", indicator.ShortName);
    }

    [Fact]
    public void MinHistoryDepths_IsPeriodPlusOne()
    {
        var indicator = new RocrIndicator { Period = 9 };
        Assert.Equal(10, indicator.MinHistoryDepths);
    }

    [Fact]
    public void Period_CanBeSet()
    {
        var indicator = new RocrIndicator { Period = 20 };
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void Source_CanBeSet()
    {
        var indicator = new RocrIndicator { Source = SourceType.Open };
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void ShowColdValues_CanBeSet()
    {
        var indicator = new RocrIndicator { ShowColdValues = false };
        Assert.False(indicator.ShowColdValues);
    }
}
