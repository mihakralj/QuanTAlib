using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class RvgiIndicatorTests
{
    [Fact]
    public void RvgiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RvgiIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RVGI - Relative Vigor Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RvgiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RvgiIndicator { Period = 10 };

        Assert.Equal(0, RvgiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void RvgiIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new RvgiIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Contains("RVGI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RvgiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new RvgiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Rvgi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void RvgiIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new RvgiIndicator { Period = 10 };
        indicator.Initialize();

        // RVGI line + Signal line
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void RvgiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RvgiIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double rvgiValue = indicator.LinesSeries[0].GetValue(0);
        double signalValue = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(rvgiValue));
        Assert.True(double.IsFinite(signalValue));
    }

    [Fact]
    public void RvgiIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new RvgiIndicator { Period = 5 };
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
    public void RvgiIndicator_Parameters_CanBeChanged()
    {
        var indicator = new RvgiIndicator { Period = 14 };
        indicator.Initialize();

        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void RvgiIndicator_DifferentOhlcSource_ComputesValues()
    {
        var indicator = new RvgiIndicator { Period = 10 };
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

    [Fact]
    public void RvgiIndicator_BullishBars_ParallelOutput_Positive()
    {
        // Persistent up bars → RVGI line should be positive
        var indicator = new RvgiIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100.0 + i;
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                open: basePrice,
                high: basePrice + 4.0,
                low: basePrice - 1.0,
                close: basePrice + 3.0);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double rvgiValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(rvgiValue > 0.0, $"Expected RVGI line > 0 for bullish bars, got {rvgiValue}");
    }
}
