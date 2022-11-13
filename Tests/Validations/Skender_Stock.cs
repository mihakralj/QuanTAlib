using System;
using QuanTAlib;
using Skender.Stock.Indicators;
using Xunit;

namespace Validation;
public class Skender_Stock
{
	private readonly GBM_Feed bars;
	private readonly Random rnd = new();
	private readonly int period;
	private readonly IEnumerable<Quote> quotes;

	public Skender_Stock()
	{
		this.bars = new(Bars: 5000, Volatility:0.7, Drift:0.0);
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
		var SK = this.quotes.GetSma(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Sma!, 6), Math.Round(QL.Last().v, 6));
	}

		[Fact]
	public void EMA()
	{
		EMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetEma(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Ema!, 6), Math.Round(QL.Last().v, 6));
	}
	[Fact]
	public void WMA()
	{
		WMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetWma(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Wma!, 6), Math.Round(QL.Last().v, 6));
	}

	[Fact]
	public void DEMA()
	{
		DEMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetDema(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Dema!, 6), Math.Round(QL.Last().v, 6));
	}

	[Fact]
	public void TEMA()
	{
		TEMA_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetTema(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Tema!, 6), Math.Round(QL.Last().v, 6));
	}

	[Fact]
	public void MAD()
	{
		MAD_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetSmaAnalysis(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Mad!, 6), Math.Round(QL.Last().v, 6));
	}

	[Fact]
	public void MAPE()
	{
		MAPE_Series QL = new(this.bars.Close, this.period, false);
		var SK = this.quotes.GetSmaAnalysis(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Mape!, 6), Math.Round(QL.Last().v, 6));
	}

	[Fact]
	public void ATR()
	{
		ATR_Series QL = new(this.bars, this.period, false);
		var SK = this.quotes.GetAtr(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Atr!, 6), Math.Round(QL.Last().v, 6));
	}

	[Fact]
	public void OBV()
	{
		OBV_Series QL = new(this.bars, this.period, false);
		var SK = this.quotes.GetObv(this.period);

		// adding volume[0] to OBV to pass the test and keep compatibility with TA-LIB
		Assert.Equal(Math.Round(SK.Last().Obv! + (double)this.quotes.First().Volume!, 5),
			Math.Round(QL.Last().v, 5));
	}

	[Fact]
	public void ADL()
	{
		ADL_Series QL = new(this.bars, false);
		var SK = this.quotes.GetAdl();

		Assert.Equal(Math.Round((double)SK.Last().Adl!, 5), Math.Round(QL.Last().v, 5));
	}

	[Fact]
	public void CCI()
	{
		CCI_Series QL = new(this.bars, this.period, false);
		var SK = this.quotes.GetCci(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Cci!, 6), Math.Round(QL.Last().v, 6));
	}

	[Fact]
	public void ATRP()
	{
		ATRP_Series QL = new(this.bars, this.period, false);
		var SK = this.quotes.GetAtr(this.period);

		Assert.Equal(Math.Round((double)SK.Last().Atrp!, 6), Math.Round(QL.Last().v, 6));
	}

    [Fact]
    public void KAMA()
    {
        KAMA_Series QL = new(this.bars.Close, this.period, useNaN: false);
        var SK = this.quotes.GetKama(this.period);

        Assert.Equal(Math.Round((double)SK.Last().Kama!, 6), Math.Round(QL.Last().v, 6));
    }

    [Fact]
    public void HMA()
    {
	    HMA_Series QL = new(this.bars.Close, this.period, useNaN: false);
	    var SK = this.quotes.GetHma(this.period);

	    Assert.Equal(Math.Round((double)SK.Last().Hma!, 6), Math.Round(QL.Last().v, 6));
    }

	[Fact]
    public void SMMA()
    {
	    SMMA_Series QL = new(this.bars.Close, this.period, useNaN: false);
	    var SK = this.quotes.GetSmma(this.period);

	    Assert.Equal(Math.Round((double)SK.Last().Smma!, 6), Math.Round(QL.Last().v, 6));
    }

    [Fact]
    public void MACD()
    {
	    MACD_Series QL = new(this.bars.Close, 26,12,9, useNaN: false);
	    var SK = this.quotes.GetMacd(12,26,9);

	    Assert.Equal(Math.Round((double)SK.Last().Macd!, 6), Math.Round(QL.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().Signal!, 6), Math.Round(QL.Signal.Last().v, 6));
    }

    [Fact]
    public void BBANDS()
    {
	    BBANDS_Series QL = new(this.bars.Close, this.period, 2.0, useNaN: false);
	    var SK = this.quotes.GetBollingerBands(this.period, 2.0);

	    Assert.Equal(Math.Round((double)SK.Last().Sma!, 6), Math.Round(QL.Mid.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().UpperBand!, 6), Math.Round(QL.Upper.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().LowerBand!, 6), Math.Round(QL.Lower.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().Width!, 6), Math.Round(QL.Bandwidth.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().PercentB!, 6), Math.Round(QL.PercentB.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().ZScore!, 6), Math.Round(QL.Zscore.Last().v, 6));
    }

	[Fact]
    public void RSI()
    {
	    RSI_Series QL = new(this.bars.Close, this.period, useNaN: false);
	    var SK = this.quotes.GetRsi(this.period);

	    Assert.Equal(Math.Round((double)SK.Last().Rsi!, 6), Math.Round(QL.Last().v, 6));
    }

    [Fact]
    public void ALMA()
    {
	    ALMA_Series QL = new(this.bars.Close, this.period, useNaN: false);
	    var SK = this.quotes.GetAlma(this.period);

	    Assert.Equal(Math.Round((double)SK.Last().Alma!, 6), Math.Round(QL.Last().v, 6));
    }

    [Fact]
    public void SDEV()
    {
	    SDEV_Series QL = new(this.bars.Close, this.period, useNaN: false);
	    var SK = this.quotes.GetStdDev(this.period);

	    Assert.Equal(Math.Round((double)SK.Last().StdDev!, 6), Math.Round(QL.Last().v, 6));
    }

	[Fact]
    public void LINREG()
    {
	    LINREG_Series QL = new(this.bars.Close, this.period, useNaN: false);
	    var SK = this.quotes.GetSlope(this.period);

	    Assert.Equal(Math.Round((double)SK.Last().Slope!, 6), Math.Round(QL.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().Intercept!, 6), Math.Round(QL.Intercept.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().RSquared!, 6), Math.Round(QL.RSquared.Last().v, 6));
	    Assert.Equal(Math.Round((double)SK.Last().StdDev!, 6), Math.Round(QL.StdDev.Last().v, 6));
	}

    [Fact]
    public void TR()
    {
	    TR_Series QL = new(this.bars, useNaN: false);
	    var SK = this.quotes.GetTr();

	    Assert.Equal(Math.Round((double)SK.Last().Tr!, 6), Math.Round(QL.Last().v, 6));
    }

	[Fact]
    public void HL2()
    {
	    TSeries QL = this.bars.HL2;
	    var SK = this.quotes.GetBaseQuote(CandlePart.HL2);

	    Assert.Equal(Math.Round((double)SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
    }

    [Fact]
    public void OC2()
    {
	    TSeries QL = this.bars.OC2;
	    var SK = this.quotes.GetBaseQuote(CandlePart.OC2);

	    Assert.Equal(Math.Round((double)SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
    }

	[Fact]
    public void HLC3()
    {
	    TSeries QL = this.bars.HLC3;
	    var SK = this.quotes.GetBaseQuote(CandlePart.HLC3);

	    Assert.Equal(Math.Round((double)SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
    }

    [Fact]
    public void OHL3()
    {
	    TSeries QL = this.bars.OHL3;
	    var SK = this.quotes.GetBaseQuote(CandlePart.OHL3);

	    Assert.Equal(Math.Round((double)SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
    }

    [Fact]
    public void OHLC4()
    {
	    TSeries QL = this.bars.OHLC4;
	    var SK = this.quotes.GetBaseQuote(CandlePart.OHLC4);

	    Assert.Equal(Math.Round((double)SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
    }
}
