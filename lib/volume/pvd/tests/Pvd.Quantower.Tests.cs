using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PvdIndicatorTests
{
    [Fact]
    public void PvdIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PvdIndicator();

        Assert.Equal("PVD - Price Volume Divergence", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(14, indicator.PricePeriod);
        Assert.Equal(14, indicator.VolumePeriod);
        Assert.Equal(3, indicator.SmoothingPeriod);
    }

    [Fact]
    public void PvdIndicator_ShortName_IsConstant()
    {
        var indicator = new PvdIndicator();
        Assert.Equal("PVD", indicator.ShortName);
    }

    [Fact]
    public void PvdIndicator_MinHistoryDepths_CalculatedCorrectly()
    {
        var indicator = new PvdIndicator
        {
            PricePeriod = 10,
            VolumePeriod = 20,
            SmoothingPeriod = 5
        };

        // max(10,20) + 5 + 1 = 26
        Assert.Equal(26, indicator.MinHistoryDepths);
        Assert.Equal(26, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PvdIndicator_MinHistoryDepths_DefaultValue()
    {
        var indicator = new PvdIndicator();

        // max(14,14) + 3 + 1 = 18
        Assert.Equal(18, indicator.MinHistoryDepths);
    }

    [Fact]
    public void PvdIndicator_Initialize_CreatesInternalPvd()
    {
        var indicator = new PvdIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PvdIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PvdIndicator
        {
            PricePeriod = 5,
            VolumePeriod = 5,
            SmoothingPeriod = 2
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double close = 100 + (i * 0.5);
            double volume = 100000 + (i % 3 == 0 ? 20000 : -10000);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 1, close + 1, close - 2, close, volume);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void PvdIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PvdIndicator
        {
            PricePeriod = 3,
            VolumePeriod = 3,
            SmoothingPeriod = 2
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 105, 115, 100, 112, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PvdIndicator_PositiveDivergence_PriceUpVolumeDown()
    {
        var indicator = new PvdIndicator
        {
            PricePeriod = 2,
            VolumePeriod = 2,
            SmoothingPeriod = 1
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Establish baseline with stable prices and volumes
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Price up, volume down = positive divergence
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 108, 112, 105, 110, 70000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"PVD should be positive when price up and volume down: {val}");
    }

    [Fact]
    public void PvdIndicator_NegativeDivergence_PriceUpVolumeUp()
    {
        var indicator = new PvdIndicator
        {
            PricePeriod = 2,
            VolumePeriod = 2,
            SmoothingPeriod = 1
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Establish baseline
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100, 105, 95, 100, 100000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Price up, volume up = negative (same direction, no divergence)
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 108, 112, 105, 110, 130000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"PVD should be negative when price and volume move same direction: {val}");
    }

    [Fact]
    public void PvdIndicator_NoDivergence_StablePriceAndVolume()
    {
        var indicator = new PvdIndicator
        {
            PricePeriod = 2,
            VolumePeriod = 2,
            SmoothingPeriod = 1
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // All bars with same values - no momentum
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 100000);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, val, precision: 5);
    }

    [Fact]
    public void PvdIndicator_CustomPeriods_Applied()
    {
        var indicator = new PvdIndicator
        {
            PricePeriod = 5,
            VolumePeriod = 10,
            SmoothingPeriod = 3
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 1, close + 2, close - 2, close, 100000 + (i * 1000));
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}
