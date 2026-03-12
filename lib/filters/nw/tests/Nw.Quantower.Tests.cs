using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class NwIndicatorTests
{
    [Fact]
    public void NwIndicator_Constructor_SetsDefaults()
    {
        var indicator = new NwIndicator();

        Assert.Equal(64, indicator.Period);
        Assert.Equal(8.0, indicator.Bandwidth);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("NW - Nadaraya-Watson Estimator", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void NwIndicator_MinHistoryDepths_EqualsExpectedValue()
    {
        var indicator = new NwIndicator { Period = 20 };

        Assert.Equal(64, NwIndicator.MinHistoryDepths);
        Assert.Equal(64, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void NwIndicator_ShortName_IncludesParametersAndSource()
    {
        var indicator = new NwIndicator { Period = 32, Bandwidth = 4.0 };
        indicator.Initialize();

        Assert.Contains("NW", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("32", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("4.0", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void NwIndicator_Initialize_CreatesInternalNw()
    {
        var indicator = new NwIndicator { Period = 10, Bandwidth = 4.0 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void NwIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new NwIndicator { Period = 3, Bandwidth = 2.0 };
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
    public void NwIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new NwIndicator { Period = 3, Bandwidth = 2.0 };
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
    public void NwIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new NwIndicator { Period = 3, Bandwidth = 2.0 };
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
    public void NwIndicator_DifferentSourceTypes()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new NwIndicator { Period = 5, Source = sourceType };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
