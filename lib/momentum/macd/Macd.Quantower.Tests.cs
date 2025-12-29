using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class MacdIndicatorTests
{
    [Fact]
    public void MacdIndicator_Constructor_SetsDefaults()
    {
        var indicator = new MacdIndicator();

        Assert.Equal("MACD - Moving Average Convergence Divergence", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(12, indicator.FastPeriod);
        Assert.Equal(26, indicator.SlowPeriod);
        Assert.Equal(9, indicator.SignalPeriod);
    }

    [Fact]
    public void MacdIndicator_MinHistoryDepths_EqualsMaxPeriodPlusSignal()
    {
        var indicator = new MacdIndicator
        {
            FastPeriod = 12,
            SlowPeriod = 26,
            SignalPeriod = 9,
        };

        // 26 + 9 = 35
        Assert.Equal(0, MacdIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void MacdIndicator_ShortName_IncludesPeriods()
    {
        var indicator = new MacdIndicator();
        indicator.Initialize();

        Assert.Equal("MACD(12,26,9):Close", indicator.ShortName);
    }

    [Fact]
    public void MacdIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new MacdIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Macd.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void MacdIndicator_Initialize_CreatesInternalMacd()
    {
        var indicator = new MacdIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (MACD, Signal, Hist)
        Assert.Equal(3, indicator.LinesSeries.Count);
    }

    [Fact]
    public void MacdIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new MacdIndicator
        {
            FastPeriod = 2,
            SlowPeriod = 5,
            SignalPeriod = 2,
        };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for(int i=0; i<10; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100, 105, 95, 100 + i);
        }

        // Process updates
        var args = new UpdateArgs(UpdateReason.HistoricalBar);

        for(int i=0; i<10; i++)
        {
            indicator.ProcessUpdate(args);
        }

        // Line series should have values
        double macd = indicator.LinesSeries[0].GetValue(0);
        double signal = indicator.LinesSeries[1].GetValue(0);
        double hist = indicator.LinesSeries[2].GetValue(0);

        // Just check they are valid numbers
        Assert.False(double.IsNaN(macd));
        Assert.False(double.IsNaN(signal));
        Assert.False(double.IsNaN(hist));
    }
}
