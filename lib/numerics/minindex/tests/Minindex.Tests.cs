using Xunit;

namespace QuanTAlib.Tests;

public sealed class MinindexTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Minindex(1));
    }

    [Fact]
    public void Update_ReturnsBarsAgoIndex()
    {
        var indicator = new Minindex(3);
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 5));
        indicator.Update(new TValue(time.AddMinutes(1), 1));
        indicator.Update(new TValue(time.AddMinutes(2), 3));

        Assert.Equal(1.0, indicator.Last.Value);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Batch_ReturnsAbsoluteIndex()
    {
        double[] source = [5, 1, 3, 0];
        double[] output = new double[source.Length];
        Minindex.Batch(source, output, 3);

        Assert.Equal([0.0, 1.0, 1.0, 3.0], output);
    }
}