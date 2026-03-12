using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class ModfIndicatorTests
{
    [Fact]
    public void ModfIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ModfIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(0.8, indicator.Beta);
        Assert.False(indicator.Feedback);
        Assert.Equal(0.5, indicator.FbWeight);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MODF - Modular Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ModfIndicator_MinHistoryDepths_EqualsExpectedValue()
    {
        var indicator = new ModfIndicator { Period = 20 };

        Assert.Equal(14, ModfIndicator.MinHistoryDepths);
        Assert.Equal(14, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void ModfIndicator_ShortName_IncludesParametersAndSource()
    {
        var indicator = new ModfIndicator { Period = 20, Beta = 0.5 };
        indicator.Initialize();

        Assert.Contains("MODF", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("0.5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ModfIndicator_Initialize_CreatesInternalModf()
    {
        var indicator = new ModfIndicator { Period = 10, Beta = 0.6 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ModfIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ModfIndicator { Period = 3, Beta = 0.8 };
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
    public void ModfIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ModfIndicator { Period = 3, Beta = 0.8 };
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
    public void ModfIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ModfIndicator { Period = 3, Beta = 0.8 };
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
    public void ModfIndicator_DifferentSourceTypes()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new ModfIndicator { Period = 5, Source = sourceType };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
