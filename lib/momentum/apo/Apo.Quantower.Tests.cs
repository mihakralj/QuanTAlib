using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class ApoIndicatorTests
{
    [Fact]
    public void ApoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ApoIndicator();

        Assert.Equal(12, indicator.FastPeriod);
        Assert.Equal(26, indicator.SlowPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("APO - Absolute Price Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void ApoIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new ApoIndicator { SlowPeriod = 20 };

        Assert.Equal(0, ApoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ApoIndicator_ShortName_IncludesParameters()
    {
        var indicator = new ApoIndicator { FastPeriod = 10, SlowPeriod = 40 };
        indicator.Initialize();

        Assert.Contains("APO", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("40", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void ApoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ApoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Apo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void ApoIndicator_Initialize_CreatesInternalApo()
    {
        var indicator = new ApoIndicator { FastPeriod = 5, SlowPeriod = 34 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ApoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ApoIndicator { FastPeriod = 2, SlowPeriod = 5 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        // Need enough bars for Period
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double val = indicator.LinesSeries[0].GetValue(0);
        Assert.True(double.IsFinite(val));
    }

    [Fact]
    public void ApoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new ApoIndicator { FastPeriod = 2, SlowPeriod = 5 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);
        }

        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));

        // Add new bar
        indicator.HistoricalData.AddBar(now.AddMinutes(20), 120, 130, 110, 125);
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));

        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }

    [Fact]
    public void ApoIndicator_Parameters_CanBeChanged()
    {
        var indicator = new ApoIndicator { FastPeriod = 5, SlowPeriod = 34 };
        Assert.Equal(5, indicator.FastPeriod);
        Assert.Equal(34, indicator.SlowPeriod);

        indicator.FastPeriod = 10;
        indicator.SlowPeriod = 40;

        Assert.Equal(10, indicator.FastPeriod);
        Assert.Equal(40, indicator.SlowPeriod);
        Assert.Equal(0, ApoIndicator.MinHistoryDepths);
    }
}
