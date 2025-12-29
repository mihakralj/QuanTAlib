using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AroonOscIndicatorTests
{
    [Fact]
    public void AroonOscIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AroonOscIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Aroon Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AroonOscIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AroonOscIndicator { Period = 20 };

        Assert.Equal(0, AroonOscIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AroonOscIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AroonOscIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("AroonOsc", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AroonOscIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AroonOscIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("AroonOsc.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AroonOscIndicator_Initialize_CreatesInternalAroonOsc()
    {
        var indicator = new AroonOscIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Osc)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AroonOscIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AroonOscIndicator { Period = 5 };
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
        double osc = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(osc));
    }
}
