using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ChopIndicatorTests
{
    [Fact]
    public void ChopIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ChopIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Choppiness Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ChopIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ChopIndicator { Period = 20 };

        Assert.Equal(0, ChopIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ChopIndicator_ShortName_IncludesParameters()
    {
        var indicator = new ChopIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("CHOP", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ChopIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ChopIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Chop.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ChopIndicator_Initialize_CreatesInternalChop()
    {
        var indicator = new ChopIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (single CHOP line)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ChopIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ChopIndicator { Period = 5 };
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
        double chop = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(chop));
        Assert.InRange(chop, 0.0, 100.0);
    }
}
