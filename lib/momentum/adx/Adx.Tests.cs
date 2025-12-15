using Xunit;

namespace QuanTAlib.Tests;

public class AdxTests
{
    private readonly GBM _gbm = new();

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenPeriodIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => new Adx(0));
        Assert.Throws<ArgumentException>(() => new Adx(-1));
    }

    [Fact]
    public void Update_ReturnsValidValues_WhenInputIsValid()
    {
        var adx = new Adx(14);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = adx.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Update_HandlesIsNewCorrectly()
    {
        var adx = new Adx(14);
        // We need enough bars to warm up ADX (2 * Period)
        int count = 2 * 14 + 5;
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        
        // Feed all but last bar
        for (int i = 0; i < count - 1; i++)
        {
            adx.Update(bars[i]);
        }

        // Update with last bar (isNew=true)
        var result1 = adx.Update(bars[count - 1], true);

        // Update with modified last bar (isNew=false)
        var modifiedBar = new TBar(bars[count - 1].Time, bars[count - 1].Open, bars[count - 1].High + 1, bars[count - 1].Low - 1, bars[count - 1].Close, bars[count - 1].Volume);
        var result2 = adx.Update(modifiedBar, false);

        // The result should change because High/Low changed, affecting TR and DM
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Reset_ResetsState()
    {
        var adx = new Adx(14);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)); // Increased to 100

        foreach (var bar in bars)
        {
            adx.Update(bar);
        }

        Assert.True(adx.IsHot);
        adx.Reset();
        Assert.False(adx.IsHot);
        Assert.Equal(0, adx.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrue_AfterWarmup()
    {
        var adx = new Adx(14);
        var bars = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        int i = 0;
        for (; i < bars.Count; i++)
        {
            adx.Update(bars[i]);
            if (adx.IsHot) break;
        }

        Assert.True(i < bars.Count);
        Assert.True(adx.IsHot);
    }

    [Fact]
    public void Update_HandlesNaN_Gracefully()
    {
        var adx = new Adx(14);
        var bar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        
        var result = adx.Update(bar);
        
        // Should not throw and return finite value (likely 0 or last valid)
        // Since it's the first value, it might be 0.
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_TValue_ReturnsValidResult()
    {
        var adx = new Adx(14);
        var val = new TValue(DateTime.UtcNow, 100);
        
        var result = adx.Update(val);
        
        Assert.True(double.IsFinite(result.Value));
    }
}
