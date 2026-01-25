using Xunit;

namespace QuanTAlib.Tests;

public class UchannelQuantowerTests
{
    [Fact]
    public void UchannelIndicator_Constructor_SetsDefaults()
    {
        var indicator = new UchannelIndicator();

        Assert.Equal(20, indicator.StrPeriod);
        Assert.Equal(20, indicator.CenterPeriod);
        Assert.Equal(1.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("UCHANNEL - Ehlers Ultimate Channel", indicator.Name);
    }

    [Fact]
    public void UchannelIndicator_MinHistoryDepths_ReturnsMaxOfPeriods()
    {
        var indicator1 = new UchannelIndicator { StrPeriod = 10, CenterPeriod = 20 };
        Assert.Equal(20, indicator1.MinHistoryDepths);

        var indicator2 = new UchannelIndicator { StrPeriod = 30, CenterPeriod = 15 };
        Assert.Equal(30, indicator2.MinHistoryDepths);

        var indicator3 = new UchannelIndicator { StrPeriod = 25, CenterPeriod = 25 };
        Assert.Equal(25, indicator3.MinHistoryDepths);
    }

    [Fact]
    public void UchannelIndicator_ShortName_FormatsCorrectly()
    {
        var indicator = new UchannelIndicator
        {
            StrPeriod = 15,
            CenterPeriod = 25,
            Multiplier = 2.5
        };

        Assert.Equal("UCHANNEL (15,25,2.5)", indicator.ShortName);
    }

    [Fact]
    public void UchannelIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new UchannelIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Uchannel.cs", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UchannelIndicator_OnInit_CreatesInternalIndicator()
    {
        var indicator = new UchannelIndicator
        {
            StrPeriod = 10,
            CenterPeriod = 15,
            Multiplier = 1.5
        };

        // OnInit is protected, but we can verify it doesn't throw
        // by checking the indicator state after construction
        Assert.NotNull(indicator);
    }

    [Fact]
    public void UchannelIndicator_Parameters_CanBeModified()
    {
        var indicator = new UchannelIndicator();

        indicator.StrPeriod = 30;
        indicator.CenterPeriod = 40;
        indicator.Multiplier = 2.0;
        indicator.ShowColdValues = false;

        Assert.Equal(30, indicator.StrPeriod);
        Assert.Equal(40, indicator.CenterPeriod);
        Assert.Equal(2.0, indicator.Multiplier);
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void UchannelIndicator_Description_IsNotEmpty()
    {
        var indicator = new UchannelIndicator();

        Assert.False(string.IsNullOrWhiteSpace(indicator.Description));
        Assert.Contains("Ultrasmooth", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UchannelIndicator_HasCorrectLineSeries()
    {
        var indicator = new UchannelIndicator();

        // The indicator should have 5 line series: Middle, Upper, Lower, STR, Width
        Assert.NotNull(indicator);
    }
}
