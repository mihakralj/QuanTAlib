using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class LogIndicatorTests
{
    [Fact]
    public void LogIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LogIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LOG - Natural Logarithm", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LogIndicator_MinHistoryDepths_IsOne()
    {
        var indicator = new LogIndicator();
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void LogIndicator_ShortName_IsCorrect()
    {
        var indicator = new LogIndicator();
        Assert.Equal("LOG", indicator.ShortName);
    }

    [Fact]
    public void LogIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new LogIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Log", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void LogIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LogIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Log of 100 is approximately 4.605
        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(value > 4.0 && value < 5.0);
    }

    [Fact]
    public void LogIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LogIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, Math.E);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, Math.E);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        // Log of e is 1.0
        Assert.Equal(1.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void LogIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LogIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void LogIndicator_DifferentSourceTypes_Work()
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
            var indicator = new LogIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
