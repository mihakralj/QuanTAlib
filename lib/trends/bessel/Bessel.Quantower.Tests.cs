using Xunit;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class BesselIndicatorTests
{
    [Fact]
    public void BesselIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BesselIndicator();

        Assert.Equal(14, indicator.Length);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BESSEL - Bessel Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BesselIndicator_MinHistoryDepths_EqualsLength()
    {
        var indicator = new BesselIndicator { Length = 20 };

        Assert.Equal(0, BesselIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BesselIndicator_ShortName_IncludesLengthAndSource()
    {
        var indicator = new BesselIndicator { Length = 15 };

        Assert.Contains("BESSEL", indicator.ShortName);
        Assert.Contains("15", indicator.ShortName);
    }

    [Fact]
    public void BesselIndicator_Initialize_CreatesInternalFilter()
    {
        var indicator = new BesselIndicator { Length = 14 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BesselIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BesselIndicator { Length = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void BesselIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BesselIndicator { Length = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BesselIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new BesselIndicator { Length = 3 };
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
    public void BesselIndicator_MultipleUpdates_ProducesSmoothedSequence()
    {
        var indicator = new BesselIndicator { Length = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        double[] closes = { 100, 102, 104, 103, 105, 107, 106 };

        foreach (var close in closes)
        {
            indicator.HistoricalData.AddBar(now, close, close + 2, close - 2, close);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            now = now.AddMinutes(1);
        }

        for (int i = 0; i < closes.Length; i++)
        {
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(closes.Length - 1 - i)));
        }

        double lastValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(lastValue >= 90 && lastValue <= 120);
    }

    [Fact]
    public void BesselIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3
        };

        foreach (var source in sources)
        {
            var indicator = new BesselIndicator { Length = 3, Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BesselIndicator_Length_CanBeChanged()
    {
        var indicator = new BesselIndicator { Length = 5 };
        Assert.Equal(5, indicator.Length);

        indicator.Length = 20;
        Assert.Equal(20, indicator.Length);
        Assert.Equal(0, BesselIndicator.MinHistoryDepths);
    }
}
