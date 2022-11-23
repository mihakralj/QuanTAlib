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
        bars = new(Bars: 10000, Volatility: 0.5, Drift: 0.0, Precision: 2);
        period = rnd.Next(30) + 5;
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
        // TODO: check precision of ADL()
        ADL_Series QL = new(bars, false);
        var SK = quotes.GetAdl().Select(i => i.Adl);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1)!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void ALMA() {
        ALMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetAlma(period).Select(i => i.Alma.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void ATR() {
        ATR_Series QL = new(bars, period, false);
        var SK = quotes.GetAtr(period).Select(i => i.Atr.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void ATRP() {
        ATRP_Series QL = new(bars, period, false);
        var SK = quotes.GetAtr(period).Select(i => i.Atrp.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void BBANDS() {
        BBANDS_Series QL = new(bars.Close, period, 2.0, useNaN: false);
        var SK = quotes.GetBollingerBands(period, 2.0);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL.Mid[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1).Sma!.Value, digits: digits);
            Assert.Equal(SK_item!, QL_item);
             QL_item = Math.Round(QL.Upper[i - 1].v, digits: digits);
             SK_item = Math.Round((double)SK.ElementAt(i - 1).UpperBand!.Value, digits: digits);
            Assert.Equal(SK_item!, QL_item);
             QL_item = Math.Round(QL.Lower[i - 1].v, digits: digits);
             SK_item = Math.Round((double)SK.ElementAt(i - 1).LowerBand!.Value, digits: digits);
            Assert.Equal(SK_item!, QL_item);
             QL_item = Math.Round(QL.Bandwidth[i - 1].v, digits: digits);
             SK_item = Math.Round((double)SK.ElementAt(i - 1).Width!.Value, digits: digits);
            Assert.Equal(SK_item!, QL_item);
             QL_item = Math.Round(QL.PercentB[i - 1].v, digits: digits);
             SK_item = Math.Round((double)SK.ElementAt(i - 1).PercentB!.Value, digits: digits);
            Assert.Equal(SK_item!, QL_item);
             QL_item = Math.Round(QL.Zscore[i - 1].v, digits: digits);
             SK_item = Math.Round((double)SK.ElementAt(i - 1).ZScore!.Value, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void CCI() {
        CCI_Series QL = new(bars, period, false);
        var SK = quotes.GetCci(period).Select(i => i.Cci.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void CORR() {
        CORR_Series QL = new(bars.High, bars.Low, period, false);
        var SK = quotes.Use(CandlePart.High).GetCorrelation(quotes.Use(CandlePart.Low), period).Select(i => i.Correlation.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void COVAR() {
        COVAR_Series QL = new(bars.High, bars.Low, period, false);
        var SK = quotes.Use(CandlePart.High).GetCorrelation(quotes.Use(CandlePart.Low), period).Select(i => i.Covariance.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void DEMA() {
        DEMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetDema(period).Select(i => i.Dema.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void EMA() {
        EMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetEma(period).Select(i => i.Ema.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void HL2() {
        TSeries QL = bars.HL2;
        var SK = quotes.GetBaseQuote(CandlePart.HL2).Select(i => i.Value);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1)!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void HLC3() {
        TSeries QL = bars.HLC3;
        var SK = quotes.GetBaseQuote(CandlePart.HLC3).Select(i => i.Value);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1)!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void HMA() {
        HMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetHma(period).Select(i => i.Hma.Null2NaN()!);
        for (int i = QL.Length; i > period+3; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void KAMA() {
        // TODO: check precision of KAMA()
        KAMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetKama(period).Select(i => i.Kama.Null2NaN()!);
        for (int i = QL.Length; i > 600; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void LINREG() {
        LINREG_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetSlope(period);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1).Slope!, digits: digits) ;
            Assert.Equal(SK_item!, QL_item);
             QL_item = Math.Round(QL.Intercept[i - 1].v, digits: digits);
             SK_item = Math.Round((double)SK.ElementAt(i - 1).Intercept!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
            QL_item = Math.Round(QL.RSquared[i - 1].v, digits: digits);
            SK_item = Math.Round((double)SK.ElementAt(i - 1).RSquared!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
            QL_item = Math.Round(QL.StdDev[i - 1].v, digits: digits);
            SK_item = Math.Round((double)SK.ElementAt(i - 1).StdDev!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void MACD() {
        MACD_Series QL = new(bars.Close, 26, 12, 9, useNaN: false);
        var SK = quotes.GetMacd(12, 26, 9);
        for (int i = QL.Length; i > 500; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1).Macd.Null2NaN()!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
            QL_item = Math.Round(QL.Signal[i - 1].v, digits: digits);
            SK_item = Math.Round(SK.ElementAt(i - 1).Signal.Null2NaN()!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void MAD() {
        MAD_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSmaAnalysis(period).Select(i => i.Mad.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void MAMA() {
        MAMA_Series QL = new(bars.HL2, fastlimit: 0.5, slowlimit: 0.05);
        var SK = quotes.GetMama(fastLimit: 0.5, slowLimit: 0.05);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1).Mama.Null2NaN()!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
             QL_item = Math.Round(QL.Fama[i - 1].v, digits: digits);
             SK_item = Math.Round(SK.ElementAt(i - 1).Fama.Null2NaN()!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void MAPE() {
        MAPE_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSmaAnalysis(period).Select(i => i.Mape.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void MSE() {
        MSE_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSmaAnalysis(period).Select(i => i.Mse.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void OBV() {
        OBV_Series QL = new(bars, period, false);
        var SK = quotes.GetObv(period).Select(i => i.Obv!);
        // adding volume[0] to OBV to pass the test and keep compatibility with TA-LIB
        Assert.Equal(Math.Round(SK.Last()! + (double)quotes.First().Volume!, digits: digits), Math.Round(QL.Last().v, digits: digits));
    }
    [Fact] public void OC2() {
        TSeries QL = bars.OC2;
        var SK = quotes.GetBaseQuote(CandlePart.OC2).Select(i => i.Value);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1)!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void OHL3() {
        TSeries QL = bars.OHL3;
        var SK = quotes.GetBaseQuote(CandlePart.OHL3).Select(i => i.Value);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1)!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void OHLC4() {
        TSeries QL = bars.OHLC4;
        var SK = quotes.GetBaseQuote(CandlePart.OHLC4).Select(i => i.Value);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round((double)SK.ElementAt(i - 1)!, digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void RSI() {
        RSI_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetRsi(period).Select(i => i.Rsi.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void SDEV() {
        SDEV_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetStdDev(period).Select(i => i.StdDev.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void SMA() {
        SMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetSma(period).Select(i => i.Sma.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void SMMA() {
        SMMA_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetSmma(period).Select(i => i.Smma.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void T3() {
        T3_Series QL = new(source: bars.Close, period: period, vfactor: 0.7, false);
        var SK = quotes.GetT3(lookbackPeriods: period, volumeFactor: 0.7).Select(i => i.T3.Null2NaN()!);
        for (int i = QL.Length; i > period*15 ; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void TEMA() {
        TEMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetTema(period).Select(i => i.Tema.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void TR() {
        TR_Series QL = new(bars, useNaN: false);
        var SK = quotes.GetTr().Select(i => i.Tr.Null2NaN()!);
        for (int i = QL.Length; i > 1; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void WMA() {
        WMA_Series QL = new(bars.Close, period, false);
        var SK = quotes.GetWma(period).Select(i => i.Wma.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }
    [Fact] public void ZSCORE() {
        ZSCORE_Series QL = new(bars.Close, period, useNaN: false);
        var SK = quotes.GetStdDev(period).Select(i => i.ZScore.Null2NaN()!);
        for (int i = QL.Length; i > period; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double SK_item = Math.Round(SK.ElementAt(i - 1), digits: digits);
            Assert.Equal(SK_item!, QL_item);
        }
    }

}
