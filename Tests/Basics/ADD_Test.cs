using Xunit;
using System;
using QuanTAlib;

namespace Basics;
public class ADD_Test
{
    [Fact]
    public void ADDSeriesSeries_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        TSeries b = new() { 5, 4, 3, 2, 1, 0 };
        ADD_Series c = new(a, b);
        Assert.Equal(5, c.Last().v);
    }

    [Fact]
    public void ADDSeriesDouble_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        ADD_Series c = new(a, 10.0);
        Assert.Equal(15, c.Last().v);
    }

    [Fact]
    public void ADDDoubleSeries_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        ADD_Series c = new(10.0, a);
        Assert.Equal(15, c.Last().v);
    }

    [Fact]
    public void ADDEventing_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        TSeries b = new() { 5, 4, 3, 2, 1, 0 };
        ADD_Series c = new(a, b);
        a.Add(2);
        b.Add(2);
        Assert.Equal(4, c.Last().v);
    }

    [Fact]
    public void ADDUpdateDouble_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        double b = 10;
        ADD_Series c = new(a, b);
        a.Add(0, true);
        Assert.Equal(10, c.Last().v);
    }

    [Fact]
    public void ADDUpdating_Test()
    {
        TSeries a = new() { 0, 1, 2, 3, 4, 5 };
        TSeries b = new() { 5, 4, 3, 2, 1, 0 };
        ADD_Series c = new(a, b);
        a.Add(10, true);
        b.Add(10, true);
        Assert.Equal(20, c.Last().v);
    }
}
