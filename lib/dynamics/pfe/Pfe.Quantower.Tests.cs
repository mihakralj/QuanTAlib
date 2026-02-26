using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class PfeIndicatorTests
{
    [Fact]
    public void PfeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new PfeIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(5, indicator.SmoothPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("PFE - Polarized Fractal Efficiency", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void PfeIndicator_ShortName_IncludesParameters()
    {
        var indicator = new PfeIndicator { Period = 20, SmoothPeriod = 8 };
        indicator.Initialize();

        Assert.Contains("PFE", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("20", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("8", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void PfeIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new PfeIndicator();

        Assert.Equal(0, PfeIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void PfeIndicator_Initialize_CreatesInternalPfe()
    {
        var indicator = new PfeIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (single PFE line)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void PfeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new PfeIndicator { Period = 5, SmoothPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double pfeVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(pfeVal));
    }

    [Fact]
    public void PfeIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new PfeIndicator { Period = 5, SmoothPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 128, 115, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void PfeIndicator_DifferentPeriods_Work()
    {
        int[][] paramSets = { new[] { 3, 2 }, new[] { 10, 5 }, new[] { 20, 8 } };

        foreach (var ps in paramSets)
        {
            var indicator = new PfeIndicator { Period = ps[0], SmoothPeriod = ps[1] };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double pfeVal = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(pfeVal), $"Periods ({ps[0]},{ps[1]}) should produce finite PFE");
        }
    }

    [Fact]
    public void PfeIndicator_Period_CanBeChanged()
    {
        var indicator = new PfeIndicator();
        Assert.Equal(10, indicator.Period);
        Assert.Equal(5, indicator.SmoothPeriod);

        indicator.Period = 20;
        indicator.SmoothPeriod = 8;
        Assert.Equal(20, indicator.Period);
        Assert.Equal(8, indicator.SmoothPeriod);
    }

    [Fact]
    public void PfeIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new PfeIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void PfeIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new PfeIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Pfe.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void PfeIndicator_HasOneLineSeries_WithCorrectName()
    {
        var indicator = new PfeIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("PFE", indicator.LinesSeries[0].Name);
    }
}
