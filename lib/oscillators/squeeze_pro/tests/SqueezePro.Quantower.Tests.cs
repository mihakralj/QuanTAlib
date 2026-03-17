using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class SqueezeProIndicatorTests
{
    [Fact]
    public void Indicator_Can_Be_Constructed()
    {
        var indicator = new SqueezeProIndicator();
        Assert.NotNull(indicator);
        Assert.Equal("SQUEEZE_PRO", indicator.Name);
    }

    [Fact]
    public void Indicator_Default_Period()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void Indicator_Default_BbMult()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Equal(2.0, indicator.BbMult);
    }

    [Fact]
    public void Indicator_Default_KcMultWide()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Equal(2.0, indicator.KcMultWide);
    }

    [Fact]
    public void Indicator_Default_KcMultNormal()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Equal(1.5, indicator.KcMultNormal);
    }

    [Fact]
    public void Indicator_Default_KcMultNarrow()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Equal(1.0, indicator.KcMultNarrow);
    }

    [Fact]
    public void Indicator_Default_MomLength()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Equal(12, indicator.MomLength);
    }

    [Fact]
    public void Indicator_Default_MomSmooth()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Equal(6, indicator.MomSmooth);
    }

    [Fact]
    public void Indicator_Default_UseSma()
    {
        var indicator = new SqueezeProIndicator();
        Assert.True(indicator.UseSma);
    }

    [Fact]
    public void Indicator_ShortName_Format()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Contains("SQZ_PRO", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Indicator_Properties_Can_Be_Set()
    {
        var indicator = new SqueezeProIndicator
        {
            Period = 30,
            BbMult = 2.5,
            KcMultWide = 3.0,
            KcMultNormal = 2.0,
            KcMultNarrow = 1.5,
            MomLength = 15,
            MomSmooth = 8,
            UseSma = false
        };

        Assert.Equal(30, indicator.Period);
        Assert.Equal(2.5, indicator.BbMult);
        Assert.Equal(3.0, indicator.KcMultWide);
        Assert.Equal(2.0, indicator.KcMultNormal);
        Assert.Equal(1.5, indicator.KcMultNarrow);
        Assert.Equal(15, indicator.MomLength);
        Assert.Equal(8, indicator.MomSmooth);
        Assert.False(indicator.UseSma);
    }

    [Fact]
    public void Indicator_SourceCodeLink_Valid()
    {
        var indicator = new SqueezeProIndicator();
        Assert.Contains("SqueezePro.cs", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Indicator_ShowColdValues_Default()
    {
        var indicator = new SqueezeProIndicator();
        Assert.True(indicator.ShowColdValues);
    }
}
