using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class StochrsiIndicatorTests
{
    [Fact]
    public void StochrsiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new StochrsiIndicator();

        Assert.Equal(14, indicator.RsiLength);
        Assert.Equal(14, indicator.StochLength);
        Assert.Equal(3, indicator.KSmooth);
        Assert.Equal(3, indicator.DSmooth);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Contains("STOCHRSI", indicator.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void StochrsiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new StochrsiIndicator();

        Assert.Equal(0, StochrsiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void StochrsiIndicator_ShortName_IncludesParameters()
    {
        var indicator = new StochrsiIndicator { RsiLength = 14, StochLength = 14, KSmooth = 3, DSmooth = 3 };
        indicator.Initialize();

        Assert.Contains("StochRSI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void StochrsiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new StochrsiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Stochrsi", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void StochrsiIndicator_Initialize_CreatesLineSeries()
    {
        var indicator = new StochrsiIndicator();
        indicator.Initialize();

        // K and D line series
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void StochrsiIndicator_ProcessUpdate_HistoricalBar_ComputesValues()
    {
        var indicator = new StochrsiIndicator { RsiLength = 5, StochLength = 5, KSmooth = 3, DSmooth = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double k = indicator.LinesSeries[0].GetValue(0);
        double d = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(k));
        Assert.True(double.IsFinite(d));
    }

    [Fact]
    public void StochrsiIndicator_ProcessUpdate_NewBar_ComputesValues()
    {
        var indicator = new StochrsiIndicator { RsiLength = 5, StochLength = 5, KSmooth = 3, DSmooth = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Simulate a new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 110, 120, 100, 115);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double k = indicator.LinesSeries[0].GetValue(0);
        double d = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(k));
        Assert.True(double.IsFinite(d));
    }

    [Fact]
    public void StochrsiIndicator_DifferentSource_Works()
    {
        var indicator = new StochrsiIndicator
        {
            RsiLength = 5,
            StochLength = 5,
            KSmooth = 3,
            DSmooth = 3,
            Source = SourceType.Open,
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (i * 0.3);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 2, price - 2, price + 1);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double k = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(k));
    }

    [Fact]
    public void StochrsiIndicator_CustomParameters_Work()
    {
        var indicator = new StochrsiIndicator
        {
            RsiLength = 7,
            StochLength = 10,
            KSmooth = 2,
            DSmooth = 5,
        };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            double price = 100.0 + (i * 0.4);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), price, price + 1, price - 1, price + 0.5);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double k = indicator.LinesSeries[0].GetValue(0);
        double d = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(k));
        Assert.True(double.IsFinite(d));
    }

    [Fact]
    public void StochrsiIndicator_ShowColdValues_Default_True()
    {
        var indicator = new StochrsiIndicator();
        Assert.True(indicator.ShowColdValues);
    }
}
