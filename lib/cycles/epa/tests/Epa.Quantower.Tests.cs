using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Quantower.Tests;

public class EpaIndicatorTests
{
    [Fact]
    public void Constructor_DefaultParameters()
    {
        var indicator = new EpaIndicator();
        Assert.Equal(28, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void MinHistoryDepths_IsZero()
    {
        Assert.Equal(0, EpaIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_ContainsPeriod()
    {
        var indicator = new EpaIndicator { Period = 20 };
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_DoesNotThrow()
    {
        var indicator = new EpaIndicator();
        var ex = Record.Exception(() => indicator.Initialize());
        Assert.Null(ex);
    }

    [Fact]
    public void ProcessUpdate_Historical_DoesNotThrow()
    {
        var indicator = new EpaIndicator();
        indicator.Initialize();
        indicator.HistoricalData.AddBar(
            open: 100, high: 105, low: 95, close: 102, volume: 1000,
            time: DateTime.UtcNow);
        var ex = Record.Exception(() =>
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar)));
        Assert.Null(ex);
    }

    [Fact]
    public void ProcessUpdate_NewBar_DoesNotThrow()
    {
        var indicator = new EpaIndicator();
        indicator.Initialize();
        indicator.HistoricalData.AddBar(
            open: 100, high: 105, low: 95, close: 102, volume: 1000,
            time: DateTime.UtcNow);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(
            open: 102, high: 107, low: 97, close: 104, volume: 1100,
            time: DateTime.UtcNow.AddDays(1));
        var ex = Record.Exception(() =>
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar)));
        Assert.Null(ex);
    }

    [Fact]
    public void ProcessUpdate_Tick_DoesNotThrow()
    {
        var indicator = new EpaIndicator();
        indicator.Initialize();
        indicator.HistoricalData.AddBar(
            open: 100, high: 105, low: 95, close: 102, volume: 1000,
            time: DateTime.UtcNow);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        var ex = Record.Exception(() =>
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick)));
        Assert.Null(ex);
    }

    [Fact]
    public void SourceCodeLink_IsNotEmpty()
    {
        var indicator = new EpaIndicator();
        Assert.False(string.IsNullOrEmpty(indicator.SourceCodeLink));
    }

    [Fact]
    public void MultipleHistoricalBars_DoNotThrow()
    {
        var indicator = new EpaIndicator { Period = 10 };
        indicator.Initialize();

        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(
                open: 100 + i, high: 105 + i, low: 95 + i, close: 102 + i,
                volume: 1000 + i * 10,
                time: DateTime.UtcNow.AddDays(i));
            var ex = Record.Exception(() =>
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar)));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void CustomPeriod_InitializesCorrectly()
    {
        var indicator = new EpaIndicator { Period = 14 };
        indicator.Initialize();
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentSources_DoNotThrow()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new EpaIndicator { Source = sourceType };
            indicator.Initialize();
            indicator.HistoricalData.AddBar(
                open: 100, high: 105, low: 95, close: 102, volume: 1000,
                time: DateTime.UtcNow);
            var ex = Record.Exception(() =>
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar)));
            Assert.Null(ex);
        }
    }
}
