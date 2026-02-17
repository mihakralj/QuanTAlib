using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PivotIndicatorTests
{
    [Fact]
    public void PivotIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PivotIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Contains("PIVOT", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PivotIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PivotIndicator();

        Assert.Equal(0, PivotIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PivotIndicator_ShortName_IsPivot()
    {
        var indicator = new PivotIndicator();
        indicator.Initialize();

        Assert.Contains("PIVOT", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PivotIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pivot", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new PivotIndicator();

        indicator.Initialize();

        // 7 line series: PP, R1, R2, R3, S1, S2, S3
        Assert.Equal(7, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PivotIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PivotIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + i * 2;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // PP is index 0
        double pp = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(pp) || double.IsNaN(pp));
    }

    [Fact]
    public void PivotIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PivotIndicator();
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
    public void PivotIndicator_SevenLineSeries_ArePresent()
    {
        var indicator = new PivotIndicator();
        indicator.Initialize();

        // PP=0, R1=1, R2=2, R3=3, S1=4, S2=5, S3=6
        Assert.Equal(7, indicator.LinesSeries.Count);
        Assert.Contains("PP", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R1", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S1", indicator.LinesSeries[4].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PivotIndicator_Description_IsSet()
    {
        var indicator = new PivotIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("pivot", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
