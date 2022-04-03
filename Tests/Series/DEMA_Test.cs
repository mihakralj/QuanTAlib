using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class DEMA_Test
{
    [Fact]
    public void DEMASeries_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        DEMA_Series c = new(a, 3);
        Assert.Equal(6, c.Count);
        Assert.Equal(4.921875, c.Last().v);
    }

    [Fact]
    public void DEMAUpdate_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        DEMA_Series c = new(a, 3);
        a.Add(2, true);
        Assert.Equal(2.671875, c.Last().v);
    }
}
