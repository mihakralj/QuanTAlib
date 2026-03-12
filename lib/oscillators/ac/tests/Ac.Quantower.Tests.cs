using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AcIndicatorTests
{
    [Fact]
    public void AcIndicator_Constructor_SetsDefaults()
    {
        var indicator = new AcIndicator();

        Assert.Equal(5, indicator.FastPeriod);
        Assert.Equal(34, indicator.SlowPeriod);
        Assert.Equal(5, indicator.AcPeriod);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("AC - Acceleration Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void AcIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new AcIndicator { SlowPeriod = 20 };

        Assert.Equal(0, AcIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void AcIndicator_ShortName_IncludesParameters()
    {
        var indicator = new AcIndicator { FastPeriod = 10, SlowPeriod = 40, AcPeriod = 7 };
        indicator.Initialize();

        Assert.Contains("AC", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("10", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("40", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("7", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void AcIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new AcIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Ac.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void AcIndicator_Initialize_CreatesInternalAc()
    {
        var indicator = new AcIndicator { FastPeriod = 5, SlowPeriod = 34, AcPeriod = 5 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (Up and Down)
        Assert.Equal(2, indicator.LinesSeries.Count);
    }

    [Fact]
    public void AcIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new AcIndicator { FastPeriod = 2, SlowPeriod = 5, AcPeriod = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value (either Up or Down)
        double up = indicator.LinesSeries[0].GetValue(0);
        double down = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(up) || double.IsFinite(down));
    }

    [Fact]
    public void AcIndicator_ProcessUpdate_NewBar_UpdatesValue()
    {
        var indicator = new AcIndicator { FastPeriod = 2, SlowPeriod = 5, AcPeriod = 3 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            var reason = i < 19 ? UpdateReason.HistoricalBar : UpdateReason.NewBar;
            var args = new UpdateArgs(reason);
            indicator.ProcessUpdate(args);
        }

        // Verify line series has values
        double up = indicator.LinesSeries[0].GetValue(0);
        double down = indicator.LinesSeries[1].GetValue(0);

        Assert.True(double.IsFinite(up) || double.IsFinite(down));
    }
}
