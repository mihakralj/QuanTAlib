using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class TyppriceIndicatorTests
{
    [Fact]
    public void TyppriceIndicator_Constructor_SetsDefaults()
    {
        var indicator = new TyppriceIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("TYPPRICE - Typical Price", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void TyppriceIndicator_ShortName_IsTypprice()
    {
        var indicator = new TyppriceIndicator();
        Assert.Equal("TYPPRICE", indicator.ShortName);
    }

    [Fact]
    public void TyppriceIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new TyppriceIndicator();

        Assert.Equal(1, TyppriceIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void TyppriceIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new TyppriceIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void TyppriceIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new TyppriceIndicator();
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
    public void TyppriceIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new TyppriceIndicator();
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
    public void TyppriceIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new TyppriceIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void TyppriceIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new TyppriceIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Typprice.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void TyppriceIndicator_ComputesCorrectTypicalPrice()
    {
        var indicator = new TyppriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // O=100, H=110, L=90, C=105 → (100+110+90)/3 = 100.0
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(300.0 * (1.0 / 3.0), val, 10);
    }

    [Fact]
    public void TyppriceIndicator_IsHotImmediately()
    {
        var indicator = new TyppriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}
