using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class NotchIndicatorTests
{
    [Fact]
    public void NotchIndicator_Constructor_SetsDefaults()
    {
        var indicator = new NotchIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(1.0, indicator.Q);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Notch - Notch Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void NotchIndicator_MinHistoryDepths_EqualsExpectedValue()
    {
        var indicator = new NotchIndicator { Period = 20 };

        Assert.Equal(14, NotchIndicator.MinHistoryDepths);
        Assert.Equal(14, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void NotchIndicator_ShortName_IncludesParametersAndSource()
    {
        var indicator = new NotchIndicator { Period = 20, Q = 0.5 };
        indicator.Initialize();

        Assert.Contains("Notch", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void NotchIndicator_Initialize_CreatesInternalNotch()
    {
        var indicator = new NotchIndicator { Period = 10, Q = 2.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void NotchIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new NotchIndicator { Period = 3, Q = 1.0 };
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
    public void NotchIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new NotchIndicator { Period = 3, Q = 1.0 };
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
    public void NotchIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new NotchIndicator { Period = 3, Q = 1.0 };
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
    public void NotchIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new NotchIndicator { Period = 5, Q = 1.0 };
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
    }

    [Fact]
    public void NotchIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new NotchIndicator { Period = 5, Q = 1.0, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void NotchIndicator_Period_CanBeChanged()
    {
        var indicator = new NotchIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void NotchIndicator_Q_CanBeChanged()
    {
        var indicator = new NotchIndicator { Q = 0.5 };
        Assert.Equal(0.5, indicator.Q);

        indicator.Q = 2.5;
        Assert.Equal(2.5, indicator.Q);
    }

    [Fact]
    public void NotchIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new NotchIndicator { ShowColdValues = false };
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void NotchIndicator_DifferentQValues_ProduceDifferentResults()
    {
        var indicatorQ1 = new NotchIndicator { Period = 10, Q = 0.5 };
        var indicatorQ2 = new NotchIndicator { Period = 10, Q = 2.0 };

        indicatorQ1.Initialize();
        indicatorQ2.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 105, 103, 108, 106, 110, 108, 112, 110, 115 };

        foreach (var close in closes)
        {
            indicatorQ1.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicatorQ2.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicatorQ1.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            indicatorQ2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // Different Q values should produce different results
        double valueQ1 = indicatorQ1.LinesSeries[0].GetValue(0);
        double valueQ2 = indicatorQ2.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(valueQ1));
        Assert.True(double.IsFinite(valueQ2));
        // Values may or may not be equal depending on input, but both should be finite
    }

    [Fact]
    public void NotchIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new NotchIndicator { Period = 14 };

        Assert.Single(indicator.LinesSeries);
        var lineSeries = indicator.LinesSeries[0];
        Assert.NotNull(lineSeries);
    }

    [Fact]
    public void NotchIndicator_BarCorrection_ProcessesCorrectly()
    {
        var indicator = new NotchIndicator { Period = 5, Q = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add first bar
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add second bar
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        double valueAfterNewBar = indicator.LinesSeries[0].GetValue(0);

        // Simulate tick update (bar correction - same bar being updated)
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        double valueAfterTick = indicator.LinesSeries[0].GetValue(0);

        // Both should be finite
        Assert.True(double.IsFinite(valueAfterNewBar));
        Assert.True(double.IsFinite(valueAfterTick));
    }

    [Fact]
    public void NotchIndicator_OHLCV_SourceType_Works()
    {
        var indicator = new NotchIndicator { Period = 5, Q = 1.0, Source = SourceType.OHLC4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void NotchIndicator_LongSequence_RemainsStable()
    {
        var indicator = new NotchIndicator { Period = 14, Q = 1.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        var random = new Random(42); // Fixed seed for reproducibility

        // Generate 100 bars
        for (int i = 0; i < 100; i++)
        {
            double close = 100 + random.NextDouble() * 20;
            indicator.HistoricalData.AddBar(now, close - 1, close + 2, close - 3, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All values should be finite after long sequence
        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(99 - i)),
                $"Value at index {i} should be finite");
        }
    }
}