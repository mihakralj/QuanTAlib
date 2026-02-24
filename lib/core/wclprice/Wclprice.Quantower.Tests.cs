using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class WclpriceIndicatorTests
{
    [Fact]
    public void WclpriceIndicator_Constructor_SetsDefaults()
    {
        var indicator = new WclpriceIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("WCLPRICE - Weighted Close Price", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void WclpriceIndicator_ShortName_IsWclprice()
    {
        var indicator = new WclpriceIndicator();
        Assert.Equal("WCLPRICE", indicator.ShortName);
    }

    [Fact]
    public void WclpriceIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new WclpriceIndicator();

        Assert.Equal(1, WclpriceIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void WclpriceIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new WclpriceIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void WclpriceIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new WclpriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 1, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void WclpriceIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new WclpriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 115, 105, 112, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void WclpriceIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new WclpriceIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void WclpriceIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new WclpriceIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Wclprice.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void WclpriceIndicator_ComputesCorrectWeightedClose()
    {
        var indicator = new WclpriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // H=110, L=90, C=105 → (110+90+2*105)/4 = (110+90+210)/4 = 410/4 = 102.5
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(102.5, val, 10);
    }

    [Fact]
    public void WclpriceIndicator_IsHotImmediately()
    {
        var indicator = new WclpriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}
