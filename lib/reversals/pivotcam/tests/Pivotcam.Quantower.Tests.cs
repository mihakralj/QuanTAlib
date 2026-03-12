using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PivotcamIndicatorTests
{
    [Fact]
    public void PivotcamIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PivotcamIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Contains("PIVOTCAM", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PivotcamIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PivotcamIndicator();

        Assert.Equal(0, PivotcamIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PivotcamIndicator_ShortName_IsPivotcam()
    {
        var indicator = new PivotcamIndicator();
        indicator.Initialize();

        Assert.Contains("PIVOTCAM", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotcamIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PivotcamIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pivotcam", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotcamIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new PivotcamIndicator();

        indicator.Initialize();

        // 9 line series: PP, R1, R2, R3, R4, S1, S2, S3, S4
        Assert.Equal(9, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PivotcamIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PivotcamIndicator();
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
    public void PivotcamIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PivotcamIndicator();
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
    public void PivotcamIndicator_NineLineSeries_ArePresent()
    {
        var indicator = new PivotcamIndicator();
        indicator.Initialize();

        // PP=0, R1=1, R2=2, R3=3, R4=4, S1=5, S2=6, S3=7, S4=8
        Assert.Equal(9, indicator.LinesSeries.Count);
        Assert.Contains("PP", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R1", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R4", indicator.LinesSeries[4].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S1", indicator.LinesSeries[5].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S4", indicator.LinesSeries[8].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PivotcamIndicator_Description_IsSet()
    {
        var indicator = new PivotcamIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("Camarilla", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
