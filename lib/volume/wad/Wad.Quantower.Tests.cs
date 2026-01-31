using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class WadIndicatorTests
{
    [Fact]
    public void WadIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WadIndicator();

        Assert.Equal("WAD - Williams Accumulation/Distribution", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(1, WadIndicator.MinHistoryDepths);
    }

    [Fact]
    public void WadIndicator_ShortName_IsCorrect()
    {
        var indicator = new WadIndicator();
        Assert.Equal("WAD", indicator.ShortName);
    }

    [Fact]
    public void WadIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new WadIndicator();

        Assert.Equal(1, WadIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void WadIndicator_Initialize_CreatesInternalWad()
    {
        var indicator = new WadIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void WadIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WadIndicator();
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
    public void WadIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new WadIndicator();
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