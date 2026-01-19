using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ReluIndicatorTests
{
    [Fact]
    public void ReluIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ReluIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RELU - Rectified Linear Unit", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ReluIndicator_MinHistoryDepths_IsOne()
    {
        var indicator = new ReluIndicator();
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void ReluIndicator_ShortName_IsCorrect()
    {
        var indicator = new ReluIndicator();
        Assert.Equal("RELU", indicator.ShortName);
    }

    [Fact]
    public void ReluIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new ReluIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("ReLU", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void ReluIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ReluIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Close = -5 (negative value should become 0)
        indicator.HistoricalData.AddBar(now, 0, 1, -10, -5);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // ReLU of -5 is 0
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void ReluIndicator_ProcessUpdate_PositiveValue_PassesThrough()
    {
        var indicator = new ReluIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Close = 10 (positive value should pass through)
        indicator.HistoricalData.AddBar(now, 0, 15, 5, 10);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // ReLU of 10 is 10
        Assert.Equal(10.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void ReluIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ReluIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, -2);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 0, 5, 0, 3);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        // ReLU of 3 is 3
        Assert.Equal(3.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void ReluIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ReluIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ReluIndicator_ProcessUpdate_ZeroValue_ReturnsZero()
    {
        var indicator = new ReluIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // ReLU of 0 is 0
        Assert.Equal(0.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void ReluIndicator_DifferentSourceTypes_Work()
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
            var indicator = new ReluIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 1, 2, 0, 1);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
