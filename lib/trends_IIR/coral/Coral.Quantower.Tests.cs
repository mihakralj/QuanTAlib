using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class CoralIndicatorTests
{
    [Fact]
    public void CoralIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CoralIndicator();

        Assert.Equal(21, indicator.Period);
        Assert.Equal(0.4, indicator.Cd);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("CORAL - Coral Trend Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void CoralIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new CoralIndicator { Period = 20 };

        Assert.Equal(0, CoralIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void CoralIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new CoralIndicator { Period = 15 };

        Assert.Contains("CORAL", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CoralIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new CoralIndicator { Period = 10, Cd = 0.5 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CoralIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CoralIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }

    [Fact]
    public void CoralIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new CoralIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }
}
