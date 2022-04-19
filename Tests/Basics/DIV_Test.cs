using Xunit;
using System;
using QuanTAlib;

namespace Basics;
public class DIV_Test
{
	[Fact]
	public void DIVSeriesSeries_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 15 };
		TSeries b = new() { 5, 4, 3, 2, 1, 3 };
		DIV_Series c = new(a, b);
		Assert.Equal(5, c.Last().v);
	}

	[Fact]
	public void DIVSeriesDouble_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 15.0 };
		DIV_Series c = new(a, 0);
		Assert.Equal(double.PositiveInfinity, c.Last().v);
	}

	[Fact]
	public void DIVDoubleSeries_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 3.0 };
		DIV_Series c = new(12.0, a);
		Assert.Equal(4.0, c.Last().v);
	}

	[Fact]
	public void DIVEventing_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 0 };
		DIV_Series c = new(a, b);
		a.Add(12.0);
		b.Add(2);
		Assert.Equal(6.0, c.Last().v);
	}

	[Fact]
	public void DIVUpdatewDouble_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 15 };
		double b = 2;
		DIV_Series c = new(a, b);
		a.Add(10, true);
		Assert.Equal(5, c.Last().v);
	}

	[Fact]
	public void DIVUpdating_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 1 };
		DIV_Series c = new(a, b);
		a.Add(10, true);
		b.Add(2, true);
		Assert.Equal(5, c.Last().v);
	}
}
