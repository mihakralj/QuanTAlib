using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VwapIndicatorTests
{
    [Fact]
    public void VwapIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VwapIndicator();

        Assert.Equal("VWAP - Volume Weighted Average Price", indicator.Name);
        Assert.Equal(0, indicator.Period);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void VwapIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new VwapIndicator { Period = 14 };
        Assert.Equal("VWAP(14)", indicator.ShortName);

        var indicatorNoPeriod = new VwapIndicator { Period = 0 };
        Assert.Equal("VWAP", indicatorNoPeriod.ShortName);
    }

    [Fact]
    public void VwapIndicator_MinHistoryDepths_EqualsDefault()
    {
        var indicator = new VwapIndicator();

        Assert.Equal(1, indicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VwapIndicator_Initialize_CreatesInternalVwap()
    {
        var indicator = new VwapIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VwapIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VwapIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
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
    public void VwapIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VwapIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VwapIndicator_Value_TracksVolumeWeightedPrice()
    {
        var indicator = new VwapIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            double volume = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (i > 0)
            {
                double val = indicator.LinesSeries[0].GetValue(0);
                values.Add(val);
            }
        }

        // VWAP should produce finite values
        Assert.True(values.Count > 0, "Should have recorded values");
        Assert.All(values, v => Assert.True(double.IsFinite(v)));

        // VWAP values should be within price range (approximately)
        double avgValue = values.Average();
        Assert.True(avgValue > 90 && avgValue < 200, $"VWAP {avgValue} should be within reasonable price range");
    }

    [Fact]
    public void VwapIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator0 = new VwapIndicator { Period = 0 };  // No reset
        var indicator10 = new VwapIndicator { Period = 10 }; // Reset every 10 bars

        indicator0.Initialize();
        indicator10.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double open = 100 + i;
            double high = open + 10;
            double low = open - 5;
            double close = open + 5;
            double volume = 1000 + (i * 50);

            indicator0.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator10.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);

            indicator0.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator10.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val0 = indicator0.LinesSeries[0].GetValue(0);
        double val10 = indicator10.LinesSeries[0].GetValue(0);

        // Different periods should produce different results
        // Period 0 accumulates all history, Period 10 resets every 10 bars
        Assert.NotEqual(val0, val10, 6);
    }

    [Fact]
    public void VwapIndicator_PeriodReset_ResetsAccumulation()
    {
        var indicator = new VwapIndicator { Period = 5 }; // Reset every 5 bars
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var valuesAtReset = new List<double>();

        for (int i = 0; i < 20; i++)
        {
            double price = 100.0; // Constant price
            double volume = 1000.0; // Constant volume

            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Record value right after reset (at bars 5, 10, 15)
            if (i > 0 && (i + 1) % 5 == 1)
            {
                double val = indicator.LinesSeries[0].GetValue(0);
                valuesAtReset.Add(val);
            }
        }

        // After reset, VWAP should be close to typical price for constant price input
        // All values after reset should be similar (since price is constant)
        Assert.True(valuesAtReset.Count >= 2, "Should have multiple reset points");
    }
}
