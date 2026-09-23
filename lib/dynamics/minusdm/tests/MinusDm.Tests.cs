using Xunit;

namespace QuanTAlib.Tests;

public sealed class MinusDmTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentException>(() => new MinusDm(0));
        Assert.Throws<ArgumentException>(() => new MinusDm(-1));
    }

    [Fact]
    public void Update_ProducesNonNegativeMovement()
    {
        var indicator = new MinusDm(3);
        for (int i = 0; i < 8; i++)
        {
            indicator.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100 - i, 101 - i, 98 - i, 99 - i, 1000));
        }

        Assert.True(indicator.IsHot);
        Assert.True(indicator.Last.Value >= 0.0);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new MinusDm(3);
        indicator.Update(new TBar(DateTime.UtcNow, 100, 101, 98, 99, 1000));
        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }
}