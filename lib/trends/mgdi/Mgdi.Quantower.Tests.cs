using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Quantower.Tests;

public class MgdiIndicatorTests
{
    [Fact]
    public void Indicator_Initializes_Correctly()
    {
        var indicator = new MgdiIndicator();
        Assert.Equal("MGDI - McGinley Dynamic Indicator", indicator.Name);
        Assert.Equal("MGDI(14,0.6):Close", indicator.ShortName);
        Assert.Equal(14, indicator.MinHistoryDepths);
        Assert.Single(indicator.LinesSeries);
    }

    [Fact]
    public void Indicator_Updates_Correctly()
    {
        var indicator = new MgdiIndicator();
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
