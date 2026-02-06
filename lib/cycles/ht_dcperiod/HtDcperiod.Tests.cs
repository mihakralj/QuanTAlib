using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests.Cycles;

public class HtDcperiodTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ht = new HtDcperiod();
        Assert.Equal("HtDcperiod", ht.Name);
        Assert.Equal(32, ht.WarmupPeriod);
        Assert.False(ht.IsHot);
    }

    [Fact]
    public void Update_BecomesHotAfterWarmup()
    {
        var ht = new HtDcperiod();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(80, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ht.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ht.IsHot);
        Assert.True(double.IsFinite(ht.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ht = new HtDcperiod();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 40; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        Assert.True(ht.IsHot);
        ht.Reset();
        Assert.False(ht.IsHot);
        Assert.Equal(default, ht.Last);
    }
}
