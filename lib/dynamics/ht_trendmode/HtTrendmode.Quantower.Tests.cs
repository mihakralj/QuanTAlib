using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class HtTrendmodeIndicatorTests
{
    [Fact]
    public void HtTrendmodeIndicator_Constructor_SetsDefaults()
    {
        var indicator = new HtTrendmodeIndicator();

        Assert.Equal(SourceType.Close, indicator.SourceInput);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("HT_TRENDMODE - Ehlers Hilbert Transform Trend vs Cycle Mode", indicator.Name);
        Assert.True(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void HtTrendmodeIndicator_MinHistoryDepths_EqualsZero()
    {
        var indicator = new HtTrendmodeIndicator();

        Assert.Equal(0, HtTrendmodeIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void HtTrendmodeIndicator_Initialize_CreatesInternalIndicator()
    {
        var indicator = new HtTrendmodeIndicator();

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist (TrendMode)
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void HtTrendmodeIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new HtTrendmodeIndicator();
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            indicator.HistoricalData.AddBar(now.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i);

            // Process update for each bar to simulate history loading
            var args = new UpdateArgs(UpdateReason.HistoricalBar);
            indicator.ProcessUpdate(args);
        }

        // Line series should have a value
        double trendMode = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(trendMode));
    }

    [Fact]
    public void HtTrendmodeIndicator_ShortName_IsCorrect()
    {
        var indicator = new HtTrendmodeIndicator();
        Assert.Equal("HT_TRENDMODE", indicator.ShortName);
    }

    [Fact]
    public void HtTrendmodeIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new HtTrendmodeIndicator();
        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HtTrendmode.Quantower.cs", indicator.SourceCodeLink, StringComparison.OrdinalIgnoreCase);
    }
}
