using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RocIndicatorTests
{
    [Fact]
    public void RocIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RocIndicator();

        Assert.Equal(9, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ROC - Rate of Change (Absolute)", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void RocIndicator_MinHistoryDepths_IsPeriodPlusOne()
    {
        var indicator = new RocIndicator { Period = 10 };
        Assert.Equal(11, indicator.MinHistoryDepths);
    }

    [Fact]
    public void RocIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new RocIndicator { Period = 5 };
        Assert.Equal("ROC(5)", indicator.ShortName);
    }

    [Fact]
    public void RocIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new RocIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("ROC", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void RocIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RocIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void RocIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RocIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RocIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new RocIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RocIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new RocIndicator();
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
    public void RocIndicator_DifferentSourceTypes_Work()
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
            var indicator = new RocIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void RocIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new RocIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void RocIndicator_Uptrend_ProducesPositiveRoc()
    {
        var indicator = new RocIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastRoc = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastRoc > 0);
    }

    [Fact]
    public void RocIndicator_Downtrend_ProducesNegativeRoc()
    {
        var indicator = new RocIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 200 - i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastRoc = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastRoc < 0);
    }

    [Fact]
    public void RocIndicator_FlatPrices_ProducesZeroRoc()
    {
        var indicator = new RocIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastRoc = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastRoc);
    }

    [Fact]
    public void RocIndicator_KnownRoc_Correct()
    {
        var indicator = new RocIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bar at 100
        indicator.HistoricalData.AddBar(now, 100, 100, 100, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add bar at 110 (ROC = 110 - 100 = 10)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 110, 110, 110, 110);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double roc = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(10, roc, 5);
    }

    [Fact]
    public void RocIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 1, 5, 10, 20 };

        foreach (var period in periods)
        {
            var indicator = new RocIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;

            // Add enough bars
            for (int i = 0; i < period + 5; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.Equal(period + 5, indicator.LinesSeries[0].Count);
        }
    }
}
