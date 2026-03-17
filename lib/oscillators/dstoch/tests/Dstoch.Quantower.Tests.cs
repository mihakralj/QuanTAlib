using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public sealed class DstochIndicatorTests
{
    [Fact]
    public void DstochIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DstochIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DSTOCH", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DstochIndicator_MinHistoryDepths_EqualsZero()
    {
        Assert.Equal(0, DstochIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = new DstochIndicator();
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void DstochIndicator_ShortName_IsCorrect()
    {
        var indicator = new DstochIndicator();
        indicator.Initialize();

        Assert.Equal("DSTOCH 21", indicator.ShortName);
    }

    [Fact]
    public void DstochIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DstochIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dstoch.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DstochIndicator_Initialize_CreatesOneLineSeries()
    {
        var indicator = new DstochIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DstochIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DstochIndicator { Period = 5 };
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

        double dssValue = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(dssValue));
    }

    [Fact]
    public void DstochIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new DstochIndicator { Period = 5 };
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
}
