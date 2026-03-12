using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class HoltIndicatorTests
{
    [Fact]
    public void HoltIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HoltIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(0, indicator.Gamma);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HOLT - Holt Exponential Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HoltIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HoltIndicator { Period = 20 };

        Assert.Equal(0, HoltIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HoltIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new HoltIndicator { Period = 15 };

        Assert.Contains("HOLT", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void HoltIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new HoltIndicator { Period = 10, Gamma = 0.3 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void HoltIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HoltIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void HoltIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new HoltIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }
}
