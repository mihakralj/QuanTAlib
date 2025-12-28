using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Quantower.Tests;

public class HtitIndicatorTests
{
    [Fact]
    public void Indicator_Initializes_Correctly()
    {
        var indicator = new HtitIndicator();
        indicator.Initialize();
        Assert.Equal("HTIT - Ehlers Hilbert Transform Instantaneous Trend", indicator.Name);
        Assert.StartsWith("HTIT", indicator.ShortName, StringComparison.Ordinal);
        Assert.Contains("Close", indicator.ShortName, StringComparison.Ordinal);
        Assert.Equal(0, HtitIndicator.MinHistoryDepths);
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Indicator_Updates_Correctly()
    {
        var indicator = new HtitIndicator();
        indicator.Initialize();

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            var time = DateTime.UtcNow.AddMinutes(i);
            indicator.HistoricalData.AddBar(time, 100 + i, 100 + i, 100 + i, 100 + i);

            var args = new UpdateArgs(UpdateReason.NewBar);
            indicator.ProcessUpdate(args);
        }

        // Check if value is set (should be non-zero after warmup)
        var result = indicator.LinesSeries[0].GetValue();
        Assert.NotEqual(0, result);
        Assert.False(double.IsNaN(result));
    }
}
