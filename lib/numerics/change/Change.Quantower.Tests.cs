using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ChangeIndicatorTests
{
    [Fact]
    public void ChangeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ChangeIndicator();

        Assert.Equal(1, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CHANGE - Percentage Change", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void ChangeIndicator_MinHistoryDepths_IsPeriodPlusOne()
    {
        var indicator = new ChangeIndicator { Period = 10 };
        Assert.Equal(11, indicator.MinHistoryDepths);
    }

    [Fact]
    public void ChangeIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new ChangeIndicator { Period = 5 };
        Assert.Equal("CHANGE(5)", indicator.ShortName);
    }

    [Fact]
    public void ChangeIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("Change", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void ChangeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void ChangeIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ChangeIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ChangeIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + i * 2,
                105 + i * 2,
                95 + i * 2,
                102 + i * 2);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
            Assert.Equal(0, indicator.LinesSeries[1].GetValue(i));
        }
    }

    [Fact]
    public void ChangeIndicator_DifferentSourceTypes_Work()
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
            var indicator = new ChangeIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void ChangeIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new ChangeIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ChangeIndicator_Uptrend_ProducesPositiveChange()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastChange = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastChange > 0);
    }

    [Fact]
    public void ChangeIndicator_Downtrend_ProducesNegativeChange()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 200 - i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastChange = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastChange < 0);
    }

    [Fact]
    public void ChangeIndicator_FlatPrices_ProducesZeroChange()
    {
        var indicator = new ChangeIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastChange = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastChange);
    }

    [Fact]
    public void ChangeIndicator_KnownChange_Correct()
    {
        var indicator = new ChangeIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bar at 100
        indicator.HistoricalData.AddBar(now, 100, 100, 100, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add bar at 110 (10% change)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 110, 110, 110, 110);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // (110 - 100) / 100 = 0.1
        double change = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0.1, change, 5);
    }
}
