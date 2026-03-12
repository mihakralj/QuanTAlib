using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VwmaIndicatorTests
{
    [Fact]
    public void VwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VwmaIndicator();

        Assert.Equal("VWMA - Volume Weighted Moving Average", indicator.Name);
        Assert.Equal(20, indicator.Period);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void VwmaIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new VwmaIndicator { Period = 14 };
        Assert.Equal("VWMA(14)", indicator.ShortName);

        var indicatorDefault = new VwmaIndicator { Period = 20 };
        Assert.Equal("VWMA(20)", indicatorDefault.ShortName);
    }

    [Fact]
    public void VwmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new VwmaIndicator { Period = 10 };

        Assert.Equal(10, indicator.MinHistoryDepths);
        Assert.Equal(10, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VwmaIndicator_Initialize_CreatesInternalVwma()
    {
        var indicator = new VwmaIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VwmaIndicator { Period = 5 };
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
    public void VwmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VwmaIndicator { Period = 5 };
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
    public void VwmaIndicator_Value_TracksVolumeWeightedAverage()
    {
        var indicator = new VwmaIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var recordedValues = new List<double>();

        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            double vol = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, vol);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (i > 0)
            {
                double val = indicator.LinesSeries[0].GetValue(0);
                recordedValues.Add(val);
            }
        }

        // VWMA should produce finite values
        Assert.True(recordedValues.Count > 0, "Should have recorded values");
        Assert.All(recordedValues, v => Assert.True(double.IsFinite(v)));

        // VWMA values should be within price range (approximately)
        double avgValue = recordedValues.Average();
        Assert.True(avgValue > 90 && avgValue < 200, $"VWMA {avgValue} should be within reasonable price range");
    }

    [Fact]
    public void VwmaIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator5 = new VwmaIndicator { Period = 5 };
        var indicator20 = new VwmaIndicator { Period = 20 };

        indicator5.Initialize();
        indicator20.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double open = 100 + i;
            double high = open + 10;
            double low = open - 5;
            double close = open + 5;
            double volume = 1000 + (i * 50);

            indicator5.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator20.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);

            indicator5.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator20.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val5 = indicator5.LinesSeries[0].GetValue(0);
        double val20 = indicator20.LinesSeries[0].GetValue(0);

        // Different periods should produce different results
        // Shorter period responds faster to recent prices
        Assert.NotEqual(val5, val20, 6);
    }

    [Fact]
    public void VwmaIndicator_SlidingWindow_DropsOldValues()
    {
        var indicator = new VwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add initial bars with constant price/volume
        for (int i = 0; i < 3; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 101, 99, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double valueAtConstant = indicator.LinesSeries[0].GetValue(0);

        // Add bars with higher prices - old low prices should drop out
        for (int i = 3; i < 6; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 200, 201, 199, 200, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double valueAfterHigh = indicator.LinesSeries[0].GetValue(0);

        // Value should have changed significantly as old bars dropped
        Assert.True(valueAfterHigh > valueAtConstant + 50,
            $"VWMA should increase as low-price bars drop out: {valueAtConstant} -> {valueAfterHigh}");
    }
}
