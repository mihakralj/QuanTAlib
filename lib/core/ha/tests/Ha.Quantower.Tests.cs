using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class HaIndicatorTests
{
    [Fact]
    public void HaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HaIndicator();

        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HA - Heikin-Ashi", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HaIndicator_ShortName_IsHa()
    {
        var indicator = new HaIndicator();
        Assert.Equal("HA", indicator.ShortName);
    }

    [Fact]
    public void HaIndicator_MinHistoryDepths_EqualsOne()
    {
        var indicator = new HaIndicator();

        Assert.Equal(1, HaIndicator.MinHistoryDepths);
        Assert.Equal(1, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void HaIndicator_Initialize_CreatesFourLineSeries()
    {
        var indicator = new HaIndicator();
        indicator.Initialize();

        Assert.Equal(4, indicator.LinesSeries.Count);
    }

    [Fact]
    public void HaIndicator_ProcessUpdate_HistoricalBar_ComputesValues()
    {
        var indicator = new HaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 1, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        // All 4 series should have finite values
        for (int s = 0; s < 4; s++)
        {
            double val = indicator.LinesSeries[s].GetValue(0);
            Assert.True(double.IsFinite(val), $"LineSeries[{s}] should be finite");
        }
    }

    [Fact]
    public void HaIndicator_ProcessUpdate_NewBar_ComputesValues()
    {
        var indicator = new HaIndicator();
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
        Assert.Equal(2, indicator.LinesSeries[1].Count);
        Assert.Equal(2, indicator.LinesSeries[2].Count);
        Assert.Equal(2, indicator.LinesSeries[3].Count);
    }

    [Fact]
    public void HaIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new HaIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void HaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new HaIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ha.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void HaIndicator_ComputesCorrectValues()
    {
        var indicator = new HaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        // First bar: O=100, H=110, L=90, C=105
        // HA_Close = (100+110+90+105)/4 = 101.25
        // HA_Open = (100+105)/2 = 102.5 (seed)
        // HA_High = max(110, 102.5, 101.25) = 110
        // HA_Low = min(90, 102.5, 101.25) = 90
        indicator.HistoricalData.AddBar(now, 100, 110, 90, 105, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        double haOpen = indicator.LinesSeries[0].GetValue(0);
        double haHigh = indicator.LinesSeries[1].GetValue(0);
        double haLow = indicator.LinesSeries[2].GetValue(0);
        double haClose = indicator.LinesSeries[3].GetValue(0);

        Assert.Equal(102.5, haOpen, 10);
        Assert.Equal(110.0, haHigh, 10);
        Assert.Equal(90.0, haLow, 10);
        Assert.Equal(101.25, haClose, 10);
    }

    [Fact]
    public void HaIndicator_IsHotImmediately()
    {
        var indicator = new HaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102, 1000);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // All 4 series should have finite values (IsHot after first bar)
        for (int s = 0; s < 4; s++)
        {
            double val = indicator.LinesSeries[s].GetValue(0);
            Assert.True(double.IsFinite(val), $"LineSeries[{s}] should be finite after one bar");
        }
    }

    [Fact]
    public void HaIndicator_HighAlwaysAboveOrEqualLow()
    {
        var indicator = new HaIndicator();
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + (i * 2);
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 1, 1000);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        }

        double haHigh = indicator.LinesSeries[1].GetValue(0);
        double haLow = indicator.LinesSeries[2].GetValue(0);

        Assert.True(haHigh >= haLow, "HA High must be >= HA Low");
    }
}
