using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class Ssf3IndicatorTests
{
    [Fact]
    public void Ssf3Indicator_Constructor_SetsDefaults()
    {
        var indicator = new Ssf3Indicator();

        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SSF3 - Ehlers 3-Pole Super Smoother Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void Ssf3Indicator_MinHistoryDepths_ReturnsZero()
    {
        var indicator = new Ssf3Indicator { Period = 20 };

        Assert.Equal(0, Ssf3Indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void Ssf3Indicator_ShortName_IncludesParameters()
    {
        var indicator = new Ssf3Indicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("SSF3", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Ssf3Indicator_SourceCodeLink_IsValid()
    {
        var indicator = new Ssf3Indicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ssf3.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void Ssf3Indicator_Initialize_CreatesInternalSsf3()
    {
        var indicator = new Ssf3Indicator { Period = 20 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Ssf3Indicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new Ssf3Indicator { Period = 5 };
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
        double ssf = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(ssf));
    }
}
