using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class BbwIndicatorTests
{
    [Fact]
    public void BbwIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BbwIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.0, indicator.Multiplier);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BBW - Bollinger Band Width", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BbwIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BbwIndicator { Period = 14, Multiplier = 2.5 };
        Assert.Contains("BBW", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BbwIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BbwIndicator();

        Assert.Equal(0, BbwIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BbwIndicator_Initialize_CreatesInternalBbw()
    {
        var indicator = new BbwIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BbwIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BbwIndicator { Period = 5 };
        indicator.Initialize();

        // Add historical data with volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + (i * 2) + (i % 2 == 0 ? 5 : -5); // Add some volatility
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0); // BBW should be non-negative
    }

    [Fact]
    public void BbwIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BbwIndicator { Period = 5 };
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
    public void BbwIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new BbwIndicator { Period = period };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 60; i++)
            {
                double basePrice = 100 + i + (i % 3 == 0 ? 10 : -5); // Add volatility
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Period {period} should produce finite value");
            Assert.True(val >= 0, $"Period {period} should produce non-negative BBW");
        }
    }

    [Fact]
    public void BbwIndicator_DifferentMultipliers_Work()
    {
        double[] multipliers = { 1.0, 1.5, 2.0, 2.5, 3.0 };

        foreach (var multiplier in multipliers)
        {
            var indicator = new BbwIndicator { Multiplier = multiplier };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 30; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Multiplier {multiplier} should produce finite value");
            Assert.True(val >= 0, $"Multiplier {multiplier} should produce non-negative BBW");
        }
    }

    [Fact]
    public void BbwIndicator_DifferentSourceTypes_Work()
    {
        SourceType[] sources = { SourceType.Close, SourceType.High, SourceType.Low, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BbwIndicator { Source = source };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 30; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Source {source} should produce finite value");
        }
    }

    [Fact]
    public void BbwIndicator_Period_CanBeChanged()
    {
        var indicator = new BbwIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }

    [Fact]
    public void BbwIndicator_Multiplier_CanBeChanged()
    {
        var indicator = new BbwIndicator();
        Assert.Equal(2.0, indicator.Multiplier);

        indicator.Multiplier = 1.5;
        Assert.Equal(1.5, indicator.Multiplier);

        indicator.Multiplier = 3.0;
        Assert.Equal(3.0, indicator.Multiplier);
    }

    [Fact]
    public void BbwIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new BbwIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void BbwIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BbwIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Bbw.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}
