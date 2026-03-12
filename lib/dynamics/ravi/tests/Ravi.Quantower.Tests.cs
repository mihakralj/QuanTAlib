using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class RaviIndicatorTests
{
    [Fact]
    public void RaviIndicator_Constructor_SetsDefaults()
    {
        var indicator = new RaviIndicator();

        Assert.Equal(7, indicator.ShortPeriod);
        Assert.Equal(65, indicator.LongPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("RAVI - Chande Range Action Verification Index", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void RaviIndicator_ShortName_IncludesParameters()
    {
        var indicator = new RaviIndicator { ShortPeriod = 5, LongPeriod = 50 };
        indicator.Initialize();

        Assert.Contains("RAVI", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("50", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void RaviIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new RaviIndicator();

        Assert.Equal(0, RaviIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void RaviIndicator_Initialize_CreatesInternalRavi()
    {
        var indicator = new RaviIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (single RAVI line)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void RaviIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new RaviIndicator { ShortPeriod = 3, LongPeriod = 10 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        double raviVal = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(raviVal));
        Assert.True(raviVal >= 0);
    }

    [Fact]
    public void RaviIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new RaviIndicator { ShortPeriod = 3, LongPeriod = 10 };
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
    public void RaviIndicator_DifferentPeriods_Work()
    {
        int[][] paramSets = { new[] { 3, 10 }, new[] { 5, 20 }, new[] { 7, 65 } };

        foreach (var ps in paramSets)
        {
            var indicator = new RaviIndicator { ShortPeriod = ps[0], LongPeriod = ps[1] };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double raviVal = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(raviVal), $"Periods ({ps[0]},{ps[1]}) should produce finite RAVI");
        }
    }

    [Fact]
    public void RaviIndicator_Period_CanBeChanged()
    {
        var indicator = new RaviIndicator();
        Assert.Equal(7, indicator.ShortPeriod);
        Assert.Equal(65, indicator.LongPeriod);

        indicator.ShortPeriod = 5;
        indicator.LongPeriod = 50;
        Assert.Equal(5, indicator.ShortPeriod);
        Assert.Equal(50, indicator.LongPeriod);
    }

    [Fact]
    public void RaviIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new RaviIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void RaviIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new RaviIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ravi.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void RaviIndicator_HasOneLineSeries_WithCorrectName()
    {
        var indicator = new RaviIndicator();
        indicator.Initialize();

        Assert.Single(indicator.LinesSeries);
        Assert.Equal("RAVI", indicator.LinesSeries[0].Name);
    }
}
