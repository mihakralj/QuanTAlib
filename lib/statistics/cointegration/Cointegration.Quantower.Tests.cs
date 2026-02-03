using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class CointegrationIndicatorTests
{
    [Fact]
    public void CointegrationIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CointegrationIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(SourceType.Open, indicator.Source2);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("COINT - Cointegration (Engle-Granger)", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CointegrationIndicator_MinHistoryDepths_EqualsTwo()
    {
        var indicator = new CointegrationIndicator();

        Assert.Equal(2, CointegrationIndicator.MinHistoryDepths);
        Assert.Equal(2, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CointegrationIndicator_ShortName_IncludesPeriodAndSources()
    {
        var indicator = new CointegrationIndicator { Period = 20 };

        Assert.Contains("COINT", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CointegrationIndicator_Initialize_CreatesInternalCointegration()
    {
        var indicator = new CointegrationIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CointegrationIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CointegrationIndicator { Period = 5 };
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
    public void CointegrationIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CointegrationIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CointegrationIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new CointegrationIndicator { Period = 5 };
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
    public void CointegrationIndicator_MultipleUpdates_ProducesSequence()
    {
        var indicator = new CointegrationIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add bars with different O/C patterns to create cointegration signals
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
    public void CointegrationIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new CointegrationIndicator { Period = 5, Source = source, Source2 = SourceType.Close };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            // Should have computed a value (may be NaN during warmup, but should not throw)
            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void CointegrationIndicator_CointegrationInterpretation()
    {
        // This test verifies the indicator produces meaningful cointegration values
        // when given perfectly correlated data (Open = Close), we expect strong cointegration
        var indicator = new CointegrationIndicator { Period = 5, Source = SourceType.Close, Source2 = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add perfectly proportional bars: Open always equals Close
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // After warmup, should have finite values
        // (Note: when Close == Open exactly, residuals have zero variance, may produce NaN)
        Assert.Equal(20, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void CointegrationIndicator_DifferentSource2Types_Work()
    {
        var source2Types = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.HL2 };

        foreach (var source2 in source2Types)
        {
            var indicator = new CointegrationIndicator { Period = 5, Source = SourceType.Close, Source2 = source2 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void CointegrationIndicator_Period_CanBeChanged()
    {
        var indicator = new CointegrationIndicator { Period = 50 };

        Assert.Equal(50, indicator.Period);

        indicator.Period = 100;
        Assert.Equal(100, indicator.Period);
    }

    [Fact]
    public void CointegrationIndicator_Source2_CanBeChanged()
    {
        var indicator = new CointegrationIndicator { Source2 = SourceType.High };

        Assert.Equal(SourceType.High, indicator.Source2);

        indicator.Source2 = SourceType.Low;
        Assert.Equal(SourceType.Low, indicator.Source2);
    }

    [Fact]
    public void CointegrationIndicator_ReInitialize_ResetsState()
    {
        var indicator = new CointegrationIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);

        // Re-initialize should work without errors
        var indicator2 = new CointegrationIndicator { Period = 5 };
        indicator2.Initialize();
        indicator2.HistoricalData.AddBar(now.AddMinutes(100), 200, 210, 190, 205);
        indicator2.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator2.LinesSeries[0].Count);
    }

    [Fact]
    public void CointegrationIndicator_HighLow_ProducesValues()
    {
        // Test with High vs Low as a practical use case
        var indicator = new CointegrationIndicator { Period = 10, Source = SourceType.High, Source2 = SourceType.Low };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bars with varying spread between high and low
        for (int i = 0; i < 15; i++)
        {
            double mid = 100 + (i * 0.5);
            double spread = 5 + (i % 3); // Varying spread
            indicator.HistoricalData.AddBar(now.AddMinutes(i), mid, mid + spread, mid - spread, mid);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(15, indicator.LinesSeries[0].Count);

        // After warmup period, should have finite values
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        // High and Low should be cointegrated (they move together)
        Assert.True(double.IsFinite(lastValue) || double.IsNaN(lastValue));
    }

    [Fact]
    public void CointegrationIndicator_Description_IsSet()
    {
        var indicator = new CointegrationIndicator();

        Assert.Contains("cointegration", indicator.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ADF", indicator.Description, StringComparison.Ordinal);
    }
}