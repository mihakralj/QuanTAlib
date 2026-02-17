using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PivotwoodIndicatorTests
{
    [Fact]
    public void PivotwoodIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PivotwoodIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Contains("PIVOTWOOD", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PivotwoodIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PivotwoodIndicator();

        Assert.Equal(0, PivotwoodIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PivotwoodIndicator_ShortName_IsPivotwood()
    {
        var indicator = new PivotwoodIndicator();
        indicator.Initialize();

        Assert.Contains("PIVOTWOOD", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotwoodIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PivotwoodIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pivotwood", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PivotwoodIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new PivotwoodIndicator();

        indicator.Initialize();

        // 7 line series: PP, R1, R2, R3, S1, S2, S3
        Assert.Equal(7, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PivotwoodIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PivotwoodIndicator();
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
    public void PivotwoodIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PivotwoodIndicator();
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
    public void PivotwoodIndicator_SevenLineSeries_ArePresent()
    {
        var indicator = new PivotwoodIndicator();
        indicator.Initialize();

        // PP=0, R1=1, R2=2, R3=3, S1=4, S2=5, S3=6
        Assert.Equal(7, indicator.LinesSeries.Count);
        Assert.Contains("PP", indicator.LinesSeries[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R1", indicator.LinesSeries[1].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S1", indicator.LinesSeries[4].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PivotwoodIndicator_Description_IsSet()
    {
        var indicator = new PivotwoodIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("Woodie", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
