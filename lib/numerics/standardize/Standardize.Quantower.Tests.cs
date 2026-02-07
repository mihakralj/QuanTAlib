using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class StandardizeIndicatorTests
{
    [Fact]
    public void StandardizeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StandardizeIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(20, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("STANDARDIZE - Z-Score Normalization", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void StandardizeIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new StandardizeIndicator { Period = 30 };
        Assert.Equal(30, indicator.MinHistoryDepths);
    }

    [Fact]
    public void StandardizeIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new StandardizeIndicator { Period = 10 };
        Assert.Equal("STND(10)", indicator.ShortName);
    }

    [Fact]
    public void StandardizeIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new StandardizeIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Z-Score", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void StandardizeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new StandardizeIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 10, 15, 5, 10);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Single bar: not enough data for stdev, expect 0
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void StandardizeIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new StandardizeIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add bars with varying close values: 2, 4, 6
        indicator.HistoricalData.AddBar(now, 2, 3, 1, 2);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 4, 5, 3, 4);
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 6, 7, 5, 6);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(3, indicator.LinesSeries[0].Count);
        // Last value should be finite (indicator is computing z-score)
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(lastValue), $"Z-score should be finite, got {lastValue}");
    }

    [Fact]
    public void StandardizeIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new StandardizeIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 10, 15, 5, 10);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void StandardizeIndicator_OutputIsFinite()
    {
        var indicator = new StandardizeIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add various bars
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), i * 10, i * 10 + 5, i * 10 - 5, i * 10);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // All z-score values should be finite
        for (int i = 0; i < indicator.LinesSeries[0].Count; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.True(double.IsFinite(val), $"Value {val} at index {i} is not finite");
        }
    }

    [Fact]
    public void StandardizeIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
        };

        foreach (var source in sources)
        {
            var indicator = new StandardizeIndicator { Source = source, Period = 5 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 10 + i, 20 + i, 5 + i, 15 + i);
                indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            }

            Assert.Equal(5, indicator.LinesSeries[0].Count);
            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Source {source}: value {val} is not finite");
        }
    }

    [Fact]
    public void StandardizeIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 2, 5, 14, 50, 100 };

        foreach (var period in periods)
        {
            var indicator = new StandardizeIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < period + 5; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), i, i + 1, i - 1, i);
                indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
            }

            Assert.Equal(period + 5, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void StandardizeIndicator_MeanValue_ReturnsZero()
    {
        var indicator = new StandardizeIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Create symmetric pattern around 50
        indicator.HistoricalData.AddBar(now, 30, 35, 25, 30);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 40, 45, 35, 40);
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 60, 65, 55, 60);
        indicator.HistoricalData.AddBar(now.AddMinutes(3), 70, 75, 65, 70);
        indicator.HistoricalData.AddBar(now.AddMinutes(4), 50, 55, 45, 50);  // Mean

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Last value = 50 = mean of [30, 40, 60, 70, 50] = 250/5 = 50
        // Z-score should be 0
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void StandardizeIndicator_FlatData_ReturnsZero()
    {
        var indicator = new StandardizeIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // All same close values
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100, 105, 95, 100);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // Flat data: stdev = 0, should return 0
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }
}
