using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class SqueezeIndicatorTests
{
    [Fact]
    public void SqueezeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SqueezeIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.0, indicator.BbMult);
        Assert.Equal(1.5, indicator.KcMult);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SQUEEZE", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void SqueezeIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new SqueezeIndicator { Period = 20, BbMult = 2.0, KcMult = 1.5 };

        Assert.Equal(0, SqueezeIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void SqueezeIndicator_ShortName_IncludesParameters()
    {
        var indicator = new SqueezeIndicator { Period = 20, BbMult = 2.0, KcMult = 1.5 };
        indicator.Initialize();

        Assert.Contains("SQUEEZE", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void SqueezeIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new SqueezeIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Squeeze", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void SqueezeIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new SqueezeIndicator { Period = 20, BbMult = 2.0, KcMult = 1.5 };
        indicator.Initialize();

        // Momentum + SqueezeOn
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void SqueezeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SqueezeIndicator { Period = 5, BbMult = 2.0, KcMult = 1.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double mom = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(mom));
    }

    [Fact]
    public void SqueezeIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SqueezeIndicator { Period = 5, BbMult = 2.0, KcMult = 1.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 112, 108, 111);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double mom = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(mom));
    }

    [Fact]
    public void SqueezeIndicator_DifferentOhlcSources_Supported()
    {
        var indicator = new SqueezeIndicator { Period = 5, BbMult = 2.0, KcMult = 1.5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
        {
            double price = 50.0 + i * 0.5;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 0.5, price - 0.5, price + 0.1);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}
