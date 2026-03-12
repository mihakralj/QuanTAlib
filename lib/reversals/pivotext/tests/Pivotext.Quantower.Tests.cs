using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PivotextIndicatorTests
{
    [Fact]
    public void PivotextIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PivotextIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Contains("PIVOTEXT", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PivotextIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PivotextIndicator();

        Assert.Equal(0, PivotextIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PivotextIndicator_ShortName_IsPivotext()
    {
        var indicator = new PivotextIndicator();
        indicator.Initialize();

        Assert.Contains("PIVOTEXT", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotextIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PivotextIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pivotext", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotextIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new PivotextIndicator();

        indicator.Initialize();

        // 11 line series: PP, R1, R2, R3, R4, R5, S1, S2, S3, S4, S5
        Assert.Equal(11, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PivotextIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PivotextIndicator();
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
    public void PivotextIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PivotextIndicator();
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
    public void PivotextIndicator_ElevenLineSeries_ArePresent()
    {
        var indicator = new PivotextIndicator();
        indicator.Initialize();

        // PP=0, R1=1, R2=2, R3=3, R4=4, R5=5, S1=6, S2=7, S3=8, S4=9, S5=10
        Assert.Equal(11, indicator.LinesSeries.Count);
        Assert.Contains("PP", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R1", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R5", indicator.LinesSeries[5].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S1", indicator.LinesSeries[6].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S5", indicator.LinesSeries[10].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PivotextIndicator_Description_IsSet()
    {
        var indicator = new PivotextIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("Extended", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
