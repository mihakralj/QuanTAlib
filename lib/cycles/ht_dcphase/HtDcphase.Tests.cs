using System;
using QuanTAlib;
using Xunit;

namespace QuanTAlib.Tests.Cycles;

public class HtDcphaseTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var ht = new HtDcphase();
        Assert.Equal("HtDcphase", ht.Name);
        Assert.Equal(63, ht.WarmupPeriod);
        Assert.False(ht.IsHot);
    }

    [Fact]
    public void Update_BecomesHotAfterWarmup()
    {
        var ht = new HtDcphase();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

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
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 80; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        Assert.True(ht.IsHot);
        ht.Reset();
        Assert.False(ht.IsHot);
        Assert.Equal(default, ht.Last);
    }

    [Fact]
    public void PhaseRange_IsValid()
    {
        var ht = new HtDcphase();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ht.Update(new TValue(bar.Time, bar.Close));
        }

        // After warmup, phase should be in valid range
        double phase = ht.Last.Value;
        Assert.True(phase >= -45.0 && phase <= 315.0,
            $"Phase {phase} should be in range [-45, 315]");
    }

    [Fact]
    public void SameBarUpdate_ReturnsSameValue()
    {
        var ht = new HtDcphase();
        var now = DateTime.UtcNow;

        // Prime with data
        for (int i = 0; i < 70; i++)
        {
            ht.Update(new TValue(now.AddMinutes(i), 100 + Math.Sin(i * 0.1) * 10));
        }

        Assert.True(ht.IsHot);

        // First update (new bar)
        var result1 = ht.Update(new TValue(now.AddMinutes(70), 105), isNew: true);

        // Same bar update
        var result2 = ht.Update(new TValue(now.AddMinutes(70), 106), isNew: false);

        Assert.Equal(result1.Value, result2.Value);
    }
}
