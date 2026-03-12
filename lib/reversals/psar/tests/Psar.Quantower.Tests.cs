using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class PsarIndicatorTests
{
    [Fact]
    public void PsarIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PsarIndicator();

        Assert.Equal(0.02, indicator.AfStart);
        Assert.Equal(0.02, indicator.AfIncrement);
        Assert.Equal(0.20, indicator.AfMax);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("PSAR", indicator.Name, StringComparison.Ordinal);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PsarIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PsarIndicator();

        Assert.Equal(0, PsarIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void PsarIndicator_ShortName_IncludesParameters()
    {
        var indicator = new PsarIndicator { AfStart = 0.02, AfMax = 0.20 };
        indicator.Initialize();

        Assert.Contains("PSAR", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.02", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PsarIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PsarIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Psar", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PsarIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new PsarIndicator();

        indicator.Initialize();

        // After init, line series should exist (SAR only)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PsarIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PsarIndicator { AfStart = 0.02, AfIncrement = 0.02, AfMax = 0.20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double sar = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(sar));
    }

    [Fact]
    public void PsarIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PsarIndicator { AfStart = 0.02, AfIncrement = 0.02, AfMax = 0.20 };
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

        double sar = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(sar));
    }

    [Fact]
    public void PsarIndicator_SingleLineSeries_IsPresent()
    {
        var indicator = new PsarIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        Assert.Single(indicator.LinesSeries);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void PsarIndicator_Description_IsSet()
    {
        var indicator = new PsarIndicator();

        Assert.NotNull(indicator.Description);
        Assert.NotEmpty(indicator.Description);
        Assert.Contains("stop", indicator.Description, StringComparison.OrdinalIgnoreCase);
    }
}
