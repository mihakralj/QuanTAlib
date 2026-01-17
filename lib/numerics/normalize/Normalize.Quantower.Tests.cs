using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class NormalizeIndicatorTests
{
    [Fact]
    public void NormalizeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new NormalizeIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("NORMALIZE - Min-Max Normalization", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void NormalizeIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new NormalizeIndicator { Period = 20 };
        Assert.Equal(20, indicator.MinHistoryDepths);
    }

    [Fact]
    public void NormalizeIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new NormalizeIndicator { Period = 10 };
        Assert.Equal("NORM(10)", indicator.ShortName);
    }

    [Fact]
    public void NormalizeIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new NormalizeIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Normalize", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void NormalizeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new NormalizeIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 10, 15, 5, 10);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Single bar: value = min = max, so normalized = 0.5
        Assert.Equal(0.5, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void NormalizeIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new NormalizeIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add bars with varying close values
        indicator.HistoricalData.AddBar(now, 0, 1, 0, 0);  // Close = 0 (min)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 0, 1, 0, 10); // Close = 10 (max)
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 0, 1, 0, 5);  // Close = 5 (mid)

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(3, indicator.LinesSeries[0].Count);
        // Last value: 5 normalized to [0,10] = 0.5
        Assert.Equal(0.5, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void NormalizeIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new NormalizeIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 10, 15, 5, 10);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void NormalizeIndicator_OutputAlwaysBounded()
    {
        var indicator = new NormalizeIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Add various bars
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), i * 10, i * 10 + 5, i * 10 - 5, i * 10);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // All normalized values should be in [0, 1]
        for (int i = 0; i < indicator.LinesSeries[0].Count; i++)
        {
            double val = indicator.LinesSeries[0].GetValue(i);
            Assert.True(val >= 0.0 && val <= 1.0, $"Value {val} at index {i} is outside [0,1]");
        }
    }

    [Fact]
    public void NormalizeIndicator_DifferentSourceTypes_Work()
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
            var indicator = new NormalizeIndicator { Source = source, Period = 5 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 10, 20, 5, 15);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val) && val >= 0 && val <= 1);
        }
    }

    [Fact]
    public void NormalizeIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 1, 5, 14, 50, 100 };

        foreach (var period in periods)
        {
            var indicator = new NormalizeIndicator { Period = period };
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
}
