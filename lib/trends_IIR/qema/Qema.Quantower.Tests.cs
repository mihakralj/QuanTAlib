using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class QemaIndicatorTests
{
    [Fact]
    public void QemaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new QemaIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("QEMA - Quad Exponential Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void QemaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new QemaIndicator { Period = 20 };

        Assert.Equal(0, QemaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void QemaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new QemaIndicator { Period = 15 };

        Assert.Contains("QEMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void QemaIndicator_Initialize_CreatesInternalQema()
    {
        var indicator = new QemaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void QemaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new QemaIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void QemaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new QemaIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        // Process first update
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Line series should have values
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void QemaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new QemaIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process historical bar first
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // Update with new tick (same bar data - simulates intrabar update)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        // Both values should be finite
        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void QemaIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new QemaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106, 108, 110, 109 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        // QEMA should be smoothing the values
        // Last QEMA value should be between first and last close
        double lastQema = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastQema >= 95 && lastQema <= 115);
    }

    [Fact]
    public void QemaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new QemaIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void QemaIndicator_Period_CanBeChanged()
    {
        var indicator = new QemaIndicator { Period = 10 };
        Assert.Equal(10, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
        Assert.Equal(0, QemaIndicator.MinHistoryDepths);
    }

    [Fact]
    public void QemaIndicator_LongPeriod_Works()
    {
        var indicator = new QemaIndicator { Period = 100 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 200; i++)
        {
            double price = 100 + (i * 0.1);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Last value should be finite and in reasonable range
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastValue));
        Assert.True(lastValue > 100 && lastValue < 125);
    }

    [Fact]
    public void QemaIndicator_ShortPeriod_Works()
    {
        var indicator = new QemaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 105, 102, 108, 104, 110 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 3, close - 3, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite
        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }
    }
}
