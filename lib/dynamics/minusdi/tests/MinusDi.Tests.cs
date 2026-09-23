using Xunit;

namespace QuanTAlib.Tests;

public sealed class MinusDiTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentException>(() => new MinusDi(0));
        Assert.Throws<ArgumentException>(() => new MinusDi(-1));
    }

    [Fact]
    public void Update_ProducesFiniteDirectionalIndicator()
    {
        var indicator = new MinusDi(3);
        for (int i = 0; i < 8; i++)
        {
            indicator.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100 - i, 101 - i, 98 - i, 99 - i, 1000));
        }

        Assert.True(indicator.IsHot);
        Assert.InRange(indicator.Last.Value, 0.0, 100.0);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        var source = new TBarSeries();
        for (int i = 0; i < 12; i++)
        {
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 99 + i, 101 + i, 1000));
        }

        var streaming = new MinusDi(3).Update(source);
        var batch = MinusDi.Batch(source, 3);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streaming[i].Value, batch[i].Value, 1e-12);
        }
    }
}