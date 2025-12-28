using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AroonIndicatorTests
{
    [Fact]
    public void AroonIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AroonIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Aroon", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AroonIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AroonIndicator { Period = 20 };

        Assert.Equal(0, AroonIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AroonIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AroonIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("Aroon", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AroonIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AroonIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Aroon.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AroonIndicator_Initialize_CreatesInternalAroon()
    {
        var indicator = new AroonIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Up, Down, Osc)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AroonIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AroonIndicator { Period = 5 };
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
        double up = indicator.LinesSeries[0].GetValue(0);
        double down = indicator.LinesSeries[1].GetValue(0);
        double osc = indicator.LinesSeries[2].GetValue(0);

        Assert.True(double.IsFinite(up));
        Assert.True(double.IsFinite(down));
        Assert.True(double.IsFinite(osc));
    }
}
