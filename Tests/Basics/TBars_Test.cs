using Xunit;
using System;
using QuanTAlib;

namespace Basics;
public class TBars_Test
{
    [Fact]
    public void InsertingTuple()
    {
        TBars s = new() { (t: DateTime.Today, o: double.Epsilon, h: double.NaN, l: Double.MaxValue, c: Double.NegativeInfinity, v: Double.PositiveInfinity) };
        var tup = (t: DateTime.Today, o: double.Epsilon, h: double.NaN, l: Double.MaxValue,
	        c: Double.NegativeInfinity, v: Double.PositiveInfinity);
        Assert.Equal(tup, s[^1]);
    }

    [Fact]
    public void Casting_Parameters()
    {
		TBars s = new()
		{
			{ DateTime.Today, 0.1, 1.1, 2.1, 3.1, 4.1, false }
		};
		Assert.Equal(0.1, s[^1].o);
			Assert.Equal(1.1, s[^1].h);
			Assert.Equal(2.1, s[^1].l);
			Assert.Equal(3.1, s[^1].c);
			Assert.Equal(4.1, s[^1].v);
			Assert.Equal(DateTime.Today, s[^1].t);
			Assert.Single(s);
  }

    [Fact]
    public void Updating_Value()
    {
		TBars s = new()
		{
			{ DateTime.Today, 0.1, 1.1, 2.1, 3.1, 4.1 }
		};
		s.Add(DateTime.Today, 1.0, 1.0, 1.0, 1.0, 1.0, update: false);
			s.Add(DateTime.Today, 0.0, 0.0, 0.0, 0.0, 0.0, update: true);
      Assert.Equal(0.0, s[^1].o);
      Assert.Equal(0.0, s[^1].h);
      Assert.Equal(0.0, s[^1].l);
      Assert.Equal(0.0, s[^1].c);
      Assert.Equal(0.0, s[^1].v);
			Assert.Equal(2, s.Count);
    }
    [Fact]
    public void Extracting_TSeries()
    {
		TBars s = new()
		{
			{ DateTime.Today, 0.1, 1.1, 2.1, 3.1, 4.1 },
			{ DateTime.Today, 2.1, 3.1, 4.1, 5.1, 6.1 }
		};

		TSeries t = s.Open;
        Assert.Equal(t.t, s.Open.t);
        Assert.Equal(t.v, s.Open.v);

        t = s.High;
        Assert.Equal(t.t, s.High.t);
        Assert.Equal(t.v, s.High.v);

        t = s.Low;
        Assert.Equal(t.t, s.Low.t);
        Assert.Equal(t.v, s.Low.v);

        t = s.Close;
        Assert.Equal(t.t, s.Close.t);
        Assert.Equal(t.v, s.Close.v);

        t = s.Volume;
        Assert.Equal(t.t, s.Volume.t);
        Assert.Equal(t.v, s.Volume.v);

        t = s.HL2;
        Assert.Equal(t.t, s.HL2.t);
        Assert.Equal(t.v, s.HL2.v);

        t = s.OC2;
        Assert.Equal(t.t, s.OC2.t);
        Assert.Equal(t.v, s.OC2.v);

        t = s.OHL3;
        Assert.Equal(t.t, s.OHL3.t);
        Assert.Equal(t.v, s.OHL3.v);

        t = s.HLC3;
        Assert.Equal(t.t, s.HLC3.t);
        Assert.Equal(t.v, s.HLC3.v);

				t = s.OHLC4;
				Assert.Equal(t.t, s.OHLC4.t);
				Assert.Equal(t.v, s.OHLC4.v);

				t = s.HLCC4;
				Assert.Equal(t.t, s.HLCC4.t);
				Assert.Equal(t.v, s.HLCC4.v);
  }
    [Fact]
    public void Broadcasting_Events()
    {
      TBars s = new() { (DateTime.Today, 2.1, 3.1, 4.1, 5.1, 6.1) };
      TSeries t = new();
      s.Close.Pub += t.Sub;
			s.Add(DateTime.Today, 0.1, 1.1, 2.1, 3.1, 4.1, false);
			Assert.Equal(s.Close.v, t.v);
			Assert.Equal(s.Close.Count, t.Count);

  }
}
