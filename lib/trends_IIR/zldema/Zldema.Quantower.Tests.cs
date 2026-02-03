using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class ZldemaIndicatorTests
{
    [Fact]
    public void ZldemaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ZldemaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ZLDEMA - Zero-Lag Double Exponential Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ZldemaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ZldemaIndicator { Period = 20 };

        Assert.Equal(0, ZldemaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void ZldemaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new ZldemaIndicator { Period = 15 };

        Assert.Contains("ZLDEMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ZldemaIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new ZldemaIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ZldemaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ZldemaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void ZldemaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ZldemaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ZldemaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new ZldemaIndicator { Period = 4 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        double firstValue = indicator.LinesSeries[0].GetValue(0);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewTick));
        double secondValue = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(firstValue));
        Assert.True(double.IsFinite(secondValue));
    }

    [Fact]
    public void ZldemaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new ZldemaIndicator { Period = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void ZldemaIndicator_Period_CanBeChanged()
    {
        var indicator = new ZldemaIndicator { Period = 5 };
        Assert.Equal(5, indicator.Period);

        indicator.Period = 20;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(0, ZldemaIndicator.MinHistoryDepths);
    }
}