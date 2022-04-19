using Xunit;
using System;
using TALib;
using QuanTAlib;

namespace Validation;
public class TA_LIB
{
	private readonly RND_Feed bars;
	private readonly Random rnd = new();
	private readonly int period;
	private readonly double[] TALIB;
	private readonly double[] input;

	public TA_LIB()
	{
		this.bars = new(1000);
		this.period = this.rnd.Next(28) + 3;
		this.TALIB = new double[this.bars.Count];
		this.input = this.bars.Close.v.ToArray();
	}

/////////////////////////////////////////

	[Fact]
	public void SMA()
	{
		SMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Sma(this.input, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void EMA()
	{
		EMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Ema(this.input, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void WMA()
	{
		WMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Wma(this.input, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void DEMA()
	{
		DEMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Dema(this.input, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void TEMA()
	{
		TEMA_Series QL = new(this.bars.Close, this.period, false);
		Core.Tema(this.input, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void MAX()
	{
		MAX_Series QL = new(this.bars.Close, this.period, false);
		Core.Max(this.input, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void MIN()
	{
		MIN_Series QL = new(this.bars.Close, this.period, false);
		Core.Min(this.input, 0, this.bars.Count - 1, this.TALIB, out int outBegIdx, out _, this.period);

		Assert.Equal(Math.Round(this.TALIB[this.TALIB.Length - outBegIdx - 1], 8), Math.Round(QL.Last().v, 8));
	}

}
