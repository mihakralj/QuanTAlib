using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class EdecayIndicatorTests
{
    [Fact]
    public void EdecayIndicator_Constructor_SetsDefaults()
    {
        var indicator = new EdecayIndicator();

        Assert.Equal(5, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("EDECAY - Exponential Decay", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void EdecayIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new EdecayIndicator { Period = 20 };

        Assert.Equal(0, EdecayIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void EdecayIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new EdecayIndicator { Period = 15 };

        Assert.Contains("EDECAY", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void EdecayIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new EdecayIndicator { Period = 5 };
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void EdecayIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new EdecayIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void EdecayIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new EdecayIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EdecayIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new EdecayIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void EdecayIndicator_MultipleUpdates_ProducesCorrectSequence()
    {
        var indicator = new EdecayIndicator { Period = 5 };
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
        }
    }

    [Fact]
    public void EdecayIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new EdecayIndicator { Period = 5, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void EdecayIndicator_Period_CanBeChanged()
    {
        var indicator = new EdecayIndicator { Period = 10 };
        Assert.Equal(10, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, EdecayIndicator.MinHistoryDepths);
    }

    [Fact]
    public void EdecayIndicator_Uptrend_OutputFollowsPrice()
    {
        var indicator = new EdecayIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100 + i * 5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // In uptrend, edecay output should equal close price (input > decayed)
        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(145, lastValue, 1); // last close = 100 + 9*5 = 145
    }

    [Fact]
    public void EdecayIndicator_FlatPrices_OutputEqualsInput()
    {
        var indicator = new EdecayIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(100, lastValue, 1);
    }

    [Fact]
    public void EdecayIndicator_DifferentPeriods_Work()
    {
        var periods = new[] { 1, 5, 10, 20 };

        foreach (var period in periods)
        {
            var indicator = new EdecayIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;

            for (int i = 0; i < 10; i++)
            {
                indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            Assert.Equal(10, indicator.LinesSeries[0].Count);
        }
    }
}
