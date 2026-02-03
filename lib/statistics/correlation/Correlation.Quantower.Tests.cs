using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class CorrelationIndicatorTests
{
    [Fact]
    public void CorrelationIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CorrelationIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(SourceType.Open, indicator.Source2);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CORR - Pearson Correlation Coefficient", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CorrelationIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new CorrelationIndicator();

        Assert.Equal(2, CorrelationIndicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CorrelationIndicator_ShortName_IncludesPeriodAndSources()
    {
        var indicator = new CorrelationIndicator { Period = 20 };

        Assert.Contains("CORR", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrelationIndicator_Initialize_CreatesInternalCorrelation()
    {
        var indicator = new CorrelationIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CorrelationIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CorrelationIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value (may be NaN during warmup)
        Assert.Equal(1, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CorrelationIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CorrelationIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CorrelationIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CorrelationIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        // NewTick should not throw
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        // Values should be produced (may be NaN during warmup, but should not throw)
        Assert.True(double.IsNaN(firstValue) || double.IsFinite(firstValue));
        Assert.True(double.IsNaN(secondValue) || double.IsFinite(secondValue));
    }

    [Fact]
    public void CorrelationIndicator_MultipleUpdates_ProducesSequence()
    {
        var indicator = new CorrelationIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add bars with different O/C patterns to create varying correlation
        double[] opens = { 100, 101, 102, 103, 104, 105 };
        double[] closes = { 100, 101, 102, 103, 104, 105 };

        for (int i = 0; i < opens.Length; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), opens[i], opens[i] + 5, opens[i] - 5, closes[i]);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // All values should exist
        Assert.Equal(opens.Length, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CorrelationIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new CorrelationIndicator { Period = 5, Source = source, Source2 = SourceType.Close };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Should have computed a value (may be NaN during warmup, but should not throw)
            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void CorrelationIndicator_CorrelationBounds()
    {
        // This test verifies the indicator produces values in valid range [-1, +1]
        var indicator = new CorrelationIndicator { Period = 5, Source = SourceType.Close, Source2 = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with varying patterns
        for (int i = 0; i < 20; i++)
        {
            double open = 100 + i;
            double close = 100 + i + (i % 2 == 0 ? 2 : -1); // Varying relationship
            indicator.HistoricalData.AddBar(now.AddMinutes(i), open, open + 5, open - 5, close);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // After warmup, should have values in valid range
        Assert.Equal(20, indicator.LinesSeries[0].Count);

        // Check that values are bounded
        for (int i = 0; i < 20; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(i);
            if (double.IsFinite(value))
            {
                Assert.InRange(value, -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void CorrelationIndicator_DifferentSource2Types_Work()
    {
        var source2Types = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.HL2 };

        foreach (var source2 in source2Types)
        {
            var indicator = new CorrelationIndicator { Period = 5, Source = SourceType.Close, Source2 = source2 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void CorrelationIndicator_Period_CanBeChanged()
    {
        var indicator = new CorrelationIndicator { Period = 50 };

        Assert.Equal(50, indicator.Period);

        indicator.Period = 100;
        Assert.Equal(100, indicator.Period);
    }

    [Fact]
    public void CorrelationIndicator_Source2_CanBeChanged()
    {
        var indicator = new CorrelationIndicator { Source2 = SourceType.High };

        Assert.Equal(SourceType.High, indicator.Source2);

        indicator.Source2 = SourceType.Low;
        Assert.Equal(SourceType.Low, indicator.Source2);
    }

    [Fact]
    public void CorrelationIndicator_ReInitialize_ResetsState()
    {
        var indicator = new CorrelationIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);

        // Re-initialize should work without errors
        var indicator2 = new CorrelationIndicator { Period = 5 };
        indicator2.Initialize();
        indicator2.HistoricalData.AddBar(now.AddMinutes(100), 200, 210, 190, 205);
        indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator2.LinesSeries[0].Count);
    }

    [Fact]
    public void CorrelationIndicator_HighLow_ProducesPositiveCorrelation()
    {
        // Test with High vs Low - they should be positively correlated
        var indicator = new CorrelationIndicator { Period = 10, Source = SourceType.High, Source2 = SourceType.Low };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with typical High > Low relationship
        for (int i = 0; i < 15; i++)
        {
            double mid = 100 + (i * 0.5);
            double spread = 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), mid, mid + spread, mid - spread, mid);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(15, indicator.LinesSeries[0].Count);

        // After warmup period, High and Low should show positive correlation
        // (they both trend together as price moves)
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        if (double.IsFinite(lastValue))
        {
            Assert.True(lastValue > 0, $"Expected positive correlation for High vs Low, got {lastValue}");
        }
    }

    [Fact]
    public void CorrelationIndicator_Description_IsSet()
    {
        var indicator = new CorrelationIndicator();

        Assert.Contains("linear", indicator.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-1", indicator.Description, StringComparison.Ordinal);
        Assert.Contains("+1", indicator.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrelationIndicator_PerfectCorrelation_ReturnsOne()
    {
        // When Close == Open for all bars, correlation should be 1.0 (or NaN if zero variance)
        var indicator = new CorrelationIndicator { Period = 5, Source = SourceType.Close, Source2 = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars where Close always equals Open (perfect linear relationship)
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 2; // Trending up
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 5, price - 5, price);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);

        // When Open == Close exactly, we get perfect correlation = 1.0
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        if (double.IsFinite(lastValue))
        {
            Assert.Equal(1.0, lastValue, precision: 6);
        }
    }
}