using Xunit;
using System;
using QuanTAlib;

namespace Basics;
public class TSeries_Test
{
    [Fact]
    public void InsertingTuple()
    {
        TSeries s = new() { (t: DateTime.Today, v: double.Epsilon) };
        Assert.Equal((DateTime.Today, double.Epsilon), s);
    }

    [Fact]
    public void CastingTwoParameters()
    {
        TSeries s = new()
        {
            { DateTime.Today, 0.0 }
        };
        Assert.Equal(0.0, s[s.Count - 1].v);
        Assert.Equal(DateTime.Today, s[s.Count - 1].t);
    }

    [Fact]
    public void CastingOneParameter()
    {
        TSeries s = new()
        {
            double.PositiveInfinity
        };
        Assert.Equal(double.PositiveInfinity, (double)s);
    }

    [Fact]
    public void UpdatingValue()
    {
        TSeries s = new() { 1, 2, 3, 4, 5 };
        s.Add(0.0, update: true);
        Assert.Equal(0.0, (double)s);
        Assert.Equal(5, s.Count);
    }
    [Fact]
    public void ReflectingSeries()
    {
        TSeries s = new() { 1, 2, 3, 4, 5 };
        TSeries t = s;
        Assert.Equal(5, (double)t);
        Assert.Equal(5, t.Count);

    }
    [Fact]
    public void BroadcastingEvents()
    {
        TSeries s = new() { 1, 2, 3, 4, 5 };
        TSeries t = new();
        s.Pub += t.Sub;
        s.Add(0.0, update: true);
        Assert.Equal(0.0, (double)t);

    }
}
