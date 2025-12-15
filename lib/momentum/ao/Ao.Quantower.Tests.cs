using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AoIndicatorTests
{
    [Fact]
    public void AoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AoIndicator();

        Assert.Equal(5, indicator.FastPeriod);
        Assert.Equal(34, indicator.SlowPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("AO - Awesome Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AoIndicator_MinHistoryDepths_EqualsSlowPeriod()
    {
        var indicator = new AoIndicator { SlowPeriod = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(20, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AoIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AoIndicator { FastPeriod = 10, SlowPeriod = 40 };
        indicator.Initialize();

        Assert.Contains("AO", indicator.ShortName);
        Assert.Contains("10", indicator.ShortName);
        Assert.Contains("40", indicator.ShortName);
    }

    [Fact]
    public void AoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Ao.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void AoIndicator_Initialize_CreatesInternalAo()
    {
        var indicator = new AoIndicator { FastPeriod = 5, SlowPeriod = 34 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Up and Down)
        Assert.Equal(2, indicator.LinesSeries.Length);
    }

    [Fact]
    public void AoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AoIndicator { FastPeriod = 2, SlowPeriod = 5 };
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

        // Line series should have a value (either Up or Down)
        // One should be NaN, other should be value, or both NaN if cold
        double up = indicator.LinesSeries[0].GetValue(0);
        double down = indicator.LinesSeries[1].GetValue(0);
        
        Assert.True(double.IsFinite(up) || double.IsFinite(down));
    }

    [Fact]
    public void AoIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new AoIndicator { FastPeriod = 2, SlowPeriod = 5 };
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
    public void AoIndicator_Parameters_CanBeChanged()
    {
        var indicator = new AoIndicator { FastPeriod = 5, SlowPeriod = 34 };
        Assert.Equal(5, indicator.FastPeriod);
        Assert.Equal(34, indicator.SlowPeriod);

        indicator.FastPeriod = 10;
        indicator.SlowPeriod = 40;

        Assert.Equal(10, indicator.FastPeriod);
        Assert.Equal(40, indicator.SlowPeriod);
        Assert.Equal(40, indicator.MinHistoryDepths);
    }
}
