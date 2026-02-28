using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class Oc2IndicatorTests
{
    [Fact]
    public void Oc2Indicator_Constructor_SetsDefaults()
    {
        var indicator = new MidbodyIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MIDBODY - Open-Close Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void Oc2Indicator_ShortName_IsOc2()
    {
        var indicator = new MidbodyIndicator();
        Assert.Equal("MIDBODY", indicator.ShortName);
    }

    [Fact]
    public void Oc2Indicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new MidbodyIndicator();

        Assert.Equal(1, MidbodyIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void Oc2Indicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new MidbodyIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Oc2Indicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MidbodyIndicator();
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
    public void Oc2Indicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MidbodyIndicator();
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
    public void Oc2Indicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new MidbodyIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void Oc2Indicator_SourceCodeLink_IsValid()
    {
        var indicator = new MidbodyIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Midbody.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void Oc2Indicator_ComputesCorrectOc2()
    {
        var indicator = new MidbodyIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // O=100, C=105 → (100+105)/2 = 102.5
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(102.5, val, 10);
    }

    [Fact]
    public void Oc2Indicator_IsHotImmediately()
    {
        var indicator = new MidbodyIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}



