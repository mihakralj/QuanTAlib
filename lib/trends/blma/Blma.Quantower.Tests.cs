using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class BlmaIndicatorTests
{
    [Fact]
    public void BlmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new BlmaIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BLMA - Blackman Window Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void BlmaIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new BlmaIndicator { Period = 20 };

        Assert.Equal(0, BlmaIndicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(0, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void BlmaIndicator_ShortName_IncludesParameters()
    {
        var indicator = new BlmaIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("BLMA", indicator.ShortName);
        Assert.Contains("20", indicator.ShortName);
    }

    [Fact]
    public void BlmaIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new BlmaIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Blma.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void BlmaIndicator_Initialize_CreatesInternalBlma()
    {
        var indicator = new BlmaIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void BlmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new BlmaIndicator { Period = 5 };
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
        double blma = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(blma));
    }
}
