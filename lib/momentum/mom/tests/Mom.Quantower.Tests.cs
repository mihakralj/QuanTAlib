using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class MomIndicatorTests
{
    [Fact]
    public void MomIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MomIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MOM - Momentum", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void MomIndicator_MinHistoryDepths_IsPeriodPlusOne()
    {
        var indicator = new MomIndicator { Period = 10 };
        Assert.Equal(11, indicator.MinHistoryDepths);
    }

    [Fact]
    public void MomIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new MomIndicator { Period = 5 };
        Assert.Equal("MOM(5)", indicator.ShortName);
    }

    [Fact]
    public void MomIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new MomIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("MOM", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void MomIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MomIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void MomIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MomIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MomIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new MomIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MomIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new MomIndicator();
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
            Assert.Equal(0, indicator.LinesSeries[1].GetValue(i));
        }
    }

    [Fact]
    public void MomIndicator_DifferentSourceTypes_Work()
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
            var indicator = new MomIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void MomIndicator_ShowColdValues_False_SetsNaN()
    {
        var indicator = new MomIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void MomIndicator_Uptrend_ProducesPositiveMom()
    {
        var indicator = new MomIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100 + (i * 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastMom = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastMom > 0);
    }

    [Fact]
    public void MomIndicator_Downtrend_ProducesNegativeMom()
    {
        var indicator = new MomIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 200 - (i * 5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastMom = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastMom < 0);
    }

    [Fact]
    public void MomIndicator_FlatPrices_ProducesZeroMom()
    {
        var indicator = new MomIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastMom = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastMom);
    }

    [Fact]
    public void MomIndicator_KnownMom_Correct()
    {
        var indicator = new MomIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add bar at 100
        indicator.HistoricalData.AddBar(now, 100, 100, 100, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add bar at 110 (MOM = 110 - 100 = 10)
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 110, 110, 110, 110);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double mom = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(10, mom, 5);
    }

    [Fact]
    public void MomIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 1, 5, 10, 20 };

        foreach (var period in periods)
        {
            var indicator = new MomIndicator { Period = period };
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
