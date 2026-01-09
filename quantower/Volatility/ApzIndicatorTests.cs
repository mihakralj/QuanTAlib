using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class ApzIndicatorTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var indicator = new ApzIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("APZ - Adaptive Price Zone", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MinHistoryDepths_IsCorrect()
    {
        var indicator = new ApzIndicator { Period = 25 };
        Assert.Equal(25, indicator.MinHistoryDepths);
    }

    [Fact]
    public void ShortName_IncludesParameters()
    {
        var indicator = new ApzIndicator { Period = 15, Multiplier = 1.5 };
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("1.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_CreatesThreeLineSeries()
    {
        var indicator = new ApzIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Equal(3, indicator.LinesSeries.Count);
        Assert.Equal("Middle", indicator.LinesSeries[0].Name);
        Assert.Equal("Upper", indicator.LinesSeries[1].Name);
        Assert.Equal("Lower", indicator.LinesSeries[2].Name);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ApzIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[2].GetValue(0)));
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ApzIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ApzIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new ApzIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        Assert.Equal(10, indicator.LinesSeries[0].Count);
        Assert.Equal(10, indicator.LinesSeries[1].Count);
        Assert.Equal(10, indicator.LinesSeries[2].Count);

        // All values should be finite
        for (int i = 0; i < 10; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(i)));
            Assert.True(double.IsFinite(indicator.LinesSeries[2].GetValue(i)));
        }
    }

    [Fact]
    public void BandRelationship_UpperAboveLowerBelowMiddle()
    {
        var indicator = new ApzIndicator { Period = 5, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 1000);
            indicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        // After warmup, upper > middle > lower
        double middle = indicator.LinesSeries[0].GetValue(0);
        double upper = indicator.LinesSeries[1].GetValue(0);
        double lower = indicator.LinesSeries[2].GetValue(0);

        Assert.True(upper > middle, $"Upper ({upper}) should be > Middle ({middle})");
        Assert.True(lower < middle, $"Lower ({lower}) should be < Middle ({middle})");
    }

    [Fact]
    public void Multiplier_AffectsBandWidth()
    {
        var now = DateTime.UtcNow;

        // Narrow bands with multiplier 1.0
        var narrowIndicator = new ApzIndicator { Period = 5, Multiplier = 1.0 };
        narrowIndicator.Initialize();

        // Wide bands with multiplier 3.0
        var wideIndicator = new ApzIndicator { Period = 5, Multiplier = 3.0 };
        wideIndicator.Initialize();

        for (int i = 0; i < 10; i++)
        {
            narrowIndicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 1000);
            narrowIndicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));

            wideIndicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 110, 90, 105, 1000);
            wideIndicator.ProcessUpdate(new UpdateArgs(i == 0 ? UpdateReason.HistoricalBar : UpdateReason.NewBar));
        }

        double narrowWidth = narrowIndicator.LinesSeries[1].GetValue(0) - narrowIndicator.LinesSeries[2].GetValue(0);
        double wideWidth = wideIndicator.LinesSeries[1].GetValue(0) - wideIndicator.LinesSeries[2].GetValue(0);

        Assert.True(wideWidth > narrowWidth, $"Wide bands ({wideWidth}) should be wider than narrow bands ({narrowWidth})");
    }

    [Fact]
    public void Period_CanBeChanged()
    {
        var indicator = new ApzIndicator { Period = 10 };
        Assert.Equal(10, indicator.Period);

        indicator.Period = 30;
        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void Multiplier_CanBeChanged()
    {
        var indicator = new ApzIndicator { Multiplier = 2.0 };
        Assert.Equal(2.0, indicator.Multiplier);

        indicator.Multiplier = 3.5;
        Assert.Equal(3.5, indicator.Multiplier);
    }
}
