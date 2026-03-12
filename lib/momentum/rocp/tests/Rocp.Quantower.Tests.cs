using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Tests;

public class RocpIndicatorTests
{
    [Fact]
    public void Constructor_InitializesDefaults()
    {
        var indicator = new RocpIndicator();
        Assert.Equal(9, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ROCP - Rate of Change Percentage", indicator.Name);
        Assert.Contains("100 × (current - past) / past", indicator.Description, StringComparison.Ordinal);
        Assert.True(indicator.SeparateWindow);
        Assert.False(indicator.OnBackGround);
    }

    [Fact]
    public void ShortName_ReflectsPeriod()
    {
        var indicator = new RocpIndicator { Period = 14 };
        Assert.Equal("ROCP(14)", indicator.ShortName);
    }

    [Fact]
    public void MinHistoryDepths_IsPeriodPlusOne()
    {
        var indicator = new RocpIndicator { Period = 9 };
        Assert.Equal(10, indicator.MinHistoryDepths);
    }

    [Fact]
    public void MinHistoryDepths_MatchesWatchlistInterface()
    {
        var indicator = new RocpIndicator { Period = 17 };
        Assert.Equal(18, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void Period_CanBeSet()
    {
        var indicator = new RocpIndicator { Period = 20 };
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void Source_CanBeSet()
    {
        var indicator = new RocpIndicator { Source = SourceType.Open };
        Assert.Equal(SourceType.Open, indicator.Source);
    }

    [Fact]
    public void ShowColdValues_CanBeSet()
    {
        var indicator = new RocpIndicator { ShowColdValues = false };
        Assert.False(indicator.ShowColdValues);
    }

    [Fact]
    public void Initialize_CreatesLineSeries()
    {
        var indicator = new RocpIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("ROCP", indicator.LinesSeries[0].Name);
        Assert.Equal("Zero", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RocpIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.Equal(1, indicator.LinesSeries[1].Count);
    }

    [Fact]
    public void ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RocpIndicator();
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
        var indicator = new RocpIndicator();
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
        var indicator = new RocpIndicator();
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
    public void DifferentSourceTypes_Work()
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
            var indicator = new RocpIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
        }
    }

    [Fact]
    public void ShowColdValues_False_SetsNaN()
    {
        var indicator = new RocpIndicator { ShowColdValues = false };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.True(double.IsNaN(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void Uptrend_ProducesPositiveRocp()
    {
        var indicator = new RocpIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastRocp = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastRocp > 0);
    }

    [Fact]
    public void Downtrend_ProducesNegativeRocp()
    {
        var indicator = new RocpIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 200 - i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastRocp = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastRocp < 0);
    }

    [Fact]
    public void FlatPrices_ProducesZeroRocp()
    {
        var indicator = new RocpIndicator { Period = 1 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastRocp = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(0, lastRocp);
    }

    [Fact]
    public void DifferentPeriods_Work()
    {
        var periods = new[] { 1, 5, 10, 20 };

        foreach (var period in periods)
        {
            var indicator = new RocpIndicator { Period = period };
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
