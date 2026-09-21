using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class BrarIndicatorTests
{
    [Fact]
    public void BrarIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BrarIndicator();

        Assert.Equal(26, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BRAR - Bull-Bear Power Ratio", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BrarIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BrarIndicator { Period = 26 };

        Assert.Equal(0, BrarIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BrarIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new BrarIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Contains("BRAR", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BrarIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BrarIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Brar.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BrarIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new BrarIndicator { Period = 26 };
        indicator.Initialize();

        // BR line + AR line
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void BrarIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BrarIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double brValue = indicator.LinesSeries[0].GetValue(0);
        double arValue = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(brValue));
        Assert.True(double.IsFinite(arValue));
    }

    [Fact]
    public void BrarIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new BrarIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        // After new bar, series should have grown
        Assert.True(indicator.LinesSeries[0].Count >= 2);
    }

    [Fact]
    public void BrarIndicator_Parameters_CanBeChanged()
    {
        var indicator = new BrarIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void BrarIndicator_DifferentOhlcSource_ComputesValues()
    {
        var indicator = new BrarIndicator { Period = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100.0 + (i * 0.5);
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                open: basePrice,
                high: basePrice + 3.0,
                low: basePrice - 2.0,
                close: basePrice + 1.0);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // Both lines should have finite values
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
        Assert.True(double.IsFinite(indicator.LinesSeries[1].GetValue(0)));
    }
}
