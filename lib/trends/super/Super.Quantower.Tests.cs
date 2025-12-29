using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class SuperIndicatorTests
{
    [Fact]
    public void SuperIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SuperIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(3.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SuperTrend", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SuperIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SuperIndicator { Period = 20 };

        Assert.Equal(0, SuperIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SuperIndicator_ShortName_IncludesParameters()
    {
        var indicator = new SuperIndicator { Period = 20, Multiplier = 2.5 };
        indicator.Initialize();

        Assert.Contains("Super", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SuperIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Super.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperIndicator_Initialize_CreatesInternalSuper()
    {
        var indicator = new SuperIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (SuperTrend, Upper, Lower)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void SuperIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SuperIndicator { Period = 5 };
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

        // Line series should have a value (either Up or Down)
        // One should be NaN, other should be value, or both NaN if cold
        double up = indicator.LinesSeries[0].GetValue(0);
        double down = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(up) || double.IsFinite(down));
    }

    [Fact]
    public void SuperIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SuperIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SuperIndicator_Parameters_CanBeChanged()
    {
        var indicator = new SuperIndicator { Period = 14 };
        Assert.Equal(14, indicator.Period);

        indicator.Period = 20;
        indicator.Multiplier = 4.0;

        Assert.Equal(20, indicator.Period);
        Assert.Equal(4.0, indicator.Multiplier);
        Assert.Equal(0, SuperIndicator.MinHistoryDepths);
    }
}
