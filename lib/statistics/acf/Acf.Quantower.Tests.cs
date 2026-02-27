using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class AcfIndicatorTests
{
    [Fact]
    public void AcfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AcfIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(1, indicator.Lag);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ACF - Autocorrelation Function", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AcfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AcfIndicator();

        Assert.Equal(0, AcfIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AcfIndicator_ShortName_IncludesPeriodAndLag()
    {
        var indicator = new AcfIndicator { Period = 14, Lag = 2 };

        Assert.True(indicator.ShortName.Contains("ACF", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("14", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("2", StringComparison.Ordinal));
    }

    [Fact]
    public void AcfIndicator_Initialize_CreatesInternalAcf()
    {
        var indicator = new AcfIndicator { Period = 10, Lag = 1 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AcfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AcfIndicator { Period = 5, Lag = 1 };
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
    public void AcfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AcfIndicator { Period = 5, Lag = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AcfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AcfIndicator { Period = 5, Lag = 1 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void AcfIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new AcfIndicator { Period = 5, Lag = 1 };
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
    public void AcfIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new AcfIndicator { Period = 5, Lag = 1, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void AcfIndicator_Period_CanBeChanged()
    {
        var indicator = new AcfIndicator { Period = 10 };

        Assert.Equal(10, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void AcfIndicator_Lag_CanBeChanged()
    {
        var indicator = new AcfIndicator { Lag = 1 };

        Assert.Equal(1, indicator.Lag);

        indicator.Lag = 5;
        Assert.Equal(5, indicator.Lag);
    }

    [Fact]
    public void AcfIndicator_Source_CanBeChanged()
    {
        var indicator = new AcfIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void AcfIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new AcfIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void AcfIndicator_ShortName_UpdatesWhenPeriodChanges()
    {
        var indicator = new AcfIndicator { Period = 10 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("10", StringComparison.Ordinal));

        indicator.Period = 20;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void AcfIndicator_ShortName_UpdatesWhenLagChanges()
    {
        var indicator = new AcfIndicator { Lag = 1 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("1", StringComparison.Ordinal));

        indicator.Lag = 3;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("3", StringComparison.Ordinal));
    }

    [Fact]
    public void AcfIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new AcfIndicator { Period = 5, Lag = 1 };
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
    public void AcfIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new AcfIndicator { Period = 10 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.Equal("ACF", lineSeries.Name);
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }

    [Fact]
    public void AcfIndicator_DifferentLagValues_Work()
    {
        var lags = new[] { 1, 2, 3, 5, 10 };

        foreach (var lag in lags)
        {
            // Period must be > lag + 1
            int period = Math.Max(20, lag + 5);
            var indicator = new AcfIndicator { Period = period, Lag = lag };
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
            double acfValue = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(acfValue), $"Lag {lag} should produce finite value");
            Assert.True(acfValue >= -1 && acfValue <= 1, $"ACF at lag {lag} should be bounded [-1, 1]");
        }
    }

    [Fact]
    public void AcfIndicator_AcfValuesAreBounded()
    {
        var indicator = new AcfIndicator { Period = 10, Lag = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 98, 105, 97, 110, 95, 108, 92, 115, 90, 120 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 5, close - 5, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        // All ACF values should be bounded between -1 and 1
        for (int i = 0; i < closes.Length; i++)
        {
            double value = indicator.LinesSeries[0].GetValue(closes.Length - 1 - i);
            Assert.True(value >= -1 && value <= 1, $"ACF value at index {i} should be bounded [-1, 1], got {value}");
        }
    }
}
