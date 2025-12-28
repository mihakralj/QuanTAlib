using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AdoscIndicatorTests
{
    [Fact]
    public void AdoscIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AdoscIndicator();

        Assert.Equal(3, indicator.FastPeriod);
        Assert.Equal(10, indicator.SlowPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("ADOSC - Accumulation/Distribution Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AdoscIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AdoscIndicator
        {
            SlowPeriod = 20
        };

        Assert.Equal(0, AdoscIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AdoscIndicator_SlowPeriod_CanBeChanged()
    {
        var indicator = new AdoscIndicator
        {
            SlowPeriod = 40
        };

        Assert.Equal(40, indicator.SlowPeriod);
        Assert.Equal(0, AdoscIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AdoscIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AdoscIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Adosc.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AdoscIndicator_Initialize_CreatesInternalAdosc()
    {
        var indicator = new AdoscIndicator { FastPeriod = 5, SlowPeriod = 34 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void AdoscIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AdoscIndicator { FastPeriod = 2, SlowPeriod = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void AdoscIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AdoscIndicator { FastPeriod = 2, SlowPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125, 1200);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void AdoscIndicator_Parameters_CanBeChanged()
    {
        var indicator = new AdoscIndicator { FastPeriod = 5, SlowPeriod = 34 };
        Assert.Equal(5, indicator.FastPeriod);
        Assert.Equal(34, indicator.SlowPeriod);

        indicator.FastPeriod = 10;
        indicator.SlowPeriod = 40;

        Assert.Equal(10, indicator.FastPeriod);
        Assert.Equal(40, indicator.SlowPeriod);
        Assert.Equal(0, AdoscIndicator.MinHistoryDepths);
    }
}
