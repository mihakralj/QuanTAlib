using System;
using QuanTAlib;
using Skender.Stock.Indicators;
using Xunit;

namespace Validations;
public class Skender_Stock
{
  private readonly GBM_Feed bars;
  private readonly Random rnd = new();
  private readonly int period;
  private readonly IEnumerable<Quote> quotes;

  public Skender_Stock()
  {
    bars = new(Bars: 5000, Volatility: 0.7, Drift: 0.0);
    period = rnd.Next(28) + 3;
    quotes = bars.Select(
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
    SMA_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetSma(period);

    Assert.Equal(Math.Round((double)SK.Last().Sma!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void EMA()
  {
    EMA_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetEma(period);

    Assert.Equal(Math.Round((double)SK.Last().Ema!, 6), Math.Round(QL.Last().v, 6));
  }
  [Fact]
  public void WMA()
  {
    WMA_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetWma(period);

    Assert.Equal(Math.Round((double)SK.Last().Wma!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void DEMA()
  {
    DEMA_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetDema(period);

    Assert.Equal(Math.Round((double)SK.Last().Dema!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void TEMA()
  {
    TEMA_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetTema(period);

    Assert.Equal(Math.Round((double)SK.Last().Tema!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void MAD()
  {
    MAD_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetSmaAnalysis(period);

    Assert.Equal(Math.Round((double)SK.Last().Mad!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void MSE()
  {
    MSE_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetSmaAnalysis(period);

    Assert.Equal(Math.Round((double)SK.Last().Mse!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void MAPE()
  {
    MAPE_Series QL = new(bars.Close, period, false);
    var SK = quotes.GetSmaAnalysis(period);

    Assert.Equal(Math.Round((double)SK.Last().Mape!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void COVAR()
  {
    COVAR_Series QL = new(bars.High, bars.Low, period, false);
    var SK = quotes.Use(CandlePart.High).GetCorrelation(quotes.Use(CandlePart.Low), period);

    Assert.Equal(Math.Round((double)SK.Last().Covariance!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void CORR()
  {
    CORR_Series QL = new(bars.High, bars.Low, period, false);
    var SK = quotes.Use(CandlePart.High).GetCorrelation(quotes.Use(CandlePart.Low), period);

    Assert.Equal(Math.Round((double)SK.Last().Correlation!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void ATR()
  {
    ATR_Series QL = new(bars, period, false);
    var SK = quotes.GetAtr(period);

    Assert.Equal(Math.Round((double)SK.Last().Atr!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void OBV()
  {
    OBV_Series QL = new(bars, period, false);
    var SK = quotes.GetObv(period);

    // adding volume[0] to OBV to pass the test and keep compatibility with TA-LIB
    Assert.Equal(Math.Round(SK.Last().Obv! + (double)quotes.First().Volume!, 5),
        Math.Round(QL.Last().v, 5));
  }

  [Fact]
  public void ADL()
  {
    ADL_Series QL = new(bars, false);
    var SK = quotes.GetAdl();

    Assert.Equal(Math.Round(SK.Last().Adl!, 5), Math.Round(QL.Last().v, 5));
  }

  [Fact]
  public void CCI()
  {
    CCI_Series QL = new(bars, period, false);
    var SK = quotes.GetCci(period);

    Assert.Equal(Math.Round((double)SK.Last().Cci!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void ATRP()
  {
    ATRP_Series QL = new(bars, period, false);
    var SK = quotes.GetAtr(period);

    Assert.Equal(Math.Round((double)SK.Last().Atrp!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void KAMA()
  {
    KAMA_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetKama(period);

    Assert.Equal(Math.Round((double)SK.Last().Kama!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void HMA()
  {
    HMA_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetHma(period);

    Assert.Equal(Math.Round((double)SK.Last().Hma!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void SMMA()
  {
    SMMA_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetSmma(period);

    Assert.Equal(Math.Round((double)SK.Last().Smma!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void MACD()
  {
    MACD_Series QL = new(bars.Close, 26, 12, 9, useNaN: false);
    var SK = quotes.GetMacd(12, 26, 9);

    Assert.Equal(Math.Round((double)SK.Last().Macd!, 6), Math.Round(QL.Last().v, 6));
    Assert.Equal(Math.Round((double)SK.Last().Signal!, 6), Math.Round(QL.Signal.Last().v, 6));
  }

  [Fact]
  public void BBANDS()
  {
    BBANDS_Series QL = new(bars.Close, period, 2.0, useNaN: false);
    var SK = quotes.GetBollingerBands(period, 2.0);

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
    RSI_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetRsi(period);

    Assert.Equal(Math.Round((double)SK.Last().Rsi!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void ALMA()
  {
    ALMA_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetAlma(period);

    Assert.Equal(Math.Round((double)SK.Last().Alma!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void SDEV()
  {
    SDEV_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetStdDev(period);

    Assert.Equal(Math.Round((double)SK.Last().StdDev!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void ZSCORE()
  {
    ZSCORE_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetStdDev(period);

    Assert.Equal(Math.Round((double)SK.Last().ZScore!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void LINREG()
  {
    LINREG_Series QL = new(bars.Close, period, useNaN: false);
    var SK = quotes.GetSlope(period);

    Assert.Equal(Math.Round((double)SK.Last().Slope!, 6), Math.Round(QL.Last().v, 6));
    Assert.Equal(Math.Round((double)SK.Last().Intercept!, 6), Math.Round(QL.Intercept.Last().v, 6));
    Assert.Equal(Math.Round((double)SK.Last().RSquared!, 6), Math.Round(QL.RSquared.Last().v, 6));
    Assert.Equal(Math.Round((double)SK.Last().StdDev!, 6), Math.Round(QL.StdDev.Last().v, 6));
  }

  [Fact]
  public void TR()
  {
    TR_Series QL = new(bars, useNaN: false);
    var SK = quotes.GetTr();

    Assert.Equal(Math.Round((double)SK.Last().Tr!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void HL2()
  {
    TSeries QL = bars.HL2;
    var SK = quotes.GetBaseQuote(CandlePart.HL2);

    Assert.Equal(Math.Round(SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void OC2()
  {
    TSeries QL = bars.OC2;
    var SK = quotes.GetBaseQuote(CandlePart.OC2);

    Assert.Equal(Math.Round(SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void HLC3()
  {
    TSeries QL = bars.HLC3;
    var SK = quotes.GetBaseQuote(CandlePart.HLC3);

    Assert.Equal(Math.Round(SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void OHL3()
  {
    TSeries QL = bars.OHL3;
    var SK = quotes.GetBaseQuote(CandlePart.OHL3);

    Assert.Equal(Math.Round(SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
  }

  [Fact]
  public void OHLC4()
  {
    TSeries QL = bars.OHLC4;
    var SK = quotes.GetBaseQuote(CandlePart.OHLC4);

    Assert.Equal(Math.Round((double)SK.Last().Value!, 6), Math.Round(QL.Last().v, 6));
  }
}
