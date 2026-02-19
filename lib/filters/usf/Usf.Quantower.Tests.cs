using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class UsfIndicatorTests
{
    [Fact]
    public void UsfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new UsfIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("USF - Ehlers Ultimate Smoother Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void UsfIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new UsfIndicator();

        Assert.Equal(0, UsfIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void UsfIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new UsfIndicator { Period = 14 };

        Assert.True(indicator.ShortName.Contains("USF", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("14", StringComparison.Ordinal));
        Assert.True(indicator.ShortName.Contains("Close", StringComparison.Ordinal));
    }

    [Fact]
    public void UsfIndicator_Initialize_CreatesInternalUsf()
    {
        var indicator = new UsfIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void UsfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new UsfIndicator { Period = 5 };
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
    public void UsfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new UsfIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void UsfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new UsfIndicator { Period = 5 };
        indicator.Initialize();

        // Should not throw an exception
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        // Assert that the indicator still exists (method completed without exception)
        Assert.NotNull(indicator);
    }

    [Fact]
    public void UsfIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new UsfIndicator { Period = 5 };
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
    public void UsfIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new UsfIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void UsfIndicator_Period_CanBeChanged()
    {
        var indicator = new UsfIndicator { Period = 10 };

        Assert.Equal(10, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void UsfIndicator_Source_CanBeChanged()
    {
        var indicator = new UsfIndicator { Source = SourceType.Close };

        Assert.Equal(SourceType.Close, indicator.Source);

        indicator.Source = SourceType.Open;
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void UsfIndicator_ShowColdValues_CanBeChanged()
    {
        var indicator = new UsfIndicator { ShowColdValues = true };

        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void UsfIndicator_ShortName_UpdatesWhenPeriodChanges()
    {
        var indicator = new UsfIndicator { Period = 10 };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("10", StringComparison.Ordinal));

        indicator.Period = 20;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("20", StringComparison.Ordinal));
    }

    [Fact]
    public void UsfIndicator_ShortName_UpdatesWhenSourceChanges()
    {
        var indicator = new UsfIndicator { Source = SourceType.Close };
        string initialName = indicator.ShortName;

        Assert.True(initialName.Contains("Close", StringComparison.Ordinal));

        indicator.Source = SourceType.Open;
        string updatedName = indicator.ShortName;

        Assert.True(updatedName.Contains("Open", StringComparison.Ordinal));
    }

    [Fact]
    public void UsfIndicator_ProcessUpdate_IgnoresNonBarUpdates()
    {
        var indicator = new UsfIndicator { Period = 5 };
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
    public void UsfIndicator_LineSeries_HasCorrectProperties()
    {
        var indicator = new UsfIndicator { Period = 10 };
        indicator.Initialize();

        var lineSeries = indicator.LinesSeries[0];

        Assert.True(lineSeries.Name.Contains("USF 20", StringComparison.Ordinal)); // LineSeries name is set in constructor with default period
        Assert.Equal(2, lineSeries.Width);
        Assert.Equal(LineStyle.Solid, lineSeries.Style);
    }
}
