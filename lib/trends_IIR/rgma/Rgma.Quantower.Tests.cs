using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RgmaIndicatorTests
{
    [Fact]
    public void RgmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RgmaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(3, indicator.Passes);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RGMA - Recursive Gaussian Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RgmaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RgmaIndicator { Period = 10 };

        Assert.Equal(0, RgmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RgmaIndicator_ShortName_IncludesParametersAndSource()
    {
        var indicator = new RgmaIndicator { Period = 15, Passes = 4, Source = SourceType.HLC3 };

        Assert.Contains("RGMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("4", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("HLC3", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RgmaIndicator_Initialize_CreatesInternalRgma()
    {
        var indicator = new RgmaIndicator { Period = 10, Passes = 3 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RgmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RgmaIndicator { Period = 10, Passes = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void RgmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RgmaIndicator { Period = 10, Passes = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 112, 98, 110);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RgmaIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new RgmaIndicator { Period = 10, Passes = 3 };
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
    public void RgmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new RgmaIndicator { Source = source, Period = 10, Passes = 3 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }
}

