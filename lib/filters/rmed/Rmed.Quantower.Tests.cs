using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Quantower.Tests;

public class RmedIndicatorTests
{
    [Fact]
    public void RmedIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RmedIndicator();

        Assert.Equal(12, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RMED - Ehlers Recursive Median Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RmedIndicator_MinHistoryDepths_EqualsExpectedValue()
    {
        var indicator = new RmedIndicator { Period = 20 };

        Assert.Equal(12, RmedIndicator.MinHistoryDepths);
        Assert.Equal(12, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RmedIndicator_ShortName_IncludesParametersAndSource()
    {
        var indicator = new RmedIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("RMED", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RmedIndicator_Initialize_CreatesInternalRmed()
    {
        var indicator = new RmedIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RmedIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RmedIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void RmedIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RmedIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void RmedIndicator_ProcessUpdate_NewTick_ProcessesWithoutError()
    {
        var indicator = new RmedIndicator { Period = 5 };
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
    public void RmedIndicator_DifferentSourceTypes()
    {
        foreach (var sourceType in new[] { SourceType.Open, SourceType.High, SourceType.Low, SourceType.Close })
        {
            var indicator = new RmedIndicator { Period = 5, Source = sourceType };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

            Assert.Equal(1, indicator.LinesSeries[0].Count);
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        }
    }
}
