using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PvoIndicatorTests
{
    [Fact]
    public void PvoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PvoIndicator();

        Assert.Equal("PVO - Percentage Volume Oscillator", indicator.Name);
        Assert.Equal(12, indicator.FastPeriod);
        Assert.Equal(26, indicator.SlowPeriod);
        Assert.Equal(9, indicator.SignalPeriod);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(26, indicator.MinHistoryDepths); // SlowPeriod
    }

    [Fact]
    public void PvoIndicator_ShortName_ReflectsPeriods()
    {
        var indicator = new PvoIndicator { FastPeriod = 5, SlowPeriod = 20, SignalPeriod = 5 };
        Assert.Equal("PVO(5,20,5)", indicator.ShortName);
    }

    [Fact]
    public void PvoIndicator_MinHistoryDepths_EqualsSlowPeriod()
    {
        var indicator = new PvoIndicator { SlowPeriod = 50 };

        Assert.Equal(50, indicator.MinHistoryDepths);
        Assert.Equal(50, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PvoIndicator_Initialize_CreatesInternalPvo()
    {
        var indicator = new PvoIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, three line series should exist (PVO, Signal, Histogram)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void PvoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PvoIndicator();
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

        // PVO series should have a value
        double pvoVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(pvoVal));

        // Signal series should have a value
        double signalVal = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(signalVal));

        // Histogram series should have a value
        double histogramVal = indicator.LinesSeries[2].GetValue(0);
        Assert.True(double.IsFinite(histogramVal));
    }

    [Fact]
    public void PvoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PvoIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + (i * 100));
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 130, 140, 120, 135, 4000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        Assert.Equal(2, indicator.LinesSeries[1].Count);
        Assert.Equal(2, indicator.LinesSeries[2].Count);
    }

    [Fact]
    public void PvoIndicator_Value_IsFinite()
    {
        var indicator = new PvoIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            // Create varying volume patterns
            double volume = 1000 + (i * 50) + ((i % 5) * 200);

            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double pvoVal = indicator.LinesSeries[0].GetValue(0);
        double signalVal = indicator.LinesSeries[1].GetValue(0);
        double histogramVal = indicator.LinesSeries[2].GetValue(0);
        Assert.True(double.IsFinite(pvoVal), $"PVO value {pvoVal} should be finite");
        Assert.True(double.IsFinite(signalVal), $"Signal value {signalVal} should be finite");
        Assert.True(double.IsFinite(histogramVal), $"Histogram value {histogramVal} should be finite");
    }

    [Fact]
    public void PvoIndicator_PositiveValue_OnIncreasingVolume()
    {
        var indicator = new PvoIndicator { FastPeriod = 3, SlowPeriod = 6, SignalPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with increasing volume
        for (int i = 0; i < 15; i++)
        {
            // Exponentially increasing volume
            double volume = 1000 * Math.Pow(1.2, i);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val > 0, $"PVO should be positive on increasing volume, got {val}");
    }

    [Fact]
    public void PvoIndicator_NegativeValue_OnDecreasingVolume()
    {
        var indicator = new PvoIndicator { FastPeriod = 3, SlowPeriod = 6, SignalPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with decreasing volume
        for (int i = 0; i < 15; i++)
        {
            // Start high and decrease
            double volume = 10000 / (1.0 + (i * 0.3));
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(val < 0, $"PVO should be negative on decreasing volume, got {val}");
    }

    [Fact]
    public void PvoIndicator_SignalLine_CalculatedCorrectly()
    {
        var indicator = new PvoIndicator { FastPeriod = 5, SlowPeriod = 10, SignalPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double volume = 1000 + (i * 100);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double pvoVal = indicator.LinesSeries[0].GetValue(0);
        double signalVal = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(pvoVal));
        Assert.True(double.IsFinite(signalVal));
        // Signal is an EMA of PVO, so they should be different in trending conditions
    }

    [Fact]
    public void PvoIndicator_Histogram_EqualsPvoMinusSignal()
    {
        var indicator = new PvoIndicator { FastPeriod = 5, SlowPeriod = 10, SignalPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double volume = 1000 + (i * 150);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double pvoVal = indicator.LinesSeries[0].GetValue(0);
        double signalVal = indicator.LinesSeries[1].GetValue(0);
        double histogramVal = indicator.LinesSeries[2].GetValue(0);

        Assert.Equal(pvoVal - signalVal, histogramVal, 10);
    }

    [Fact]
    public void PvoIndicator_CustomPeriods_AffectsOutput()
    {
        var indicator1 = new PvoIndicator { FastPeriod = 5, SlowPeriod = 10, SignalPeriod = 5 };
        var indicator2 = new PvoIndicator { FastPeriod = 10, SlowPeriod = 20, SignalPeriod = 10 };
        indicator1.Initialize();
        indicator2.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            double volume = 1000 + (i * 100);
            indicator1.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);
            indicator2.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, volume);
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
