using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class EomIndicatorTests
{
    [Fact]
    public void EomIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EomIndicator();

        Assert.Equal("EOM - Ease of Movement", indicator.Name);
        Assert.Equal(14, indicator.Period);
        Assert.Equal(10000, indicator.VolumeScale);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(15, indicator.MinHistoryDepths); // Period + 1
    }

    [Fact]
    public void EomIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new EomIndicator { Period = 20 };
        Assert.Equal("EOM(20)", indicator.ShortName);
    }

    [Fact]
    public void EomIndicator_MinHistoryDepths_EqualsPeriodPlusOne()
    {
        var indicator = new EomIndicator { Period = 26 };

        Assert.Equal(27, indicator.MinHistoryDepths); // Period + 1
        Assert.Equal(27, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EomIndicator_Initialize_CreatesInternalEom()
    {
        var indicator = new EomIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void EomIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EomIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void EomIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EomIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EomIndicator_Value_IsFinite()
    {
        var indicator = new EomIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            // Create varying price patterns with price ranges
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            double volume = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val), $"EOM value {val} should be finite");
    }

    [Fact]
    public void EomIndicator_PositiveValue_OnUpwardMovement()
    {
        var indicator = new EomIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar: baseline
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add bars with increasing midpoints (price moving up) with low volume (easy movement)
        for (int i = 1; i <= 10; i++)
        {
            double basePrice = 100 + (i * 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice + 5, 500);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"EOM should be positive on sustained upward movement, got {val}");
    }

    [Fact]
    public void EomIndicator_NegativeValue_OnDownwardMovement()
    {
        var indicator = new EomIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar: baseline
        indicator.HistoricalData.AddBar(now, 150, 160, 140, 150, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add bars with decreasing midpoints (price moving down) with low volume (easy movement)
        for (int i = 1; i <= 10; i++)
        {
            double basePrice = 150 - (i * 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice - 5, 500);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"EOM should be negative on sustained downward movement, got {val}");
    }

    [Fact]
    public void EomIndicator_VolumeScale_AffectsOutput()
    {
        var indicator1 = new EomIndicator { Period = 5, VolumeScale = 10000 };
        var indicator2 = new EomIndicator { Period = 5, VolumeScale = 100000 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 50000);
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 50000);
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        // Different volume scales should produce different magnitude results
        Assert.NotEqual(val1, val2);
        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
    }
}
