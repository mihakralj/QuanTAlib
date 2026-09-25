using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class HhllsIndicatorTests
{
    [Fact]
    public void Constructor_DefaultPeriod()
    {
        var indicator = new HhllsIndicator();
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void ShortName_IncludesPeriod()
    {
        var indicator = new HhllsIndicator { Period = 15 };
        Assert.Equal("HHLLS 15", indicator.ShortName);
    }

    [Fact]
    public void SourceCodeLink_IsValid()
    {
        var indicator = new HhllsIndicator();
        Assert.Contains("Hhlls.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MinHistoryDepths_IsZero()
    {
        Assert.Equal(0, HhllsIndicator.MinHistoryDepths);
    }

    [Fact]
    public void Initialize_CreatesIndicator()
    {
        var indicator = new HhllsIndicator { Period = 10 };
        Assert.NotNull(indicator);
    }

    [Fact]
    public void PeriodChange_UpdatesShortName()
    {
        var indicator = new HhllsIndicator { Period = 30 };
        Assert.Equal("HHLLS 30", indicator.ShortName);
    }

    [Fact]
    public void SeparateWindow_IsTrue()
    {
        var indicator = new HhllsIndicator();
        Assert.True(indicator.SeparateWindow);
    }

    [Fact]
    public void Name_IsHHLLS()
    {
        var indicator = new HhllsIndicator();
        Assert.Equal("HHLLS", indicator.Name);
    }

    [Fact]
    public void Description_ContainsApirine()
    {
        var indicator = new HhllsIndicator();
        Assert.Contains("Apirine", indicator.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowColdValues_DefaultTrue()
    {
        var indicator = new HhllsIndicator();
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void IWatchlistIndicator_MinHistoryDepths()
    {
        IWatchlistIndicator wl = new HhllsIndicator();
        Assert.Equal(0, wl.MinHistoryDepths);
    }
}
