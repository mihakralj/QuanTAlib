using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RwmaIndicatorTests
{
    [Fact]
    public void RwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RwmaIndicator();

        Assert.Equal("RWMA - Range Weighted Moving Average", indicator.Name);
        Assert.Equal(14, indicator.Period);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(14, indicator.MinHistoryDepths);
    }

    [Fact]
    public void RwmaIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new RwmaIndicator { Period = 10 };
        Assert.Equal("RWMA(10)", indicator.ShortName);

        var indicatorDefault = new RwmaIndicator { Period = 14 };
        Assert.Equal("RWMA(14)", indicatorDefault.ShortName);
    }

    [Fact]
    public void RwmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new RwmaIndicator { Period = 10 };

        Assert.Equal(10, indicator.MinHistoryDepths);
        Assert.Equal(10, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RwmaIndicator_Initialize_CreatesInternalRwma()
    {
        var indicator = new RwmaIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RwmaIndicator { Period = 5 };
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
    public void RwmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RwmaIndicator { Period = 5 };
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
    public void RwmaIndicator_Value_TracksRangeWeightedAverage()
    {
        var indicator = new RwmaIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var recordedValues = new List<double>();

        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns with varying ranges
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            if (i > 0)
            {
                double val = indicator.LinesSeries[0].GetValue(0);
                recordedValues.Add(val);
            }
        }

        // RWMA should produce finite values
        Assert.True(recordedValues.Count > 0, "Should have recorded values");
        Assert.All(recordedValues, v => Assert.True(double.IsFinite(v)));

        // RWMA values should be within price range (approximately)
        double avgValue = recordedValues.Average();
        Assert.True(avgValue > 90 && avgValue < 200, $"RWMA {avgValue} should be within reasonable price range");
    }

    [Fact]
    public void RwmaIndicator_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator5 = new RwmaIndicator { Period = 5 };
        var indicator20 = new RwmaIndicator { Period = 20 };

        indicator5.Initialize();
        indicator20.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double open = 100 + i;
            double high = open + 10;
            double low = open - 5;
            double close = open + 5;

            indicator5.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, 1000);
            indicator20.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, 1000);

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
    public void RwmaIndicator_SlidingWindow_DropsOldValues()
    {
        var indicator = new RwmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add initial bars with constant price
        for (int i = 0; i < 3; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 100, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double valueAtConstant = indicator.LinesSeries[0].GetValue(0);

        // Add bars with higher prices - old low prices should drop out
        for (int i = 3; i < 6; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 200, 210, 190, 200, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double valueAfterHigh = indicator.LinesSeries[0].GetValue(0);

        // Value should have changed significantly as old bars dropped
        Assert.True(valueAfterHigh > valueAtConstant + 50,
            $"RWMA should increase as low-price bars drop out: {valueAtConstant} -> {valueAfterHigh}");
    }

    [Fact]
    public void RwmaIndicator_VolatileBarsHaveMoreWeight()
    {
        var indicator = new RwmaIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with varying ranges — volatile bar at close=50, quiet bar at close=150
        // Volatile bar: range = 40
        indicator.HistoricalData.AddBar(now, 50, 70, 30, 50, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Quiet bar: range = 2
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 150, 151, 149, 150, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);

        // RWMA should be close to 50 (the volatile bar) rather than 150
        Assert.True(val < 60, $"RWMA {val} should be weighted toward volatile bar close (50)");
    }
}
