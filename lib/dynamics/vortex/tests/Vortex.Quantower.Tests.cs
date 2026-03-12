using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class VortexIndicatorTests
{
    [Fact]
    public void VortexIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VortexIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Vortex", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void VortexIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new VortexIndicator { Period = 20 };

        Assert.Equal(0, VortexIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void VortexIndicator_ShortName_IncludesParameters()
    {
        var indicator = new VortexIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("Vortex", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void VortexIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new VortexIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Vortex.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void VortexIndicator_Initialize_CreatesInternalVortex()
    {
        var indicator = new VortexIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (VI+, VI-)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void VortexIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VortexIndicator { Period = 5 };
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
        double viPlus = indicator.LinesSeries[0].GetValue(0);
        double viMinus = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(viPlus));
        Assert.True(double.IsFinite(viMinus));
    }
}
