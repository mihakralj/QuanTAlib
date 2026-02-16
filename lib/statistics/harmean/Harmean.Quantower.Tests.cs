using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class HarmeanIndicatorTests
{
    [Fact]
    public void HarmeanIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HarmeanIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HARMEAN - Harmonic Mean", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void HarmeanIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HarmeanIndicator { Period = 14 };

        Assert.Equal(0, HarmeanIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void HarmeanIndicator_Initialize_CreatesInternalHarmean()
    {
        var indicator = new HarmeanIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
        Assert.Equal("Harmean", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void HarmeanIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HarmeanIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double harmean = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(harmean));
        Assert.True(harmean > 0, $"Harmonic mean should be positive, got {harmean}");
    }

    [Fact]
    public void HarmeanIndicator_DifferentSourceTypes()
    {
        var indicator = new HarmeanIndicator { Period = 5, Source = SourceType.Open };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double harmean = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(harmean));
        Assert.True(harmean > 0);
    }

    [Fact]
    public void HarmeanIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new HarmeanIndicator { Period = 20 };
        Assert.Equal("Harmean 20", indicator.ShortName);
    }

    [Fact]
    public void HarmeanIndicator_NewBar_UpdatesValue()
    {
        var indicator = new HarmeanIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;

        // Add enough bars to warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        _ = indicator.LinesSeries[0].GetValue(0);

        // Add a new bar with a very different value
        indicator.HistoricalData.AddBar(now.AddMinutes(10), 200, 210, 190, 205);
        var newArgs = new UpdateArgs(UpdateReason.NewBar);
        indicator.ProcessUpdate(newArgs);

        double valueAfter = indicator.LinesSeries[0].GetValue(0);

        // Value should change after adding a significantly different bar
        Assert.True(double.IsFinite(valueAfter));
        Assert.True(valueAfter > 0);
    }
}
