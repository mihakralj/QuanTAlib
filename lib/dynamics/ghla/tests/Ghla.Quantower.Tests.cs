using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class GhlaIndicatorTests
{
    [Fact]
    public void GhlaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new GhlaIndicator();

        Assert.Equal(13, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("GHLA - Gann High-Low Activator", indicator.Name);
        Assert.False(indicator.SeparateWindow); // Overlay
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void GhlaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new GhlaIndicator { Period = 5 };
        Assert.Equal("GHLA 5", indicator.ShortName);
    }

    [Fact]
    public void GhlaIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new GhlaIndicator();

        Assert.Equal(0, GhlaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void GhlaIndicator_Initialize_CreatesInternalGhla()
    {
        var indicator = new GhlaIndicator();

        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void GhlaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new GhlaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double ghlaVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(ghlaVal));
    }

    [Fact]
    public void GhlaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new GhlaIndicator { Period = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 128, 115, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void GhlaIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 3, 5, 13, 21, 50 };

        foreach (var period in periods)
        {
            var indicator = new GhlaIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double ghlaVal = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(ghlaVal), $"Period {period} should produce finite GHLA value");
        }
    }

    [Fact]
    public void GhlaIndicator_Period_CanBeChanged()
    {
        var indicator = new GhlaIndicator();
        Assert.Equal(13, indicator.Period);

        indicator.Period = 5;
        Assert.Equal(5, indicator.Period);

        indicator.Period = 21;
        Assert.Equal(21, indicator.Period);
    }

    [Fact]
    public void GhlaIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new GhlaIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void GhlaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new GhlaIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ghla.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void GhlaIndicator_HasOneLineSeries_WithCorrectName()
    {
        var indicator = new GhlaIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("GHLA", indicator.LinesSeries[0].Name);
    }

    [Fact]
    public void GhlaIndicator_IsOverlay_NotSeparateWindow()
    {
        var indicator = new GhlaIndicator();
        Assert.False(indicator.SeparateWindow);
    }
}
