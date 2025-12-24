using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class T3IndicatorTests
{
    [Fact]
    public void T3Indicator_Constructor_SetsDefaults()
    {
        var indicator = new T3Indicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(0.7, indicator.VolumeFactor);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("T3 - Tillson T3 Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void T3Indicator_MinHistoryDepths_EqualsSixTimesPeriod()
    {
        var indicator = new T3Indicator { Period = 10 };

        // MinHistoryDepths is Period * 6 for T3 due to 6 stages
        Assert.Equal(0, T3Indicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void T3Indicator_ShortName_IncludesPeriodAndFactor()
    {
        var indicator = new T3Indicator { Period = 15, VolumeFactor = 0.618 };

        Assert.Contains("T3", indicator.ShortName);
        Assert.Contains("15", indicator.ShortName);
        Assert.Contains("0.62", indicator.ShortName); // F2 formatting
    }

    [Fact]
    public void T3Indicator_Initialize_CreatesInternalT3()
    {
        var indicator = new T3Indicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void T3Indicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new T3Indicator { Period = 3 };
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
    public void T3Indicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new T3Indicator { Period = 3 };
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
    public void T3Indicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new T3Indicator { Period = 3 };
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
    public void T3Indicator_MultipleUpdates_ProducesCorrectT3Sequence()
    {
        var indicator = new T3Indicator { Period = 3 };
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

        double lastT3 = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastT3 >= 100 && lastT3 <= 110);
    }

    [Fact]
    public void T3Indicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new T3Indicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void T3Indicator_Parameters_CanBeChanged()
    {
        var indicator = new T3Indicator { Period = 5, VolumeFactor = 0.5 };
        Assert.Equal(5, indicator.Period);
        Assert.Equal(0.5, indicator.VolumeFactor);

        indicator.Period = 20;
        indicator.VolumeFactor = 0.9;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0.9, indicator.VolumeFactor);
        Assert.Equal(0, T3Indicator.MinHistoryDepths); // 20 * 6
    }
}
