using Xunit;
using System;
using Skender.Stock.Indicators;
using QuanTAlib;


namespace Validation;
public class Skender_Stock
{
	private readonly RND_Feed bars;
	private readonly Random rnd = new();
	private readonly int period;
	private readonly IEnumerable<Quote> quotes;

	public Skender_Stock()
	{
		this.bars = new(1000);
		this.period = this.rnd.Next(28) + 3;
		this.quotes = this.bars.Select(
			q => new Quote
			{
				Date = q.t,
				Open = (decimal)q.o,
				High = (decimal)q.h,
				Low = (decimal)q.l,
				Close = (decimal)q.c,
				Volume = (decimal)q.v
			});
	}

	[Fact]
	public void SMA()
	{
		SMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetSma(this.period, CandlePart.Close);

		Assert.Equal(Math.Round((double)SK.Last().Sma!, 8), Math.Round(QL.Last().v, 8));
	}

		[Fact]
	public void EMA()
	{
		EMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetEma(this.period, CandlePart.Close);

		Assert.Equal(Math.Round((double)SK.Last().Ema!, 8), Math.Round(QL.Last().v, 8));
	}
	[Fact]
	public void WMA()
	{
		WMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetWma(this.period, CandlePart.Close);

		Assert.Equal(Math.Round((double)SK.Last().Wma!, 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void DEMA()
	{
		DEMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetDema(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Dema!, 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void TEMA()
	{
		TEMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetTema(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Tema!, 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void MAD()
	{
		MAD_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetSmaExtended(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Mad!, 8), Math.Round(QL.Last().v, 8));
	}

	[Fact]
	public void MAPE()
	{
		MAPE_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetSmaExtended(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Mape!, 8), Math.Round(QL.Last().v, 8));
	}

}
