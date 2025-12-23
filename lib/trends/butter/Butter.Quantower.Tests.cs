using System;
using Xunit;
using TradingPlatform.BusinessLayer;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class ButterIndicatorTests
{
    [Fact]
    public void ButterIndicator_Constructor_SetsDefaults()
    {
        var indicator = new ButterIndicator();

        Assert.Equal(14, indicator.Period);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("BUTTER - Butterworth Filter", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.Equal(SourceType.Close, indicator.Source);
    }

    [Fact]
    public void ButterIndicator_MinHistoryDepths_EqualsPeriod()
    {
        var indicator = new ButterIndicator { Period = 20 };

        Assert.Equal(20, indicator.MinHistoryDepths);
        IWatchlistIndicator watchlistIndicator = indicator;
        Assert.Equal(20, watchlistIndicator.MinHistoryDepths);
    }

    [Fact]
    public void ButterIndicator_ShortName_IncludesParameters()
    {
        var indicator = new ButterIndicator { Period = 20 };
        indicator.Initialize();

        Assert.Contains("BUTTER", indicator.ShortName);
        Assert.Contains("20", indicator.ShortName);
    }

    [Fact]
    public void ButterIndicator_SourceCodeLink_IsValid()
    {
        var indicator = new ButterIndicator();

        Assert.Contains("github.com", indicator.SourceCodeLink);
        Assert.Contains("Butter.Quantower.cs", indicator.SourceCodeLink);
    }

    [Fact]
    public void ButterIndicator_Initialize_CreatesInternalButter()
    {
        var indicator = new ButterIndicator { Period = 14 };

        // Initialize should not throw
        indicator.Initialize();

        // After init, line series should exist
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void ButterIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new ButterIndicator { Period = 5 };
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
        double butter = indicator.LinesSeries[0].GetValue(0);

        Assert.True(double.IsFinite(butter));
    }
}
