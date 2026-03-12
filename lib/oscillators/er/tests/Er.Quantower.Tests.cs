using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class ErIndicatorTests
{
    [Fact]
    public void ErIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ErIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ER - Kaufman Efficiency Ratio", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ErIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ErIndicator { Period = 10 };

        Assert.Equal(0, ErIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ErIndicator_ShortName_IncludesParameters()
    {
        var indicator = new ErIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("ER", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ErIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ErIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Er.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ErIndicator_Initialize_CreatesInternalEr()
    {
        var indicator = new ErIndicator { Period = 10 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ErIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ErIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double value = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(value));
    }

    [Fact]
    public void ErIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ErIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ErIndicator_Parameters_CanBeChanged()
    {
        var indicator = new ErIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Equal(20, indicator.Period);
    }
}
