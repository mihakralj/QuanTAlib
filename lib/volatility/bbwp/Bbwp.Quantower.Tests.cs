using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class BbwpIndicatorTests
{
    [Fact]
    public void BbwpIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BbwpIndicator();

        Assert.Equal(20, indicator.Period);
        Assert.Equal(2.0, indicator.Multiplier);
        Assert.Equal(252, indicator.Lookback);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BBWP - Bollinger Band Width Percentile", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void BbwpIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BbwpIndicator { Period = 14, Multiplier = 2.5, Lookback = 100 };
        Assert.Contains("BBWP", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("14", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("2.5", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("100", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void BbwpIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new BbwpIndicator();

        Assert.Equal(0, BbwpIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void BbwpIndicator_Initialize_CreatesInternalBbwp()
    {
        var indicator = new BbwpIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BbwpIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BbwpIndicator { Period = 5, Lookback = 20 };
        indicator.Initialize();

        // Add historical data with volatility
        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i * 2 + (i % 2 == 0 ? 5 : -5); // Add some volatility
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
        Assert.True(val >= 0 && val <= 1); // BBWP should be in [0,1] range
    }

    [Fact]
    public void BbwpIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new BbwpIndicator { Period = 5, Lookback = 20 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i;
            indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(30), 120, 128, 115, 125, 1500);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void BbwpIndicator_DifferentPeriods_Work()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var indicator = new BbwpIndicator { Period = period, Lookback = 30 };
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
            Assert.True(val >= 0 && val <= 1, $"Period {period} should produce BBWP percentile in range");
        }
    }

    [Fact]
    public void BbwpIndicator_DifferentLookbacks_Work()
    {
        int[] lookbacks = { 10, 20, 50, 100 };

        foreach (var lookback in lookbacks)
        {
            var indicator = new BbwpIndicator { Lookback = lookback };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 120; i++)
            {
                double basePrice = 100 + i;
                indicator.HistoricalData.AddBar(now.AddMinutes(i), basePrice, basePrice + 5, basePrice - 5, basePrice + 2, 1000);
                indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            }

            double val = indicator.LinesSeries[0].GetValue(0);
            Assert.True(double.IsFinite(val), $"Lookback {lookback} should produce finite value");
            Assert.True(val >= 0 && val <= 1, $"Lookback {lookback} should produce BBWP percentile in range");
        }
    }

    [Fact]
    public void BbwpIndicator_DifferentSourceTypes_Work()
    {
        SourceType[] sources = { SourceType.Close, SourceType.High, SourceType.Low, SourceType.HL2, SourceType.HLC3 };

        foreach (var source in sources)
        {
            var indicator = new BbwpIndicator { Source = source, Lookback = 20 };
            indicator.Initialize();

            var now = DateTime.UtcNow;
            for (int i = 0; i < 40; i++)
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
    public void BbwpIndicator_Period_CanBeChanged()
    {
        var indicator = new BbwpIndicator();
        Assert.Equal(20, indicator.Period);

        indicator.Period = 14;
        Assert.Equal(14, indicator.Period);

        indicator.Period = 50;
        Assert.Equal(50, indicator.Period);
    }

    [Fact]
    public void BbwpIndicator_Lookback_CanBeChanged()
    {
        var indicator = new BbwpIndicator();
        Assert.Equal(252, indicator.Lookback);

        indicator.Lookback = 100;
        Assert.Equal(100, indicator.Lookback);

        indicator.Lookback = 50;
        Assert.Equal(50, indicator.Lookback);
    }

    [Fact]
    public void BbwpIndicator_ShowColdValues_CanBeToggled()
    {
        var indicator = new BbwpIndicator();
        Assert.True(indicator.ShowColdValues);

        indicator.ShowColdValues = false;
        Assert.False(indicator.ShowColdValues);

        indicator.ShowColdValues = true;
        Assert.True(indicator.ShowColdValues);
    }

    [Fact]
    public void BbwpIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BbwpIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Bbwp.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }
}