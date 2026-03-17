using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class BwMfiIndicatorTests
{
    [Fact]
    public void BwMfiIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BwMfiIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BW_MFI - Bill Williams Market Facilitation Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BwMfiIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new BwMfiIndicator();

        Assert.Equal(1, BwMfiIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(1, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BwMfiIndicator_ShortName_IsCorrect()
    {
        var indicator = new BwMfiIndicator();
        indicator.Initialize();

        Assert.Equal("BW_MFI", indicator.ShortName);
    }

    [Fact]
    public void BwMfiIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BwMfiIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("BwMfi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void BwMfiIndicator_Initialize_CreatesTwoLineSeries()
    {
        var indicator = new BwMfiIndicator();
        indicator.Initialize();

        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void BwMfiIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BwMfiIndicator();
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
    public void BwMfiIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new BwMfiIndicator();
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
    public void BwMfiIndicator_ZoneLine_HasValues()
    {
        var indicator = new BwMfiIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            double basePrice = 100.0 + i;
            indicator.HistoricalData.AddBar(
                now.AddMinutes(i),
                open: basePrice,
                high: basePrice + 5.0 + i,
                low: basePrice - 5.0,
                close: basePrice + 1.0);

            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double zoneValue = indicator.LinesSeries[1].GetValue(0);
        Assert.True(double.IsFinite(zoneValue));
    }
}
