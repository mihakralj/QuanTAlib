using Xunit;
using System;
using QuanTAlib;

namespace MovingAvg;
public class DWMA_Test
{
	[Fact]
	public void Add_Test()
	{
		TSeries a = new() { Double.NaN, 0, 1, 2, 3, 4 };
		DWMA_Series c = new(a, 3);
		Assert.Equal(6, c.Count);
		a.Add(5);
		Assert.Equal(a.Count, c.Count);
		a.Add(0, update: true);
		Assert.Equal(a.Count, c.Count);
		Assert.Equal(0, a[1].v);
	}

	[Fact]
	public void Edge_Test()
	{
		TSeries a = new() { double.NaN, double.Epsilon, double.PositiveInfinity, double.MaxValue };
		DWMA_Series c = new(a, 3);
		Assert.Equal(a.Count, c.Count);
		a.Add(double.NaN);
		Assert.Equal(a.Count, c.Count);
		a.Add(double.PositiveInfinity);
		Assert.Equal(a.Count, c.Count);
	}
}
