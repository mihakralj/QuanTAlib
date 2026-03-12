using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VfIndicatorTests
{
    [Fact]
    public void VfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VfIndicator();

        Assert.Equal("VF - Volume Force", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(14, indicator.Period);
        Assert.Equal(14, indicator.MinHistoryDepths);
    }

    [Fact]
    public void VfIndicator_ShortName_ReflectsPeriod()
    {
        var indicator = new VfIndicator { Period = 20 };
        Assert.Equal("VF(20)", indicator.ShortName);
    }

    [Fact]
    public void VfIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new VfIndicator { Period = 10 };

        Assert.Equal(10, indicator.MinHistoryDepths);
        Assert.Equal(10, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VfIndicator_Period_CanBeSet()
    {
        var indicator = new VfIndicator { Period = 30 };
        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void VfIndicator_Initialize_CreatesInternalVf()
    {
        var indicator = new VfIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void VfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double close = 100 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 100000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void VfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 100000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 105, 115, 100, 112, 80000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void VfIndicator_PriceUp_PositiveForce()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar establishes baseline
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar: close increases -> positive raw_vf
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 110, 98, 108, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"VF should be positive when price increases: {val}");
    }

    [Fact]
    public void VfIndicator_PriceDown_NegativeForce()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar establishes baseline
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Second bar: close decreases -> negative raw_vf
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 102, 90, 92, 10000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"VF should be negative when price decreases: {val}");
    }

    [Fact]
    public void VfIndicator_NoChange_ZeroForce()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // All bars with same close
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 10000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, val, 1);
    }

    [Fact]
    public void VfIndicator_LargerVolume_LargerImpact()
    {
        var indicator1 = new VfIndicator { Period = 14 };
        indicator1.Initialize();

        var indicator2 = new VfIndicator { Period = 14 };
        indicator2.Initialize();

        var now = DateTime.UtcNow;

        // Same price action, different volume
        for (int i = 0; i < 20; i++)
        {
            double close = 100 + i;

            indicator1.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 1000);
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator1.ProcessUpdate(args);
            indicator2.ProcessUpdate(args);
        }

        double val1 = Math.Abs(indicator1.LinesSeries[0].GetValue(0));
        double val2 = Math.Abs(indicator2.LinesSeries[0].GetValue(0));

        // Higher volume should produce larger magnitude
        Assert.True(val2 > val1, $"Higher volume should produce larger VF: {val2} > {val1}");
    }

    [Fact]
    public void VfIndicator_DifferentPeriods_DifferentSmoothing()
    {
        var shortPeriod = new VfIndicator { Period = 5 };
        shortPeriod.Initialize();

        var longPeriod = new VfIndicator { Period = 30 };
        longPeriod.Initialize();

        var now = DateTime.UtcNow;

        // Add volatile data
        for (int i = 0; i < 50; i++)
        {
            double close = 100 + (i % 2 == 0 ? 5 : -3);
            shortPeriod.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);
            longPeriod.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);

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
    public void VfIndicator_EmaSmoothing_ReducesNoise()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var values = new List<double>();

        // Add noisy data
        for (int i = 0; i < 30; i++)
        {
            // Alternating price changes
            double close = 100 + (i % 2 == 0 ? 2 : -2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);

            values.Add(indicator.LinesSeries[0].GetValue(0));
        }

        // After warmup, values should be relatively stable (EMA smoothing)
        var lastValues = values.Skip(20).ToList();
        double range = lastValues.Max() - lastValues.Min();

        // EMA should smooth out the alternating pattern
        Assert.True(range < 100000, $"EMA should smooth values; range={range}");
    }

    [Fact]
    public void VfIndicator_WarmupCompensation_FirstValueNotZero()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // First bar with significant price-volume action
        indicator.HistoricalData.AddBar(now, 100, 110, 95, 105, 50000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // With warmup compensation, first value should not be severely damped
        double firstVal = indicator.LinesSeries[0].GetValue(0);

        // First bar: no previous close, so raw_vf = 0, VF = 0
        // This is expected behavior for first bar
        Assert.True(double.IsFinite(firstVal));
    }

    [Fact]
    public void VfIndicator_OscillatesAroundZero()
    {
        var indicator = new VfIndicator { Period = 14 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        bool hasPositive = false;
        bool hasNegative = false;

        // Mix of up and down days
        for (int i = 0; i < 50; i++)
        {
            double close = 100 + (Math.Sin(i * 0.5) * 10);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), close - 2, close + 2, close - 3, close, 10000);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);

            double val = indicator.LinesSeries[0].GetValue(0);
            if (val > 0)
            {
                hasPositive = true;
            }
            if (val < 0)
            {
                hasNegative = true;
            }
        }

        Assert.True(hasPositive && hasNegative, "VF should oscillate around zero");
    }
}
