using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class MarketfiIndicatorTests
{
    [Fact]
    public void MarketfiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MarketfiIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MARKETFI - Market Facilitation Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MarketfiIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new MarketfiIndicator();

        Assert.Equal(0, MarketfiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MarketfiIndicator_ShortName_IsCorrect()
    {
        var indicator = new MarketfiIndicator();
        indicator.Initialize();

        Assert.Equal("MARKETFI", indicator.ShortName);
    }

    [Fact]
    public void MarketfiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MarketfiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Marketfi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketfiIndicator_Initialize_CreatesOneLineSeries()
    {
        var indicator = new MarketfiIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MarketfiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MarketfiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100.0 + i;
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                open: basePrice,
                high: basePrice + 5.0,
                low: basePrice - 5.0,
                close: basePrice + 1.0);

            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double mfiValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(mfiValue));
    }

    [Fact]
    public void MarketfiIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new MarketfiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.True(indicator.LinesSeries[0].Count >= 2);
    }

    [Fact]
    public void MarketfiIndicator_IsHot_AfterFirstBar()
    {
        var indicator = new MarketfiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 100);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // MARKETFI has no warmup — IsHot from bar 1
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}
