using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class EMA_Test
{
    [Fact]
    public void EMASeries_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        EMA_Series c = new(a, 3);
        Assert.Equal(6, c.Count);
        Assert.Equal(4.03125, c.Last().v);
    }

    [Fact]
    public void EMAUpdate_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        EMA_Series c = new(a, 3);
        a.Add(2, true);
        Assert.Equal(2.53125, c.Last().v);
    }
}