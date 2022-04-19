using Xunit;
using System;
using QuanTAlib;

namespace Basics;
public class MUL_Test
{
	[Fact]
	public void MULSeriesSeries_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 1 };
		MUL_Series c = new(a, b);
		Assert.Equal(5, c.Last().v);
	}

	[Fact]
	public void MULSeriesDouble_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		MUL_Series c = new(a, 10.0);
		Assert.Equal(50, c.Last().v);
	}

	[Fact]
	public void MULDoubleSeries_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		MUL_Series c = new(5.0, a);
		Assert.Equal(25, c.Last().v);
	}

	[Fact]
	public void MULEventing_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 0 };
		MUL_Series c = new(a, b);
		a.Add(2);
		b.Add(5);
		Assert.Equal(10, c.Last().v);
	}

	[Fact]
	public void MULUpdateDouble_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		double b = 10;
		MUL_Series c = new(a, b);
		a.Add(2, true);
		Assert.Equal(20, c.Last().v);
	}

	[Fact]
	public void MULUpdating_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		TSeries b = new() { 5, 4, 3, 2, 1, 0 };
		MUL_Series c = new(a, b);
		a.Add(10, true);
		b.Add(10, true);
		Assert.Equal(100, c.Last().v);
	}
}
