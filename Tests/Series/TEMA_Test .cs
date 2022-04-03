using Xunit;
using System;
using QuantLib;

namespace QuantLib;
public class TEMA_Test
{
    [Fact]
    public void TEMASeries_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        TEMA_Series c = new(a, 3);
        Assert.Equal(6, c.Count);
        Assert.Equal(5.0390625, c.Last().v);
    }

    [Fact]
    public void TEMAUpdate_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        TEMA_Series c = new(a, 3);
        a.Add(2, true);
        Assert.Equal(2.4140625, c.Last().v);
    }
}
