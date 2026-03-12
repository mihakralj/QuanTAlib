using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class Butter3IndicatorTests
{
    [Fact]
    public void Butter3Indicator_Constructor_SetsDefaults()
    {
        var indicator = new Butter3Indicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BUTTER3 - Ehlers 3-Pole Butterworth Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void Butter3Indicator_MinHistoryDepths_ReturnsZero()
    {
        var indicator = new Butter3Indicator { Period = 20 };

        Assert.Equal(0, Butter3Indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void Butter3Indicator_ShortName_IncludesParameters()
    {
        var indicator = new Butter3Indicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("BUTTER3", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Butter3Indicator_SourceCodeLink_IsValid()
    {
        var indicator = new Butter3Indicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Butter3.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void Butter3Indicator_Initialize_CreatesInternalButter3()
    {
        var indicator = new Butter3Indicator { Period = 20 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Butter3Indicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new Butter3Indicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double butter = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(butter));
    }
}
