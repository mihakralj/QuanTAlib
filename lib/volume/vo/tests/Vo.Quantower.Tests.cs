using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class VoIndicatorTests
{
    [Fact]
    public void VoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new VoIndicator();

        Assert.Equal("VO - Volume Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(5, indicator.ShortPeriod);
        Assert.Equal(10, indicator.LongPeriod);
        Assert.Equal(10, indicator.SignalPeriod);
        Assert.Equal(10, indicator.MinHistoryDepths);
    }

    [Fact]
    public void VoIndicator_ShortName_ReflectsPeriods()
    {
        var indicator = new VoIndicator { ShortPeriod = 3, LongPeriod = 7, SignalPeriod = 5 };
        Assert.Equal("VO(3,7,5)", indicator.ShortName);
    }

    [Fact]
    public void VoIndicator_MinHistoryDepths_EqualsLongPeriod()
    {
        var indicator = new VoIndicator { LongPeriod = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        Assert.Equal(20, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void VoIndicator_Periods_CanBeSet()
    {
        var indicator = new VoIndicator
        {
            ShortPeriod = 12,
            LongPeriod = 26,
            SignalPeriod = 9
        };

        Assert.Equal(12, indicator.ShortPeriod);
        Assert.Equal(26, indicator.LongPeriod);
        Assert.Equal(9, indicator.SignalPeriod);
    }

    [Fact]
    public void VoIndicator_Initialize_CreatesInternalVo()
    {
        var indicator = new VoIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (VO + Signal)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void VoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new VoIndicator { ShortPeriod = 5, LongPeriod = 10, SignalPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double volume = 100000 + i * 1000;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double voVal = indicator.LinesSeries[0].GetValue(0);
        double signalVal = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(voVal));
        Assert.True(double.IsFinite(signalVal));
    }

    [Fact]
    public void VoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new VoIndicator { ShortPeriod = 5, LongPeriod = 10, SignalPeriod = 5 };
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
        Assert.Equal(2, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void VoIndicator_ConstantVolume_ZeroOscillator()
    {
        var indicator = new VoIndicator { ShortPeriod = 3, LongPeriod = 6, SignalPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // All bars with same volume
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 50000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double voVal = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, voVal, 1);
    }

    [Fact]
    public void VoIndicator_IncreasingVolume_PositiveOscillator()
    {
        var indicator = new VoIndicator { ShortPeriod = 3, LongPeriod = 6, SignalPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Volume increases over time - short MA will exceed long MA
        for (int i = 0; i < 20; i++)
        {
            double volume = 10000 + i * 5000; // Increasing volume
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double voVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(voVal > 0, $"VO should be positive when volume increasing: {voVal}");
    }

    [Fact]
    public void VoIndicator_DecreasingVolume_NegativeOscillator()
    {
        var indicator = new VoIndicator { ShortPeriod = 3, LongPeriod = 6, SignalPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Volume decreases over time - short MA will be below long MA
        for (int i = 0; i < 20; i++)
        {
            double volume = 100000 - i * 4000; // Decreasing volume
            volume = Math.Max(volume, 1000); // Keep positive
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        double voVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(voVal < 0, $"VO should be negative when volume decreasing: {voVal}");
    }

    [Fact]
    public void VoIndicator_SignalLine_SmoothsVo()
    {
        var indicator = new VoIndicator { ShortPeriod = 3, LongPeriod = 6, SignalPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var voValues = new List<double>();
        var signalValues = new List<double>();

        // Add oscillating volume
        for (int i = 0; i < 30; i++)
        {
            double volume = 50000 + (i % 2 == 0 ? 20000 : -10000);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);

            if (i >= 10) // After warmup
            {
                voValues.Add(indicator.LinesSeries[0].GetValue(0));
                signalValues.Add(indicator.LinesSeries[1].GetValue(0));
            }
        }

        // Signal line should be smoother (smaller range)
        double voRange = voValues.Max() - voValues.Min();
        double signalRange = signalValues.Max() - signalValues.Min();

        Assert.True(signalRange <= voRange, $"Signal should be smoother: VO range={voRange}, Signal range={signalRange}");
    }

    [Fact]
    public void VoIndicator_DifferentPeriods_DifferentResults()
    {
        var shortPeriods = new VoIndicator { ShortPeriod = 3, LongPeriod = 6, SignalPeriod = 3 };
        shortPeriods.Initialize();

        var longPeriods = new VoIndicator { ShortPeriod = 10, LongPeriod = 20, SignalPeriod = 10 };
        longPeriods.Initialize();

        var now = DateTime.UtcNow;

        // Add same data to both
        for (int i = 0; i < 50; i++)
        {
            double volume = 50000 + Math.Sin(i * 0.3) * 20000;
            shortPeriods.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);
            longPeriods.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            shortPeriods.ProcessUpdate(args);
            longPeriods.ProcessUpdate(args);
        }

        double shortVal = shortPeriods.LinesSeries[0].GetValue(0);
        double longVal = longPeriods.LinesSeries[0].GetValue(0);

        // Different periods should produce different results
        Assert.NotEqual(shortVal, longVal, 3);
    }

    [Fact]
    public void VoIndicator_ReturnsPercentage()
    {
        var indicator = new VoIndicator { ShortPeriod = 2, LongPeriod = 4, SignalPeriod = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Start with baseline volume
        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, 10000);
            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        // Add bar with significantly higher volume
        indicator.HistoricalData.AddBar(now.AddMinutes(5), 100, 105, 95, 100, 20000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double voVal = indicator.LinesSeries[0].GetValue(0);

        // VO should be positive percentage (short MA > long MA)
        Assert.True(voVal > 0, $"VO should be positive: {voVal}");
        Assert.True(voVal <= 200, $"VO should be reasonable percentage: {voVal}"); // Not too extreme
    }

    [Fact]
    public void VoIndicator_OscillatesAroundZero()
    {
        var indicator = new VoIndicator { ShortPeriod = 5, LongPeriod = 10, SignalPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        bool hasPositive = false;
        bool hasNegative = false;

        // Oscillating volume pattern
        for (int i = 0; i < 50; i++)
        {
            double volume = 50000 + Math.Sin(i * 0.5) * 30000;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100, volume);

            var args = i == 0
                ? new UpdateArgs(UpdateReason.HistoricalBar)
                : new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);

            if (i > 15) // After warmup
            {
                double val = indicator.LinesSeries[0].GetValue(0);
                if (val > 0.5)
                {
                    hasPositive = true;
                }
                if (val < -0.5)
                {
                    hasNegative = true;
                }
            }
        }

        Assert.True(hasPositive && hasNegative, "VO should oscillate around zero");
    }
}
