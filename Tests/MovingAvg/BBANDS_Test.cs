using Xunit;
using System;
using QuanTAlib;

namespace MovingAvg;
public class BBANDS_Test
{
	[Fact]
	public void Add_Test()
	{
		TSeries a = new() { 0, 1, 2, 3, 4, 5 };
		BBANDS_Series c = new(a, 4,2.5);
		Assert.Equal(6, c.Count);
		a.Add(5);
		Assert.Equal(a.Count, c.Count);
		Assert.Equal(a.Count, c.Mid.Count);
		Assert.Equal(a.Count, c.Upper.Count);
		Assert.Equal(a.Count, c.Lower.Count);
		Assert.Equal(a.Count, c.PercentB.Count);
		Assert.Equal(a.Count, c.Zscore.Count);
		Assert.Equal(a.Count, c.Bandwidth.Count);

		a.Add(0, update: true);
		Assert.Equal(a.Count, c.Count);
		Assert.Equal(a.Count, c.Mid.Count);
		Assert.Equal(a.Count, c.Upper.Count);
		Assert.Equal(a.Count, c.Lower.Count);
		Assert.Equal(a.Count, c.PercentB.Count);
		Assert.Equal(a.Count, c.Zscore.Count);
		Assert.Equal(a.Count, c.Bandwidth.Count);
	}

	[Fact]
	public void Edge_Test()
	{
		TSeries a = new() { double.NaN, double.Epsilon, double.PositiveInfinity, double.MaxValue };
		BBANDS_Series c = new(a, 4, 2.5);
		Assert.Equal(a.Count, c.Count);
		a.Add(double.NaN);
		Assert.Equal(a.Count, c.Count);
		Assert.Equal(a.Count, c.Mid.Count);
		Assert.Equal(a.Count, c.Upper.Count);
		Assert.Equal(a.Count, c.Lower.Count);
		Assert.Equal(a.Count, c.PercentB.Count);
		Assert.Equal(a.Count, c.Zscore.Count);
		Assert.Equal(a.Count, c.Bandwidth.Count);
		a.Add(double.PositiveInfinity);
		Assert.Equal(a.Count, c.Count);
		Assert.Equal(a.Count, c.Mid.Count);
		Assert.Equal(a.Count, c.Upper.Count);
		Assert.Equal(a.Count, c.Lower.Count);
		Assert.Equal(a.Count, c.PercentB.Count);
		Assert.Equal(a.Count, c.Zscore.Count);
		Assert.Equal(a.Count, c.Bandwidth.Count);

	}

}
