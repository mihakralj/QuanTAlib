using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class MedpriceIndicatorTests
{
    [Fact]
    public void MedpriceIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MedpriceIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("MEDPRICE - Median Price", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void MedpriceIndicator_ShortName_IsMedprice()
    {
        var indicator = new MedpriceIndicator();
        Assert.Equal("MEDPRICE", indicator.ShortName);
    }

    [Fact]
    public void MedpriceIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new MedpriceIndicator();

        Assert.Equal(1, MedpriceIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void MedpriceIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new MedpriceIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void MedpriceIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MedpriceIndicator();
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
    public void MedpriceIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new MedpriceIndicator();
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
    public void MedpriceIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new MedpriceIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void MedpriceIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MedpriceIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Medprice.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MedpriceIndicator_ComputesCorrectMedian()
    {
        var indicator = new MedpriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // H=110, L=90 → (110+90)/2 = 100.0
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.Equal(100.0, val, 10);
    }

    [Fact]
    public void MedpriceIndicator_IsHotImmediately()
    {
        var indicator = new MedpriceIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }
}
