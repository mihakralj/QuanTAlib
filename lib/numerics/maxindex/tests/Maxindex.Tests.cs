using Xunit;

namespace QuanTAlib.Tests;

public sealed class MaxindexTests
{
    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Maxindex(1));
    }

    [Fact]
    public void Update_ReturnsBarsAgoIndex()
    {
        var indicator = new Maxindex(3);
        var time = DateTime.UtcNow;
        indicator.Update(new TValue(time, 1));
        indicator.Update(new TValue(time.AddMinutes(1), 5));
        indicator.Update(new TValue(time.AddMinutes(2), 3));

        Assert.Equal(1.0, indicator.Last.Value);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Batch_ReturnsAbsoluteIndex()
    {
        double[] source = [1, 5, 3, 2];
        double[] output = new double[source.Length];
        Maxindex.Batch(source, output, 3);

        Assert.Equal([0.0, 1.0, 1.0, 1.0], output);
    }
}