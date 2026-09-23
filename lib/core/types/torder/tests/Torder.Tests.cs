namespace QuanTAlib.Tests;

public class TOrderTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var order = new TOrder(12345, OrderAction.BTO, true);

        Assert.Equal(12345, order.Time);
        Assert.Equal(OrderAction.BTO, order.Action);
        Assert.True(order.IsHot);
    }

    [Fact]
    public void OrderAction_ContainsExpectedActions()
    {
        Assert.Equal(2, (sbyte)OrderAction.BTO);
        Assert.Equal(1, (sbyte)OrderAction.BTC);
        Assert.Equal(0, (sbyte)OrderAction.NIL);
        Assert.Equal(-1, (sbyte)OrderAction.STC);
        Assert.Equal(-2, (sbyte)OrderAction.STO);
    }

    [Fact]
    public void RecordEquality_UsesAllProperties()
    {
        var first = new TOrder(12345, OrderAction.STC, true);
        var same = new TOrder(12345, OrderAction.STC, true);
        var different = new TOrder(12345, OrderAction.STO, true);

        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
    }
}
