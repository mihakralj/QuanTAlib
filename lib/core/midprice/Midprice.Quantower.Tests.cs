using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class MidpriceIndicatorTests
{
    [Fact]
    public void MidpriceIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MidpriceIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MIDPRICE - Midpoint Price", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void MidpriceIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new MidpriceIndicator();
        Assert.Equal("MIDPRICE(14)", indicator.ShortName);

        indicator.Period = 20;
        Assert.Equal("MIDPRICE(20)", indicator.ShortName);
    }

    [Fact]
    public void MidpriceIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new MidpriceIndicator { Period = 10 };
        Assert.Equal(10, indicator.MinHistoryDepths);
        Assert.Equal(10, ((IWatchlistIndicator)indicator).MinHistoryDepths);

        indicator.Period = 25;
        Assert.Equal(25, indicator.MinHistoryDepths);
    }

    [Fact]
    public void MidpriceIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new MidpriceIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MidpriceIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MidpriceIndicator { Period = 5 };
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
    public void MidpriceIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MidpriceIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(10), 110, 120, 100, 115, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void MidpriceIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new MidpriceIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void MidpriceIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MidpriceIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Midprice.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MidpriceIndicator_Period_CanBeChanged()
    {
        var indicator = new MidpriceIndicator();
        Assert.Equal(14, indicator.Period);

        indicator.Period = 30;
        Assert.Equal(30, indicator.Period);
    }

    [Fact]
    public void MidpriceIndicator_IsHotAfterWarmup()
    {
        var indicator = new MidpriceIndicator { Period = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 1, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}
