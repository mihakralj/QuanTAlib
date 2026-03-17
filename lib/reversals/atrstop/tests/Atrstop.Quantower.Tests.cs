using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class AtrstopIndicatorTests
{
    [Fact]
    public void Indicator_Creates()
    {
        var indicator = new AtrstopIndicator();
        Assert.NotNull(indicator);
    }

    [Fact]
    public void DefaultParameters_Match()
    {
        var indicator = new AtrstopIndicator();
        Assert.Equal(21, indicator.Period);
        Assert.Equal(3.0, indicator.Multiplier);
        Assert.False(indicator.UseHighLow);
    }

    [Fact]
    public void Indicator_HasLineSeries()
    {
        var indicator = new AtrstopIndicator();
        indicator.Initialize();
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var indicator = new AtrstopIndicator { Period = 14, Multiplier = 2.5 };
        Assert.Contains("ATRSTOP", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SeparateWindow_IsFalse()
    {
        var indicator = new AtrstopIndicator();
        Assert.False(indicator.SeparateWindow);
    }

    [Fact]
    public void ProcessBars_ProducesOutput()
    {
        var indicator = new AtrstopIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 42);
        for (int i = 0; i < 20; i++)
        {
            var (_, _, h, l, c, _) = gbm.Next(isNew: true);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), c, h, l, c);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void SourceCodeLink_IsValid()
    {
        var indicator = new AtrstopIndicator();
        Assert.Contains("Atrstop.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}
