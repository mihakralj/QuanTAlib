using Xunit;

namespace QuantLib;

public class RMA_Test
{
    [Fact]
    public void RMASeries_Test()
    {
        TSeries a = new() {
            0, 1, 2, 3, 4, 5
        };
        RMA_Series c = new(a, 3);
        Assert.Equal(6, c.Count);
        Assert.Equal(3.2633744855967075, c.Last().v);
    }

    [Fact]
    public void RMAUpdate_Test()
    {
        TSeries a = new() {
            0, 1, 2, 3, 4, 5
        };
        RMA_Series c = new(a, 3);
        a.Add(2, true);
        Assert.Equal(2.263374485596708, c.Last().v);
    }
}