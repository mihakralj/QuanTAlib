using Xunit;
using System;
using QuanTAlib;

namespace Basics;
public class SUB_Test
{
	[Fact]
	public void SUBSeriesSeries_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 1 };
		SUB_Series c = new(a, b);
		Assert.Equal(4, c.Last().v);
	}

	[Fact]
	public void SUBSeriesDouble_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 15.0 };
		SUB_Series c = new(a, 10.0);
		Assert.Equal(5.0, c.Last().v);
	}

	[Fact]
	public void SUBDoubleSeries_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 15.0 };
		SUB_Series c = new(10.0, a);
		Assert.Equal(-5.0, c.Last().v);
	}

	[Fact]
	public void SUBEventing_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 0 };
		SUB_Series c = new(a, b);
		a.Add(7.0);
		b.Add(2);
		Assert.Equal(5.0, c.Last().v);
	}

	[Fact]
	public void SUBUpdatewDouble_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 15 };
		double b = 10;
		SUB_Series c = new(a, b);
		a.Add(1, true);
		Assert.Equal(-9, c.Last().v);
	}

	[Fact]
	public void SUBUpdating_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 1 };
		SUB_Series c = new(a, b);
		a.Add(10, true);
		b.Add(0, true);
		Assert.Equal(10, c.Last().v);
	}
}
