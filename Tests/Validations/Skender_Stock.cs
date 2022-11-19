using System;
using QuanTAlib;
using Skender.Stock.Indicators;
using Xunit;

namespace Validations;
public class Skender_Stock {
    private readonly GBM_Feed bars;
    private readonly Random rnd = new();
    private readonly int period, digits;
    private readonly IEnumerable<Quote> quotes;

    public Skender_Stock() {
        bars = new(Bars: 5000, Volatility: 0.8, Drift: 0.0);
        period = rnd.Next(28) + 3;
        digits = 4; //minimizing rounding errors in type conversions

        quotes = bars.Select(q => new Quote {
            Date = q.t,
            Open = (decimal)q.o,
            High = (decimal)q.h,
            Low = (decimal)q.l,
            Close = (decimal)q.c,
            Volume = (decimal)q.v
        });
    }
    [Fact] public void ADL() {
        ADL_Series QL = new(bars, false);
        var SK = quotes.GetAdl();
        Assert.Equal(Math.Round(SK.Last().Adl!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void ALMA() {
        ALMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetAlma(period);
        Assert.Equal(Math.Round((double)SK.Last().Alma!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void ATR() {
        ATR_Series QL = new(bars, period, false);
        var SK = quotes.GetAtr(period);
        Assert.Equal(Math.Round((double)SK.Last().Atr!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void ATRP() {
        ATRP_Series QL = new(bars, period, false);
        var SK = quotes.GetAtr(period);
        Assert.Equal(Math.Round((double)SK.Last().Atrp!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void BBANDS() {
        BBANDS_Series QL = new(bars.Close, period, 2.0, useNaN: false);
        var SK = quotes.GetBollingerBands(period, 2.0);
        Assert.Equal(Math.Round((double)SK.Last().Sma!, digits: digits), Math.Round(QL.Mid.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().UpperBand!, digits: digits), Math.Round(QL.Upper.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().LowerBand!, digits: digits), Math.Round(QL.Lower.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().Width!, digits: digits), Math.Round(QL.Bandwidth.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().PercentB!, digits: digits), Math.Round(QL.PercentB.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().ZScore!, digits: digits), Math.Round(QL.Zscore.Last().v, digits: digits));
    }
    [Fact] public void CCI() {
        CCI_Series QL = new(bars, period, false);
        var SK = quotes.GetCci(period);
        Assert.Equal(Math.Round((double)SK.Last().Cci!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void CORR() {
        CORR_Series QL = new(bars.High, bars.Low, period, false);
        var SK = quotes.Use(CandlePart.High).GetCorrelation(quotes.Use(CandlePart.Low), period);
        Assert.Equal(Math.Round((double)SK.Last().Correlation!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void COVAR() {
        COVAR_Series QL = new(bars.High, bars.Low, period, false);
        var SK = quotes.Use(CandlePart.High).GetCorrelation(quotes.Use(CandlePart.Low), period);
        Assert.Equal(Math.Round((double)SK.Last().Covariance!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void DEMA() {
        DEMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetDema(period);
        Assert.Equal(Math.Round((double)SK.Last().Dema!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void EMA() {
        EMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetEma(period);
        Assert.Equal(Math.Round((double)SK.Last().Ema!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void HL2() {
        TSeries QL = bars.HL2;
        var SK = quotes.GetBaseQuote(CandlePart.HL2);
        Assert.Equal(Math.Round(SK.Last().Value!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void HLC3() {
        TSeries QL = bars.HLC3;
        var SK = quotes.GetBaseQuote(CandlePart.HLC3);
        Assert.Equal(Math.Round(SK.Last().Value!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void HMA() {
        HMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetHma(period);
        Assert.Equal(Math.Round((double)SK.Last().Hma!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void KAMA() {
        KAMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetKama(period);
        Assert.Equal(Math.Round((double)SK.Last().Kama!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void LINREG() {
        LINREG_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetSlope(period);
        Assert.Equal(Math.Round((double)SK.Last().Slope!, digits: digits), Math.Round(QL.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().Intercept!, digits: digits), Math.Round(QL.Intercept.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().RSquared!, digits: digits), Math.Round(QL.RSquared.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().StdDev!, digits: digits), Math.Round(QL.StdDev.Last().v, digits: digits));
    }
    [Fact] public void MACD() {
        MACD_Series QL = new(bars.Close, 26, 12, 9, useNaN: false);
        var SK = quotes.GetMacd(12, 26, 9);
        Assert.Equal(Math.Round((double)SK.Last().Macd!, digits: digits), Math.Round(QL.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().Signal!, digits: digits), Math.Round(QL.Signal.Last().v, digits: digits));
    }
    [Fact] public void MAD() {
        MAD_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSmaAnalysis(period);
        Assert.Equal(Math.Round((double)SK.Last().Mad!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void MAMA() {
        MAMA_Series QL = new(bars.HL2, fastlimit: 0.5, slowlimit: 0.05);
        var SK = quotes.GetMama(fastLimit: 0.5, slowLimit: 0.05);
        Assert.Equal(Math.Round((double)SK.Last().Mama!, digits: digits), Math.Round(QL.Last().v, digits: digits));
        Assert.Equal(Math.Round((double)SK.Last().Fama!, digits: digits), Math.Round(QL.Fama.Last().v, digits: digits));
    }
    [Fact] public void MAPE() {
        MAPE_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSmaAnalysis(period);
        Assert.Equal(Math.Round((double)SK.Last().Mape!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void MSE() {
        MSE_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSmaAnalysis(period);
        Assert.Equal(Math.Round((double)SK.Last().Mse!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void OBV() {
        OBV_Series QL = new(bars, period, false);
        var SK = quotes.GetObv(period);
        // adding volume[0] to OBV to pass the test and keep compatibility with TA-LIB
        Assert.Equal(Math.Round(SK.Last().Obv! + (double)quotes.First().Volume!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void OC2() {
        TSeries QL = bars.OC2;
        var SK = quotes.GetBaseQuote(CandlePart.OC2);
        Assert.Equal(Math.Round(SK.Last().Value!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void OHL3() {
        TSeries QL = bars.OHL3;
        var SK = quotes.GetBaseQuote(CandlePart.OHL3);
        Assert.Equal(Math.Round(SK.Last().Value!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void OHLC4() {
        TSeries QL = bars.OHLC4;
        var SK = quotes.GetBaseQuote(CandlePart.OHLC4);
        Assert.Equal(Math.Round(SK.Last().Value!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void RSI() {
        RSI_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetRsi(period);
        Assert.Equal(Math.Round((double)SK.Last().Rsi!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void SDEV() {
        SDEV_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetStdDev(period);
        Assert.Equal(Math.Round((double)SK.Last().StdDev!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void SMA() {
        SMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSma(period);
        Assert.Equal(Math.Round((double)SK.Last().Sma!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void SMMA() {
        SMMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetSmma(period);
        Assert.Equal(Math.Round((double)SK.Last().Smma!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void T3() {
        T3_Series QL = new(source: bars.Close, period, vfactor: 0.7, false);
        var SK = quotes.GetT3(lookbackPeriods: period, volumeFactor: 0.7);
        Assert.Equal(Math.Round((double)SK.Last().T3!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void TEMA() {
        TEMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetTema(period);
        Assert.Equal(Math.Round((double)SK.Last().Tema!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void TR() {
        TR_Series QL = new(bars, useNaN: false);
        var SK = quotes.GetTr();
        Assert.Equal(Math.Round((double)SK.Last().Tr!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void WMA() {
        WMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetWma(period);
        Assert.Equal(Math.Round((double)SK.Last().Wma!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void ZSCORE() {
        ZSCORE_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetStdDev(period);
        Assert.Equal(Math.Round((double)SK.Last().ZScore!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }

}
