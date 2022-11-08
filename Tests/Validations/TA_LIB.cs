using Xunit;
using System;
using TALib;
using QuanTAlib;

namespace Validation;
public class TA_LIB
{
	private readonly GBM_Feed bars;
	private readonly Random rnd = new();
	private readonly int period;
	private readonly double[] TALIB;
	private readonly double[] inopen;
	private readonly double[] inhigh;
	private readonly double[] inlow;
	private readonly double[] inclose;
	private readonly double[] involume;

	public TA_LIB()
	{
		this.bars = new(1000);
		this.period = this.rnd.Next(28) + 3;
		this.TALIB = new double[this.bars.Count];
		this.inopen = this.bars.Open.v.ToArray();
		this.inhigh = this.bars.High.v.ToArray();
		this.inlow = this.bars.Low.v.ToArray();
		this.inclose = this.bars.Close.v.ToArray();
		this.involume = this.bars.Volume.v.ToArray();
	}

	/////////////////////////////////////////

	[Fact]
	public void SDEV()
	{
		SDEV_Series QL = new(this.bars.Close, this.period, false);
		Core.StdDev(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void SMA()
	{
		SMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Sma(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void EMA()
	{
		EMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Ema(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void WMA()
	{
		WMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Wma(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void DEMA()
	{
		DEMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Dema(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void TEMA()
	{
		TEMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Tema(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void MAX()
	{
		MAX_Series QL = new(this.bars.Close, this.period, false);
		Core.Max(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void MIN()
	{
		MIN_Series QL = new(this.bars.Close, this.period, false);
		Core.Min(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void ADL()
	{
		ADL_Series QL = new(this.bars, false);
		Core.Ad(this.inhigh, this.inlow, this.inclose, this.involume, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void ADO()
	{
		ADO_Series QL = new(this.bars, false);
		Core.AdOsc(this.inhigh, this.inlow, this.inclose, this.involume, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void ATR()
	{
		ATR_Series QL = new(this.bars, this.period, false);
		Core.Atr(this.inhigh, this.inlow, this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void CCI()
	{
		CCI_Series QL = new(this.bars, this.period, false);
		Core.Cci(this.inhigh, this.inlow, this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void RSI()
	{
		RSI_Series QL = new(this.bars.Close, this.period, false);
		Core.Rsi(this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void TR()
	{
		TR_Series QL = new(this.bars, false);
		Core.TRange(this.inhigh, this.inlow, this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void MACD()
	{ 
		double[] macdSignal = new double[this.bars.Count];
		double[] macdHist = new double[this.bars.Count];
		MACD_Series QL = new(this.bars.Close, slow: 26, fast: 12, signal: 9, false);
		Core.Macd(this.inclose, 0, this.bars.Count - 1, outMacd: this.TALIB, outMacdSignal: macdSignal, outMacdHist: macdHist, out int outBegIdx, out _);
		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void HL2()
	{
		TSeries QL = this.bars.HL2;
		Core.MedPrice(this.inhigh, this.inlow, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void HLC3()
	{
		TSeries QL = this.bars.HLC3;
		Core.TypPrice(this.inhigh, this.inlow, this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void OHLC4()
	{
		TSeries QL = this.bars.OHLC4;
		Core.AvgPrice(this.inopen, this.inhigh, this.inlow, this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void HLCC4()
	{
		TSeries QL = this.bars.HLCC4;
		Core.WclPrice( this.inhigh, this.inlow, this.inclose, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

}
