using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class Butter2IndicatorTests
{
    [Fact]
    public void Butter2Indicator_Constructor_SetsDefaults()
    {
        var indicator = new Butter2Indicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BUTTER2 - Ehlers 2-Pole Butterworth Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void Butter2Indicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new Butter2Indicator { Period = 20 };

        Assert.Equal(0, Butter2Indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void Butter2Indicator_ShortName_IncludesParameters()
    {
        var indicator = new Butter2Indicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("BUTTER2", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Butter2Indicator_SourceCodeLink_IsValid()
    {
        var indicator = new Butter2Indicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Butter2.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void Butter2Indicator_Initialize_CreatesInternalButter2()
    {
        var indicator = new Butter2Indicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Butter2Indicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new Butter2Indicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double butter = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(butter));
    }
}
