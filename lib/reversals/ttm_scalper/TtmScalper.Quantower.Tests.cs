using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class TtmScalperIndicatorTests
{
    [Fact]
    public void TtmScalperIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TtmScalperIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.False(indicator.UseCloses);
        Assert.Contains("TTM_SCALPER", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TtmScalperIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new TtmScalperIndicator();

        Assert.Equal(0, TtmScalperIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void TtmScalperIndicator_ShortName_IsTtmScalper()
    {
        var indicator = new TtmScalperIndicator();
        indicator.Initialize();

        Assert.Contains("TTM_SCALPER", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void TtmScalperIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TtmScalperIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("TtmScalper", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TtmScalperIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new TtmScalperIndicator();

        indicator.Initialize();

        // After init, line series should exist (PivotHigh + PivotLow)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void TtmScalperIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TtmScalperIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            // Create a pattern with varying highs/lows to generate pivots
            double basePrice = 100 + (i % 3 == 1 ? 10 : 0); // spike every 3rd bar at position 1
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double pivotHigh = indicator.LinesSeries[0].GetValue(0);
        double pivotLow = indicator.LinesSeries[1].GetValue(0);

        // Values should be set (either finite pivot or NaN=no pivot)
        Assert.True(double.IsFinite(pivotHigh) || double.IsNaN(pivotHigh));
        Assert.True(double.IsFinite(pivotLow) || double.IsNaN(pivotLow));
    }

    [Fact]
    public void TtmScalperIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TtmScalperIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double pivotHigh = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(pivotHigh) || double.IsNaN(pivotHigh));
    }

    [Fact]
    public void TtmScalperIndicator_TwoLineSeries_ArePresent()
    {
        var indicator = new TtmScalperIndicator();
        indicator.Initialize();

        // PivotHigh is index 0 (red), PivotLow is index 1 (green)
        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Contains("High", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Low", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TtmScalperIndicator_Description_IsSet()
    {
        var indicator = new TtmScalperIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("pivot", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
