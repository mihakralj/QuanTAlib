using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PvrIndicatorTests
{
    [Fact]
    public void PvrIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PvrIndicator();

        Assert.Equal("PVR - Price Volume Rank", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void PvrIndicator_ShortName_ReturnsPVR()
    {
        var indicator = new PvrIndicator();
        Assert.Equal("PVR", indicator.ShortName);
    }

    [Fact]
    public void PvrIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new PvrIndicator();

        Assert.Equal(1, indicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PvrIndicator_Initialize_CreatesInternalPvr()
    {
        var indicator = new PvrIndicator();

        // Initialize should not throw
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PvrIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PvrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val >= 0 && val <= 4);
    }

    [Fact]
    public void PvrIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PvrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(5), 110, 120, 100, 115, 1800);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PvrIndicator_Value_IsInValidRange()
    {
        var indicator = new PvrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + (i % 5), 110 + (i % 5), 90 + (i % 5), 105 + (i % 5), 1000 + (i * 50));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val >= 0 && val <= 4, $"PVR value {val} should be in range [0,4]");
    }

    [Fact]
    public void PvrIndicator_PriceUpVolumeUp_ReturnsOne()
    {
        var indicator = new PvrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar - price up, volume up
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 107, 97, 105, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(1.0, val);
    }

    [Fact]
    public void PvrIndicator_PriceDownVolumeUp_ReturnsFour()
    {
        var indicator = new PvrIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar - price down, volume up
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 98, 103, 93, 95, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(4.0, val);
    }
}