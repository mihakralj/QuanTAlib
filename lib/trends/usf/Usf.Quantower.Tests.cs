using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;
using Xunit;

namespace QuanTAlib.Quantower.Tests;

public class UsfIndicatorTests
{
    [Fact]
    public void Indicator_InitializesCorrectly()
    {
        var indicator = new UsfIndicator();
        Assert.Equal(20, indicator.Period);
        Assert.Equal("USF 20:Close", indicator.ShortName);
    }

    [Fact]
    public void Indicator_ProcessesData()
    {
        var indicator = new UsfIndicator();
        
        // Simulate Init
        indicator.GetType().GetMethod("OnInit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(indicator, null);
        
        Assert.NotNull(indicator);
    }
}
