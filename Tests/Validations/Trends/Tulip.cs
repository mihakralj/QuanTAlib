using Xunit;
using System;
using Tulip;
using QuanTAlib;

namespace Validations;
public class Tulip_Test
{
  private readonly GBM_Feed bars;
  private readonly Random rnd = new();
  private readonly int period, digits, skip;
  private readonly double[] outdata;
  private readonly double[] inopen;
  private readonly double[] inhigh;
  private readonly double[] inlow;
  private readonly double[] inclose;
  private readonly double[] involume;

  public Tulip_Test()
  {
    bars = new(Bars: 5000, Volatility: 0.8, Drift: 0.0, Precision: 3);
    period = rnd.Next(28) + 3;
    skip = 200;
    digits = 10;

    outdata = new double[bars.Count];
    inopen = bars.Open.v.ToArray();
    inhigh = bars.High.v.ToArray();
    inlow = bars.Low.v.ToArray();
    inclose = bars.Close.v.ToArray()!;
    involume = bars.Volume.v.ToArray()!;

  }
	[Fact]
	public void ADL()
	{
		double[][] arrin = {inhigh, inlow, inclose, involume };
		double[][] arrout = { outdata };
		ADL_Series QL = new(bars, false);
		Tulip.Indicators.ad.Run(inputs: arrin, options: new double[] { }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void ADD()
	{
		double[][] arrin = { inhigh, inlow };
		double[][] arrout = { outdata };
		ADD_Series QL = new(bars.High, bars.Low);
		Tulip.Indicators.add.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void ADOSC()
	{
		double[][] arrin = { inhigh, inlow, inclose, involume };
		double[][] arrout = { outdata };
		int s = 3;
		ADOSC_Series QL = new(bars, s, period, false);
		Tulip.Indicators.adosc.Run(inputs: arrin, options: new double[] { s, period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) 
		{
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i-period+1], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void ATR()
	{
		double[][] arrin = { inhigh, inlow, inclose };
		double[][] arrout = { outdata };

		ATR_Series QL = new(bars, period, false);
		Tulip.Indicators.atr.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i - period + 1], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void BBANDS()
	{
		double[][] arrin = { inclose };
		double[] outmid = new double[bars.Count];
		double[] outlower = new double[bars.Count];
		double[] outupper = new double[bars.Count];
		double[][] arrout = { outlower, outmid, outupper};
		BBANDS_Series QL = new(bars.Close, period, 2, false);
		Tulip.Indicators.bbands.Run(inputs: arrin, options: new double[] { period, 2 }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = Math.Round(QL.Lower[i].v, digits: digits);
			double TU_item = Math.Round(outlower[i - period + 1], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
			QL_item = Math.Round(QL.Mid[i].v, digits: digits);
			TU_item = Math.Round(outmid[i - period + 1], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
			QL_item = Math.Round(QL.Upper[i].v, digits: digits);
			TU_item = Math.Round(outupper[i - period + 1], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	/*
	[Fact]
	public void DEMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		DEMA_Series QL = new(bars.Close, period, useNaN: false, useSMA: false);
		Tulip.Indicators.dema.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i-(period+period-2)], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	*/
	[Fact]
  public void EMA()
  {
    double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		EMA_Series QL = new(bars.Close, period, false);
    Tulip.Indicators.ema.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
    for (int i = QL.Length - 1; i > skip; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TU_item = Math.Round(arrout[0][i], digits);
      Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
    }
  }
	[Fact]
	public void AVGPRICE()
	{
		double[][] arrin = { inopen, inhigh, inlow, inclose };
		double[][] arrout = { outdata };

		TSeries QL = bars.OHLC4;
		Tulip.Indicators.avgprice.Run(inputs: arrin, options: new double[] { }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void SMA()
	{
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		SMA_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.sma.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i-period+1], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	/*
	[Fact]
	public void HMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		HMA_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.hma.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i-period-1], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}*/
	[Fact]
	public void CMO() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		CMO_Series QL = new(bars.Close, period, useNaN: false);
		Tulip.Indicators.cmo.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = Math.Round(QL[i].v, digits: digits);
			double TU_item = Math.Round(arrout[0][i-period], digits);
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
}
