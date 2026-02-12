using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class BbbIndicatorTests
{
    [Fact]
    public void BbbIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BbbIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.0, indicator.Multiplier);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BBB - Bollinger %B", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BbbIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BbbIndicator { Period = 20 };

        Assert.Equal(0, BbbIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BbbIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BbbIndicator { Period = 10, Multiplier = 2.5 };
        indicator.Initialize();

        Assert.Contains("BBB", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BbbIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BbbIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Bbb.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BbbIndicator_Initialize_CreatesInternalBbb()
    {
        var indicator = new BbbIndicator { Period = 20, Multiplier = 2.0 };

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BbbIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BbbIndicator { Period = 5, Multiplier = 2.0 };
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
    public void BbbIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BbbIndicator { Period = 5, Multiplier = 2.0 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BbbIndicator_Parameters_CanBeChanged()
    {
        var indicator = new BbbIndicator { Period = 20, Multiplier = 2.0 };

        indicator.Period = 10;
        indicator.Multiplier = 1.5;

        Assert.Equal(10, indicator.Period);
        Assert.Equal(1.5, indicator.Multiplier);
        Assert.Equal(0, BbbIndicator.MinHistoryDepths);
    }
}
