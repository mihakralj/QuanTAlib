namespace QuanTAlib.Tests;

public class MamaReproTests
{
    [Fact]
    public void Constructor_ThrowsArgumentException_WhenSlowLimitIsZero()
    {
        Assert.Throws<ArgumentException>(() => new Mama(0.5, 0.0));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenSlowLimitIsNegative()
    {
        Assert.Throws<ArgumentException>(() => new Mama(0.5, -0.1));
    }
}
