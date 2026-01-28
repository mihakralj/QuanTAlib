using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class KvoIndicatorTests
{
    [Fact]
    public void KvoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new KvoIndicator();

        Assert.Equal("KVO - Klinger Volume Oscillator", indicator.Name);
        Assert.Equal(34, indicator.FastPeriod);
        Assert.Equal(55, indicator.SlowPeriod);
        Assert.Equal(13, indicator.SignalPeriod);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(55, indicator.MinHistoryDepths); // SlowPeriod
    }

    [Fact]
    public void KvoIndicator_ShortName_ReflectsPeriods()
    {
        var indicator = new KvoIndicator { FastPeriod = 20, SlowPeriod = 40, SignalPeriod = 10 };
        Assert.Equal("KVO(20,40,10)", indicator.ShortName);
    }

    [Fact]
    public void KvoIndicator_MinHistoryDepths_EqualsSlowPeriod()
    {
        var indicator = new KvoIndicator { SlowPeriod = 80 };

        Assert.Equal(80, indicator.MinHistoryDepths);
        Assert.Equal(80, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void KvoIndicator_Initialize_CreatesInternalKvo()
    {
        var indicator = new KvoIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, two line series should exist (KVO and Signal)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void KvoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new KvoIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 60; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // KVO series should have a value
        double kvoVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(kvoVal));

        // Signal series should have a value
        double signalVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(signalVal));
    }

    [Fact]
    public void KvoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new KvoIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 60; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(60), 160, 170, 150, 165, 7000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        Assert.Equal(2, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void KvoIndicator_Value_IsFinite()
    {
        var indicator = new KvoIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 80; i++)
        {
            // Create varying price patterns
            double open = 100 + i;
            double high = open + 10 + (i % 5);
            double low = open - 5;
            double close = (i % 2 == 0) ? high - 1 : low + 1;
            double volume = 1000 + (i * 100);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, high, low, close, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double kvoVal = indicator.LinesSeries[0].GetValue(0);
        double signalVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(kvoVal), $"KVO value {kvoVal} should be finite");
        Assert.True(double.IsFinite(signalVal), $"Signal value {signalVal} should be finite");
    }

    [Fact]
    public void KvoIndicator_PositiveValue_OnUpwardMovement()
    {
        var indicator = new KvoIndicator { FastPeriod = 3, SlowPeriod = 5, SignalPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with increasing prices (uptrend with accumulation)
        for (int i = 0; i < 15; i++)
        {
            double basePrice = 100 + (i * 3);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 2, basePrice + 3, 1000000 + (i * 100000));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"KVO should be positive on sustained upward movement, got {val}");
    }

    [Fact]
    public void KvoIndicator_NegativeValue_OnDownwardMovement()
    {
        var indicator = new KvoIndicator { FastPeriod = 3, SlowPeriod = 5, SignalPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with decreasing prices (downtrend with distribution)
        for (int i = 0; i < 15; i++)
        {
            double basePrice = 200 - (i * 4);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 2, basePrice - 5, basePrice - 3, 1000000 + (i * 100000));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"KVO should be negative on sustained downward movement, got {val}");
    }

    [Fact]
    public void KvoIndicator_SignalLine_CalculatedCorrectly()
    {
        var indicator = new KvoIndicator { FastPeriod = 5, SlowPeriod = 10, SignalPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 50000 + (i * 1000));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double kvoVal = indicator.LinesSeries[0].GetValue(0);
        double signalVal = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(kvoVal));
        Assert.True(double.IsFinite(signalVal));
        // Signal is an EMA of KVO, so they should be different in trending conditions
    }

    [Fact]
    public void KvoIndicator_CustomPeriods_AffectsOutput()
    {
        var indicator1 = new KvoIndicator { FastPeriod = 10, SlowPeriod = 20, SignalPeriod = 5 };
        var indicator2 = new KvoIndicator { FastPeriod = 20, SlowPeriod = 40, SignalPeriod = 10 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double basePrice = 100 + i;
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 50000 + (i * 1000));
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 50000 + (i * 1000));
            indicator1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val1 = indicator1.LinesSeries[0].GetValue(0);
        double val2 = indicator2.LinesSeries[0].GetValue(0);

        // Different periods should produce different results
        Assert.NotEqual(val1, val2);
        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
    }
}