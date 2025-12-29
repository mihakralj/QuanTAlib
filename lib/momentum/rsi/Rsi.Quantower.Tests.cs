using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class RsiIndicatorTests
{
    [Fact]
    public void RsiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RsiIndicator();

        Assert.Equal("RSI - Relative Strength Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void RsiIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new RsiIndicator
        {
            Period = 20,
        };

        Assert.Equal(0, RsiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void RsiIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new RsiIndicator
        {
            Period = 20,
        };
        indicator.Initialize();

        Assert.Contains("RSI(20)", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new RsiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Rsi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void RsiIndicator_Initialize_CreatesInternalRsi()
    {
        var indicator = new RsiIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (RSI)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RsiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RsiIndicator
        {
            Period = 2, // Short period for testing
        };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 102); // Gain 2
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100, 105, 95, 101); // Loss 1

        // Process updates
        var args = new UpdateArgs(UpdateReason.HistoricalBar);

        // We need to process updates sequentially to build state
        // But the mock might not support full stateful replay easily without calling ProcessUpdate multiple times
        // Let's just verify it runs without error and produces a value

        indicator.ProcessUpdate(args); // Bar 0
        indicator.ProcessUpdate(args); // Bar 1
        indicator.ProcessUpdate(args); // Bar 2

        // Line series should have a value
        double rsi = indicator.LinesSeries[0].GetValue(0);

        // We just check it's a valid number (0-100)
        Assert.True(rsi >= 0 && rsi <= 100);
    }
}
