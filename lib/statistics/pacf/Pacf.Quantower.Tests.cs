using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class PacfIndicatorTests
{
    [Fact]
    public void PacfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PacfIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(1, indicator.Lag);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("PACF - Partial Autocorrelation Function", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PacfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PacfIndicator();

        Assert.Equal(0, PacfIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PacfIndicator_ShortName_IncludesPeriodAndLag()
    {
        var indicator = new PacfIndicator { Period = 14, Lag = 2 };

        Assert.True(indicator.ShortName.Contains("PACF", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("14", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("2", StringComparison.Ordinal));
    }

    [Fact]
    public void PacfIndicator_Initialize_CreatesInternalPacf()
    {
        var indicator = new PacfIndicator { Period = 10, Lag = 1 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PacfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PacfIndicator { Period = 5, Lag = 1 };
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
    public void PacfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PacfIndicator { Period = 5, Lag = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PacfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new PacfIndicator { Period = 5, Lag = 1 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void PacfIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new PacfIndicator { Period = 5, Lag = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 105, 103, 107, 110 };

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
    public void PacfIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new PacfIndicator { Period = 5, Lag = 1, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void PacfIndicator_Period_CanBeChanged()
    {
        var indicator = new PacfIndicator { Period = 10 };

        Assert.Equal(10, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void PacfIndicator_Lag_CanBeChanged()
    {
        var indicator = new PacfIndicator { Lag = 1 };

        Assert.Equal(1, indicator.Lag);

        indicator.Lag = 5;
        Assert.Equal(5, indicator.Lag);
    }

    [Fact]
    public void PacfIndicator_Source_CanBeChanged()
    {
        var indicator = new PacfIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void PacfIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new PacfIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void PacfIndicator_ShortName_UpdatesWhenPeriodChanges()
    {
        var indicator = new PacfIndicator { Period = 10 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("10", StringComparison.Ordinal));

        indicator.Period = 20;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void PacfIndicator_ShortName_UpdatesWhenLagChanges()
    {
        var indicator = new PacfIndicator { Lag = 1 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("1", StringComparison.Ordinal));

        indicator.Lag = 3;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("3", StringComparison.Ordinal));
    }

    [Fact]
    public void PacfIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new PacfIndicator { Period = 5, Lag = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process historical bar first
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Process other update reasons - should not throw
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void PacfIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new PacfIndicator { Period = 10 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("PACF", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void PacfIndicator_DifferentLagValues_Work()
    {
        var lags = new[] { 1, 2, 3, 5, 10 };

        foreach (var lag in lags)
        {
            // Period must be > lag + 1
            int period = Math.Max(20, lag + 5);
            var indicator = new PacfIndicator { Period = period, Lag = lag };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            // Add enough bars to fill the buffer
            for (int i = 0; i < period + 5; i++)
            {
                double close = 100 + (i % 10);
                indicator.HistoricalData.AddBar(now.AddMinutes(i), close, close + 2, close - 2, close);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            // Last value should be finite and bounded
            double pacfValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(pacfValue), $"Lag {lag} should produce finite value");
            Assert.True(pacfValue >= -1 && pacfValue <= 1, $"PACF at lag {lag} should be bounded [-1, 1]");
        }
    }

    [Fact]
    public void PacfIndicator_PacfValuesAreBounded()
    {
        var indicator = new PacfIndicator { Period = 10, Lag = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 98, 105, 97, 110, 95, 108, 92, 115, 90, 120 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 5, close - 5, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All PACF values should be bounded between -1 and 1
        for (int i = 0; i < closes.Length; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(closes.Length - 1 - i);
            Assert.True(value >= -1 && value <= 1, $"PACF value at index {i} should be bounded [-1, 1], got {value}");
        }
    }

    [Fact]
    public void PacfIndicator_AtLagOne_EqualsAcf()
    {
        // PACF at lag 1 should equal ACF at lag 1 (key mathematical property)
        var pacfIndicator = new PacfIndicator { Period = 10, Lag = 1 };
        var acfIndicator = new AcfIndicator { Period = 10, Lag = 1 };

        pacfIndicator.Initialize();
        acfIndicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 98, 105, 97, 110, 95, 108, 92, 115, 90, 120 };

        foreach (var close in closes)
        {
            pacfIndicator.HistoricalData.AddBar(now, close, close + 5, close - 5, close);
            acfIndicator.HistoricalData.AddBar(now, close, close + 5, close - 5, close);

            pacfIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            acfIndicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            now = now.AddMinutes(1);
        }

        // At lag 1, PACF should equal ACF
        double pacfValue = pacfIndicator.LinesSeries[0].GetValue(0);
        double acfValue = acfIndicator.LinesSeries[0].GetValue(0);

        Assert.Equal(acfValue, pacfValue, 6); // Allow for minor floating-point differences
    }
}
