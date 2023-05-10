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
    skip = period+5;
    digits = 8;

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
		ADL_Series QL = new(bars);
		Tulip.Indicators.ad.Run(inputs: arrin, options: new double[] { }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
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
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
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
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i-period+1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void ATR()
	{
		double[][] arrin = { inhigh, inlow, inclose };
		double[][] arrout = { outdata };

		ATR_Series QL = new(bars, period:period, useNaN:false);
		Tulip.Indicators.atr.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		//Tulip ATR doesn't use warm-up SMA, compensating with 200 warming bars
		for (int i = QL.Length - 1; i > 200+skip; i--)
		{
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
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
			double QL_item = QL.Lower[i].v;
			double TU_item = outlower[i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
			QL_item = QL.Mid[i].v;
			TU_item = outmid[i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
			QL_item = QL.Upper[i].v;
			TU_item = outupper[i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	/*
	[Fact]
	public void CCI() {
		double[][] arrin = { inhigh, inlow, inclose };
		double[][] arrout = { outdata };
		CCI_Series QL = new(bars, period, useNaN: false);
		Tulip.Indicators.cci.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = outdata[i - period + 1];
			Assert.Equal(QL_item,TU_item);
			//Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	*/
	[Fact]
	public void CMO() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		CMO_Series QL = new(bars.Close, period, useNaN: false);
		Tulip.Indicators.cmo.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void DECAY() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		DECAY_Series QL = new(bars.Close, period, useNaN: false);
		Tulip.Indicators.decay.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip + 200; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void DEMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		DEMA_Series QL = new(bars.Close, period, useNaN: false, useSMA: false);
		Tulip.Indicators.dema.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip+200; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i-(period+period-2)];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void DIV() {
		double[][] arrin = { inhigh, inlow };
		double[][] arrout = { outdata };
		DIV_Series QL = new(bars.High, bars.Low);
		Tulip.Indicators.div.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void EDECAY() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		DECAY_Series QL = new(bars.Close, period, exponential: true, useNaN: false);
		Tulip.Indicators.edecay.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip + 200; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
  public void EMA()
  {
    double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		// Tulip EMA doesn't use SMA to warm-up
		EMA_Series QL = new(bars.Close, period, false, useSMA: false);
    Tulip.Indicators.ema.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
    for (int i = QL.Length - 1; i > skip; i--)
    {
      double QL_item = QL[i].v;
      double TU_item = arrout[0][i];
      Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
    }
  }
	[Fact]
	public void HL2() {
		double[][] arrin = { inhigh, inlow };
		double[][] arrout = { outdata };

		TSeries QL = bars.HL2;
		Tulip.Indicators.medprice.Run(inputs: arrin, options: new double[] { }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void HLC3() {
		double[][] arrin = { inhigh, inlow, inclose };
		double[][] arrout = { outdata };

		TSeries QL = bars.HLC3;
		Tulip.Indicators.typprice.Run(inputs: arrin, options: new double[] { }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void HLCC4() {
		double[][] arrin = { inhigh, inlow, inclose };
		double[][] arrout = { outdata };

		TSeries QL = bars.HLCC4;
		Tulip.Indicators.wcprice.Run(inputs: arrin, options: new double[] { }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	
	[Fact]
	public void HMA() {
		int p = 10;
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		HMA_Series QL = new(bars.Close, p, false);
		Tulip.Indicators.hma.Run(inputs: arrin, options: new double[] { p }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip+2; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - p - 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits-2), Math.Exp(-digits-2));
		}
	}
	
	[Fact]
	public void KAMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		KAMA_Series QL = new(bars.Close, period);
		Tulip.Indicators.kama.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > 250; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	
	[Fact]
	public void LINREG() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		SLOPE_Series QL = new(bars.Close, period);
		Tulip.Indicators.linregslope.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period+1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void MACD() {

		double[] outsignal = new double[bars.Count];
		double[] outhist = new double[bars.Count];
		double[][] arrin = { inclose };
		double[][] arrout = { outdata, outsignal, outhist };
		MACD_Series QL = new(bars.Close, slow: 26,fast: 10, signal: 9);
		Tulip.Indicators.macd.Run(inputs: arrin, options: new double[] { 10,26,9 }, outputs: arrout);
		for (int i = QL.Length - 1; i > 150; i--) {
			double QL_item = QL[i].v;
			double TU_item =outdata[i - 26+1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void MAX() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		MAX_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.max.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i-period+1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void MIN() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		MIN_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.min.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void MUL() {
		double[][] arrin = { inhigh, inlow };
		double[][] arrout = { outdata };
		MUL_Series QL = new(bars.High, bars.Low);
		Tulip.Indicators.mul.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void OBV() {
		double[][] arrin = { inclose, involume };
		double[][] arrout = { outdata };
		OBV_Series QL = new(bars, period, false);
		Tulip.Indicators.obv.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i] + arrin[1][0];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void OHLC4()
	{
		double[][] arrin = { inopen, inhigh, inlow, inclose };
		double[][] arrout = { outdata };

		TSeries QL = bars.OHLC4;
		Tulip.Indicators.avgprice.Run(inputs: arrin, options: new double[] { }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--)
		{
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void RMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		RMA_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.wilders.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void RSI() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		RSI_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.rsi.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period];
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
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i-period+1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void SDEV() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		SDEV_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.stddev.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void SUB() {
		double[][] arrin = { inhigh, inlow };
		double[][] arrout = { outdata };
		SUB_Series QL = new(bars.High, bars.Low);
		Tulip.Indicators.sub.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void SUM() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		CUSUM_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.sum.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void TR() {
		double[][] arrin = { inhigh,inlow,inclose };
		double[][] arrout = { outdata };
		TR_Series QL = new(bars);
		Tulip.Indicators.tr.Run(inputs: arrin, options: new double[] {}, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void TEMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		TEMA_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.tema.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip+200; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - (period-1)*3];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void TRIMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		TRIMA_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.trima.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	/*
	[Fact]
	public void TRIX() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		TRIX_Series QL = new(bars.Close, period);
		Tulip.Indicators.trix.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > period+200; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - (period*3) + 2];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits+2), Math.Exp(-digits+2));
		}
	}
	*/
	[Fact]
	public void VAR() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		VAR_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.var.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
	public void WMA() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		WMA_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.wma.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
		}
	}
	[Fact]
		public void ZLEMA() {
		int p = 4;
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		ZLEMA_Series QL = new(bars.Close, p, false);
		Tulip.Indicators.zlema.Run(inputs: arrin, options: new double[] { p }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip+20; i--) {
			double QL_item = QL[i].v;
			double TU_item = outdata[i];
			Assert.InRange(TU_item! - QL_item, -Math.Exp(-digits-2), Math.Exp(-digits-2));
		}
	}
}
