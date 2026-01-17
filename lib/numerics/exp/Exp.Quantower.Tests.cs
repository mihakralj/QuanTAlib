using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ExpIndicatorTests
{
    [Fact]
    public void ExpIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ExpIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("EXP - Exponential Function", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ExpIndicator_MinHistoryDepths_IsOne()
    {
        var indicator = new ExpIndicator();
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void ExpIndicator_ShortName_IsCorrect()
    {
        var indicator = new ExpIndicator();
        Assert.Equal("EXP", indicator.ShortName);
    }

    [Fact]
    public void ExpIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new ExpIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Exp", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void ExpIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ExpIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Exp of 0 is 1.0
        Assert.Equal(1.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void ExpIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ExpIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 1);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 0, 1, -1, 1);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        // Exp of 1 is e (~2.718)
        Assert.Equal(Math.E, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void ExpIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ExpIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ExpIndicator_DifferentSourceTypes_Work()
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
            var indicator = new ExpIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 1, 2, 0, 1);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
