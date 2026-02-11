using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class CmoIndicatorTests
{
    [Fact]
    public void CmoIndicator_Constructor_SetsDefaults()
    {
        var indicator = new CmoIndicator();

        Assert.Equal("CMO - Chande Momentum Oscillator", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
        Assert.Equal(14, indicator.Period);
    }

    [Fact]
    public void CmoIndicator_MinHistoryDepths_IsStatic()
    {
        var indicator = new CmoIndicator
        {
            Period = 20,
        };

        Assert.Equal(0, CmoIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void CmoIndicator_ShortName_IncludesPeriod()
    {
        var indicator = new CmoIndicator
        {
            Period = 20,
        };
        indicator.Initialize();

        Assert.Contains("CMO(20)", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void CmoIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new CmoIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Cmo.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void CmoIndicator_Initialize_CreatesInternalCmo()
    {
        var indicator = new CmoIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (CMO)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void CmoIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new CmoIndicator
        {
            Period = 2, // Short period for testing
        };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 105, 95, 102); // Gain 2
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100, 105, 95, 101); // Loss 1

        // Process updates
        var args = new UpdateArgs(UpdateReason.HistoricalBar);

        indicator.ProcessUpdate(args); // Bar 0
        indicator.ProcessUpdate(args); // Bar 1
        indicator.ProcessUpdate(args); // Bar 2

        // Line series should have a value
        double cmo = indicator.LinesSeries[0].GetValue(0);

        // CMO ranges from -100 to +100
        Assert.True(cmo >= -100 && cmo <= 100);
    }

    [Fact]
    public void CmoIndicator_Period_CanBeSet()
    {
        var indicator = new CmoIndicator { Period = 20 };
        Assert.Equal(20, indicator.Period);
    }

    [Fact]
    public void CmoIndicator_Source_CanBeSet()
    {
        var indicator = new CmoIndicator { Source = SourceType.HLC3 };
        Assert.Equal(SourceType.HLC3, indicator.Source);
    }

    [Fact]
    public void CmoIndicator_OnUpdate_ProducesValidRange()
    {
        var indicator = new CmoIndicator { Period = 2 };
        indicator.Initialize();

        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 100);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 100, 106, 96, 103);
        indicator.HistoricalData.AddBar(now.AddMinutes(2), 100, 104, 94, 101);

        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);
        indicator.ProcessUpdate(args);
        indicator.ProcessUpdate(args);

        double cmo = indicator.LinesSeries[0].GetValue(0);

        // CMO ranges from -100 to +100
        Assert.True(cmo >= -100 && cmo <= 100);
    }
}
