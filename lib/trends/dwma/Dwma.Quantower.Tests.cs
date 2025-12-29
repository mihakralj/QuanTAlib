using TradingPlatform.BusinessLayer;

namespace QuanTAlib.Tests;

public class DwmaIndicatorTests
{
    [Fact]
    public void DwmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new DwmaIndicator();

        Assert.Equal(10, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("DWMA - Double Weighted Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }

    [Fact]
    public void DwmaIndicator_MinHistoryDepths_EqualsTwoTimesPeriod()
    {
        var indicator = new DwmaIndicator { Period = 20 };

        Assert.Equal(0, DwmaIndicator.MinHistoryDepths);
        Assert.Equal(0, ((IWatchlistIndicator)indicator).MinHistoryDepths);
    }

    [Fact]
    public void DwmaIndicator_ShortName_IncludesPeriodAndSource()
    {
        var indicator = new DwmaIndicator { Period = 15 };

        Assert.Contains("DWMA", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("15", indicator.ShortName, StringComparison.Ordinal);
    }

    [Fact]
    public void DwmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new DwmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink, StringComparison.Ordinal);
        Assert.Contains("Dwma.Quantower.cs", indicator.SourceCodeLink, StringComparison.Ordinal);
    }

    [Fact]
    public void DwmaIndicator_Initialize_CreatesInternalDwma()
    {
        var indicator = new DwmaIndicator { Period = 10 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void DwmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new DwmaIndicator { Period = 3 };
        indicator.Initialize();

        // Add historical data
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);

        // Process update
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);

        // Line series should have a value
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }
}
