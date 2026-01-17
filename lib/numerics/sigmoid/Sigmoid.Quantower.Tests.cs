using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class SigmoidIndicatorTests
{
    [Fact]
    public void SigmoidIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SigmoidIndicator();

        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.Equal(1.0, indicator.Steepness);
        Assert.Equal(0.0, indicator.Midpoint);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SIGMOID - Logistic Function", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SigmoidIndicator_MinHistoryDepths_IsOne()
    {
        var indicator = new SigmoidIndicator();
        Assert.Equal(1, indicator.MinHistoryDepths);
    }

    [Fact]
    public void SigmoidIndicator_ShortName_IncludesParameters()
    {
        var indicator = new SigmoidIndicator { Steepness = 2.0, Midpoint = 50.0 };
        Assert.Equal("SIGMOID(2.00,50.00)", indicator.ShortName);
    }

    [Fact]
    public void SigmoidIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new SigmoidIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Sigmoid", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void SigmoidIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SigmoidIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Sigmoid of 0 with default params is 0.5
        Assert.Equal(0.5, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void SigmoidIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SigmoidIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 0, 2, -1, 1);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
        // Sigmoid of 1 is about 0.731
        double expected = 1.0 / (1.0 + Math.Exp(-1.0));
        Assert.Equal(expected, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void SigmoidIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new SigmoidIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 0, 1, -1, 0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void SigmoidIndicator_CustomParameters_AreApplied()
    {
        var indicator = new SigmoidIndicator
        {
            Steepness = 2.0,
            Midpoint = 50.0
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 50, 51, 49, 50);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Sigmoid at midpoint should be 0.5
        Assert.Equal(0.5, indicator.LinesSeries[0].GetValue(0), 1e-10);
    }

    [Fact]
    public void SigmoidIndicator_DifferentSourceTypes_Work()
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
            var indicator = new SigmoidIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 1, 2, 0, 1);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            double val = indicator.LinesSeries[0].GetValue(0);
            // All outputs should be in (0, 1)
            Assert.True(val > 0 && val < 1);
        }
    }

    [Fact]
    public void SigmoidIndicator_OutputAlwaysInRange()
    {
        var indicator = new SigmoidIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // Test with large positive and negative values
        double[] testValues = { -1000, -100, -10, -1, 0, 1, 10, 100, 1000 };

        foreach (var val in testValues)
        {
            indicator.HistoricalData.AddBar(now, val, val + 1, val - 1, val);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

            double output = indicator.LinesSeries[0].GetValue(0);
            Assert.True(output > 0 && output < 1, $"Sigmoid({val}) = {output} should be in (0,1)");

            now = now.AddMinutes(1);
        }
    }
}
