namespace QuanTAlib.Tests;

public class TPositionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var position = new TPosition(12345, Direction.Long, true);

        Assert.Equal(12345, position.Time);
        Assert.Equal(Direction.Long, position.Direction);
        Assert.True(position.IsHot);
    }

    [Fact]
    public void Direction_ContainsLongFlatAndShort()
    {
        Assert.Equal(-1, (sbyte)Direction.Short);
        Assert.Equal(0, (sbyte)Direction.Flat);
        Assert.Equal(1, (sbyte)Direction.Long);
    }

    [Fact]
    public void RecordEquality_UsesAllProperties()
    {
        var first = new TPosition(12345, Direction.Flat, true);
        var same = new TPosition(12345, Direction.Flat, true);
        var different = new TPosition(12345, Direction.Long, true);

        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
    }
}
