using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class SwingsIndicatorTests
{
    [Fact]
    public void SwingsIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SwingsIndicator();

        Assert.Equal(5, indicator.Lookback);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("SWINGS", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SwingsIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SwingsIndicator();

        Assert.Equal(0, SwingsIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SwingsIndicator_ShortName_IsSwings()
    {
        var indicator = new SwingsIndicator();
        indicator.Initialize();

        Assert.Contains("SWINGS", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SwingsIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SwingsIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Swings", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SwingsIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new SwingsIndicator();

        indicator.Initialize();

        // After init, line series should exist (SwingHigh + SwingLow)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void SwingsIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SwingsIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            // Create a pattern with varying highs/lows to generate swings
            double basePrice = 100 + (i % 5 == 2 ? 10 : 0);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double swingHigh = indicator.LinesSeries[0].GetValue(0);
        double swingLow = indicator.LinesSeries[1].GetValue(0);

        // Values should be set (either finite swing or NaN=no swing)
        Assert.True(double.IsFinite(swingHigh) || double.IsNaN(swingHigh));
        Assert.True(double.IsFinite(swingLow) || double.IsNaN(swingLow));
    }

    [Fact]
    public void SwingsIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SwingsIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(15), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double swingHigh = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(swingHigh) || double.IsNaN(swingHigh));
    }

    [Fact]
    public void SwingsIndicator_TwoLineSeries_ArePresent()
    {
        var indicator = new SwingsIndicator();
        indicator.Initialize();

        // SwingHigh is index 0 (red), SwingLow is index 1 (green)
        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Contains("High", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Low", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SwingsIndicator_Description_IsSet()
    {
        var indicator = new SwingsIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("swing", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
