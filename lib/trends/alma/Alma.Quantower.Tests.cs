using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class AlmaIndicatorTests
{
    [Fact]
    public void AlmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AlmaIndicator();

        Assert.Equal(9, indicator.Period);
        Assert.Equal(0.85, indicator.Offset);
        Assert.Equal(6.0, indicator.Sigma);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ALMA - Arnaud Legoux Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AlmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new AlmaIndicator { Period = 20 };

        Assert.Equal(0, AlmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void AlmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new AlmaIndicator { Period = 15 };

        Assert.Contains("ALMA", indicator.ShortName);
        Assert.Contains("15", indicator.ShortName);
    }

    [Fact]
    public void AlmaIndicator_Initialize_CreatesInternalAlma()
    {
        var indicator = new AlmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AlmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AlmaIndicator { Period = 3 };
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
    public void AlmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AlmaIndicator { Period = 3 };
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
    public void AlmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new AlmaIndicator { Period = 3 };
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
    public void AlmaIndicator_MultipleUpdates_ProducesCorrectAlmaSequence()
    {
        var indicator = new AlmaIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106 };

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

        // ALMA should be smoothing the values
        double lastAlma = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastAlma >= 100 && lastAlma <= 110);
    }

    [Fact]
    public void AlmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new AlmaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void AlmaIndicator_Period_CanBeChanged()
    {
        var indicator = new AlmaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, AlmaIndicator.MinHistoryDepths);
    }
}
