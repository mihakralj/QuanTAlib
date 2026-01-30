using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VrocIndicatorTests
{
    [Fact]
    public void VrocIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VrocIndicator();

        Assert.Equal("VROC - Volume Rate of Change", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(12, indicator.Period);
        Assert.True(indicator.UsePercent);
        Assert.Equal(13, indicator.MinHistoryDepths);
    }

    [Fact]
    public void VrocIndicator_ShortName_ReflectsParameters()
    {
        var indicator = new VrocIndicator { Period = 20, UsePercent = true };
        Assert.Equal("VROC(20,%)", indicator.ShortName);

        var indicatorPt = new VrocIndicator { Period = 15, UsePercent = false };
        Assert.Equal("VROC(15,pt)", indicatorPt.ShortName);
    }

    [Fact]
    public void VrocIndicator_MinHistoryDepths_EqualsPeriodPlusOne()
    {
        var indicator = new VrocIndicator { Period = 10 };

        Assert.Equal(11, indicator.MinHistoryDepths);
        Assert.Equal(11, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VrocIndicator_Period_CanBeSet()
    {
        var indicator = new VrocIndicator { Period = 30 };
        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void VrocIndicator_UsePercent_CanBeSet()
    {
        var indicator = new VrocIndicator { UsePercent = false };
        Assert.False(indicator.UsePercent);
    }

    [Fact]
    public void VrocIndicator_Initialize_CreatesInternalVroc()
    {
        var indicator = new VrocIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VrocIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VrocIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double volume = 100000 + i * 1000;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void VrocIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VrocIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 105, 115, 100, 112, 200000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VrocIndicator_DoubleVolume_Returns100Percent()
    {
        var indicator = new VrocIndicator { Period = 3, UsePercent = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with constant volume (need enough to fill buffer)
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        // Double the volume
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 100, 105, 95, 100, 2000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        // (2000 - 1000) / 1000 * 100 = 100%
        Assert.Equal(100.0, val, 1);
    }

    [Fact]
    public void VrocIndicator_HalfVolume_ReturnsMinus50Percent()
    {
        var indicator = new VrocIndicator { Period = 3, UsePercent = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with constant volume (need enough to fill buffer)
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        // Half the volume
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 100, 105, 95, 100, 500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        // (500 - 1000) / 1000 * 100 = -50%
        Assert.Equal(-50.0, val, 1);
    }

    [Fact]
    public void VrocIndicator_PointMode_ReturnsAbsoluteChange()
    {
        var indicator = new VrocIndicator { Period = 3, UsePercent = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with constant volume (need enough to fill buffer)
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        // Double the volume
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 100, 105, 95, 100, 2000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        // 2000 - 1000 = 1000 (absolute change)
        Assert.Equal(1000.0, val, 1);
    }

    [Fact]
    public void VrocIndicator_SameVolume_ReturnsZero()
    {
        var indicator = new VrocIndicator { Period = 3, UsePercent = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // All bars with same volume
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0.0, val, 1);
    }

    [Fact]
    public void VrocIndicator_IncreasingVolumes_ReturnsPositive()
    {
        var indicator = new VrocIndicator { Period = 5, UsePercent = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Increasing volumes
        for (int i = 0; i < 20; i++)
        {
            double volume = 1000 + i * 100;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"VROC should be positive with increasing volume: {val}");
    }

    [Fact]
    public void VrocIndicator_DecreasingVolumes_ReturnsNegative()
    {
        var indicator = new VrocIndicator { Period = 5, UsePercent = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Decreasing volumes
        for (int i = 0; i < 20; i++)
        {
            double volume = 5000 - i * 100;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"VROC should be negative with decreasing volume: {val}");
    }

    [Fact]
    public void VrocIndicator_DifferentPeriods_DifferentResults()
    {
        var shortPeriod = new VrocIndicator { Period = 3 };
        shortPeriod.Initialize();

        var longPeriod = new VrocIndicator { Period = 10 };
        longPeriod.Initialize();

        var now = DateTime.UtcNow;

        // Volatile volume data
        for (int i = 0; i < 30; i++)
        {
            double volume = 1000 + (i % 2 == 0 ? 500 : -300);
            shortPeriod.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);
            longPeriod.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            shortPeriod.ProcessUpdate(args);
            longPeriod.ProcessUpdate(args);
        }

        double shortVal = shortPeriod.LinesSeries[0].GetValue(0);
        double longVal = longPeriod.LinesSeries[0].GetValue(0);

        // Different periods should produce different results
        Assert.NotEqual(shortVal, longVal, 1);
    }

    [Fact]
    public void VrocIndicator_VolumeSurge_DetectedAsSpikePercent()
    {
        var indicator = new VrocIndicator { Period = 5, UsePercent = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Normal volume
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 1000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        // Volume surge (10x)
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        // (10000 - 1000) / 1000 * 100 = 900%
        Assert.Equal(900.0, val, 1);
    }

    [Fact]
    public void VrocIndicator_ZeroHistoricalVolume_ReturnsZeroPercent()
    {
        var indicator = new VrocIndicator { Period = 3, UsePercent = true };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Zero volume bars
        for (int i = 0; i < 3; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 0);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Non-zero volume
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 100, 105, 95, 100, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        // Division by zero protection should return 0
        Assert.Equal(0.0, val, 1);
    }
}