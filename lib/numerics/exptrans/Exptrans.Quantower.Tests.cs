using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ExptransIndicatorTests
{
    [Fact]
    public void ExptransIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ExptransIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("EXPTRANS - Exponential Function", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ExptransIndicator_MinHistoryDepths_IsOne()
    {
        var indicator = new ExptransIndicator();
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void ExptransIndicator_ShortName_IsCorrect()
    {
        var indicator = new ExptransIndicator();
        Assert.Equal("Exptrans", indicator.ShortName);
    }

    [Fact]
    public void ExptransIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new ExptransIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Exptrans", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void ExptransIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ExptransIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Exp of 0 is 1.0
        Assert.Equal(1.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void ExptransIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ExptransIndicator();
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
    public void ExptransIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ExptransIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ExptransIndicator_DifferentSourceTypes_Work()
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
            var indicator = new ExptransIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 1, 2, 0, 1);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}