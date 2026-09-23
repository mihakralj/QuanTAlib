using Xunit;

namespace QuanTAlib.Tests;

public sealed class SarextTests
{
    [Fact]
    public void Constructor_ValidatesAccelerationParameters()
    {
        Assert.Throws<ArgumentException>(() => new Sarext(afInitLong: 0));
        Assert.Throws<ArgumentException>(() => new Sarext(afLong: 0));
        Assert.Throws<ArgumentException>(() => new Sarext(afMaxLong: 0.01));
        Assert.Throws<ArgumentException>(() => new Sarext(afInitShort: 0));
        Assert.Throws<ArgumentException>(() => new Sarext(afShort: 0));
        Assert.Throws<ArgumentException>(() => new Sarext(afMaxShort: 0.01));
        Assert.Throws<ArgumentException>(() => new Sarext(offsetOnReverse: -1));
    }

    [Fact]
    public void Update_WarmsUpAfterTwoBars()
    {
        var indicator = new Sarext();
        var time = DateTime.UtcNow;

        Assert.True(double.IsNaN(indicator.Update(new TBar(time, 100, 102, 98, 101, 1000)).Value));
        var result = indicator.Update(new TBar(time.AddMinutes(1), 101, 104, 99, 103, 1000));

        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Sarext();
        indicator.Update(new TBar(DateTime.UtcNow, 100, 102, 98, 101, 1000));
        indicator.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 101, 104, 99, 103, 1000));
        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }
}