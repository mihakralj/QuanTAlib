using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class MinindexIndicatorTests
{
    [Fact]
    public void MinindexIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MinindexIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MININDEX - Rolling Minimum Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void MinindexIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new MinindexIndicator { Period = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        Assert.Equal(20, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void MinindexIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new MinindexIndicator { Period = 10 };

        Assert.Equal("MININDEX(10)", indicator.ShortName);
    }

    [Fact]
    public void MinindexIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new MinindexIndicator { Period = 5 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MinindexIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MinindexIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void MinindexIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MinindexIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MinindexIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new MinindexIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MinindexIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new MinindexIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                100 + (i * 2),
                105 + (i * 2),
                95 + (i * 2),
                102 + (i * 2));
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        Assert.Equal(20, indicator.LinesSeries[0].Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(i)));
        }
    }

    [Fact]
    public void MinindexIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new MinindexIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void MinindexIndicator_Period_CanBeChanged()
    {
        var indicator = new MinindexIndicator { Period = 10 };
        Assert.Equal(10, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void MinindexIndicator_Downtrend_MinAtCurrentBar()
    {
        // In a monotonic downtrend, the min is always the current bar (bars-ago = 0)
        var indicator = new MinindexIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 200 - (i * 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastIndex = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastIndex); // Min is at current bar → bars-ago = 0
    }

    [Fact]
    public void MinindexIndicator_Uptrend_MinAtOldestBar()
    {
        // In a monotonic uptrend, the min is the oldest bar (bars-ago = period-1)
        var indicator = new MinindexIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100 + (i * 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastIndex = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(4, lastIndex); // Min is at oldest bar → bars-ago = period-1 = 4
    }

    [Fact]
    public void MinindexIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new MinindexIndicator { Period = 10, ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void MinindexIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 2, 5, 10, 20 };

        foreach (var period in periods)
        {
            var indicator = new MinindexIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;

            for (int i = 0; i < period + 5; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.Equal(period + 5, indicator.LinesSeries[0].Count);
        }
    }
}
