using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class LineartransIndicatorTests
{
    [Fact]
    public void LineartransIndicator_Constructor_SetsDefaults()
    {
        var indicator = new LineartransIndicator();

        Assert.Equal(1.0, indicator.Slope);
        Assert.Equal(0.0, indicator.Intercept);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("LINEARTRANS - Linear Scaling", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void LineartransIndicator_MinHistoryDepths_IsOne()
    {
        var indicator = new LineartransIndicator();
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void LineartransIndicator_ShortName_IncludesParameters()
    {
        var indicator = new LineartransIndicator { Slope = 2.0, Intercept = 5.0 };
        Assert.Equal("LINEARTRANS(2,5)", indicator.ShortName);
    }

    [Fact]
    public void LineartransIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new LineartransIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Lineartrans", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void LineartransIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new LineartransIndicator { Slope = 2.0, Intercept = 10.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // 2 * 100 + 10 = 210
        Assert.Equal(210.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void LineartransIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new LineartransIndicator { Slope = 0.5, Intercept = -50.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 200);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        // 0.5 * 200 - 50 = 50
        Assert.Equal(50.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void LineartransIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new LineartransIndicator { Slope = 2.0, Intercept = 5.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        // NewTick recalculates same bar: 2 * 100 + 5 = 205
        Assert.Equal(205.0, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void LineartransIndicator_DifferentSourceTypes_Work()
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
            var indicator = new LineartransIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }

    [Fact]
    public void LineartransIndicator_IdentityTransform_PreservesValues()
    {
        var indicator = new LineartransIndicator { Slope = 1.0, Intercept = 0.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 42.5);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Identity transform: 1.0 * 42.5 + 0.0 = 42.5
        Assert.Equal(42.5, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }
}
