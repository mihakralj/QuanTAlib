using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class NotchIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var indicator = new NotchIndicator();
        Assert.Equal(14, indicator.Period);
        Assert.Equal(1.0, indicator.Q);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Notch - Notch Filter", indicator.Name);
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var indicator = new NotchIndicator();
        indicator.Period = 20;
        indicator.Q = 0.5;
        // _sourceName is not set until OnInit, so ShortName relies on private field which might be null.
        // If undefined, it's just empty string at the end.
        Assert.StartsWith("Notch(20, 0.5):", indicator.ShortName, System.StringComparison.Ordinal);
    }
}
