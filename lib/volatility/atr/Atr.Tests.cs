using System;
using Xunit;

namespace QuanTAlib.Tests;

public class AtrTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            atr.Update(bar);
        }

        Assert.True(double.IsFinite(atr.Last.Value));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            atr.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        atr.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);

        // This will update the logic: compute new TR based on modifiedBar vs prevBar(98)
        double val2 = atr.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var atr2 = new Atr(14);
        for (int i = 0; i < 99; i++)
        {
            atr2.Update(bars[i]);
        }
        double val3 = atr2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void Reset_Works()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars) atr.Update(bar);

        double lastVal = atr.Last.Value;
        Assert.NotEqual(0, lastVal);

        atr.Reset();
        Assert.Equal(0, atr.Last.Value);
        Assert.False(atr.IsHot);
    }

    [Fact]
    public void Chainability_Works()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = atr.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(atr.Last.Value, result.Last.Value);
    }
}
