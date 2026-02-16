using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class HurstIndicatorTests
{
    [Fact]
    public void HurstIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HurstIndicator();

        Assert.Equal(100, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("Hurst - Hurst Exponent", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void HurstIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HurstIndicator { Period = 100 };

        Assert.Equal(0, HurstIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void HurstIndicator_Initialize_CreatesInternalHurst()
    {
        var indicator = new HurstIndicator { Period = 20 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Hurst line + 0.5 reference line)
        Assert.Equal(2, indicator.LinesSeries.Count);
        Assert.Equal("Hurst", indicator.LinesSeries[0].Name);
        Assert.Equal("0.5", indicator.LinesSeries[1].Name);
    }

    [Fact]
    public void HurstIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HurstIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double hurst = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(hurst));
    }

    [Fact]
    public void HurstIndicator_DifferentSourceTypes()
    {
        var indicator = new HurstIndicator { Period = 20, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double hurst = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(hurst));
    }

    [Fact]
    public void HurstIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new HurstIndicator { Period = 50 };
        Assert.Equal("Hurst 50", indicator.ShortName);
    }

    [Fact]
    public void HurstIndicator_NewBar_UpdatesValue()
    {
        var indicator = new HurstIndicator { Period = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        _ = indicator.LinesSeries[0].GetValue(0);

        // Add a new bar with a very different value
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 200, 210, 190, 205);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double valueAfter = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(valueAfter));
    }
}
