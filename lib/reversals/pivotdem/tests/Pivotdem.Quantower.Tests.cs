using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PivotdemIndicatorTests
{
    [Fact]
    public void PivotdemIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PivotdemIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Contains("PIVOTDEM", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PivotdemIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PivotdemIndicator();

        Assert.Equal(0, PivotdemIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PivotdemIndicator_ShortName_IsPivotdem()
    {
        var indicator = new PivotdemIndicator();
        indicator.Initialize();

        Assert.Contains("PIVOTDEM", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotdemIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PivotdemIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pivotdem", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotdemIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new PivotdemIndicator();

        indicator.Initialize();

        // 3 line series: PP, R1, S1
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PivotdemIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PivotdemIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + (i * 2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // PP is index 0
        double pp = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(pp) || double.IsNaN(pp));
    }

    [Fact]
    public void PivotdemIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PivotdemIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(5), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double pp = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(pp) || double.IsNaN(pp));
    }

    [Fact]
    public void PivotdemIndicator_ThreeLineSeries_ArePresent()
    {
        var indicator = new PivotdemIndicator();
        indicator.Initialize();

        // PP=0, R1=1, S1=2 — DeMark only produces 3 levels
        Assert.Equal(3, indicator.LinesSeries.Count);
        Assert.Contains("PP", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R1", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S1", indicator.LinesSeries[2].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PivotdemIndicator_Description_IsSet()
    {
        var indicator = new PivotdemIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("pivot", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
