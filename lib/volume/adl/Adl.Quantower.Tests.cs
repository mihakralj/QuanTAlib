using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AdlIndicatorTests
{
    [Fact]
    public void AdlIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AdlIndicator();

        Assert.Equal("ADL - Accumulation/Distribution Line", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(0, AdlIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AdlIndicator_ShortName_IsCorrect()
    {
        var indicator = new AdlIndicator();
        Assert.Equal("ADL", indicator.ShortName);
    }

    [Fact]
    public void AdlIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AdlIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Adl.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void AdlIndicator_Initialize_CreatesInternalAdl()
    {
        var indicator = new AdlIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AdlIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AdlIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void AdlIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AdlIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }
}
