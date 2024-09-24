using Xunit;
using System;
using TALib;
using QuanTAlib;

namespace Validations;
public class Ta_Lib
{
    private readonly GBM_Feed bars;
    private readonly Random rnd = new();
    private readonly int period, digits, skip;
    private readonly double[] TALIB;
    private readonly double[] TALIB2;
    private readonly double[] inopen;
    private readonly double[] inhigh;
    private readonly double[] inlow;
    private readonly double[] inclose;
    private readonly double[] involume;

    public Ta_Lib()
    {
        bars = new(Bars: 5000, Volatility: 0.8, Drift: 0.0, Precision: 3);
        period = rnd.Next(28) + 3;
        skip = period + 2;
        digits = 9;

        TALIB = new double[bars.Count];
        TALIB2 = new double[bars.Count];
        inopen = bars.Open.v.ToArray();
        inhigh = bars.High.v.ToArray();
        inlow = bars.Low.v.ToArray();
        inclose = bars.Close.v.ToArray();
        involume = bars.Volume.v.ToArray();
    }

    [Fact]
    public void ADD()
    {
        ADD_Series QL = new(bars.Open, bars.Close);
        Core.Add(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void ADL()
    {
        ADL_Series QL = new(bars);
        Core.Ad(inhigh, inlow, inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > 0; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void ADOSC()
    {
        ADOSC_Series QL = new(bars, 3, 10, false);
        Core.AdOsc(inhigh, inlow, inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip * 2; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void ATR()
    {
        ATR_Series QL = new(bars, period: period, useNaN: false);
        Core.Atr(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    public void BBANDS()
    {
        double[] outMiddle = new double[bars.Count];
        double[] outUpper = new double[bars.Count];
        double[] outLower = new double[bars.Count];
        BBANDS_Series QL = new(bars.Close, period: period, multiplier: 2.0, false);
        Core.Bbands(inclose, 0, bars.Count - 1, outRealUpperBand: outUpper, outRealMiddleBand: outMiddle, outRealLowerBand: outLower, out int outBegIdx, out _, optInTimePeriod: period, optInNbDevUp: 2.0, optInNbDevDn: 2.0);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL.Upper[i].v;
            double TA_item = outUpper[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), high: Math.Exp(-digits));
            QL_item = QL.Mid[i].v;
            TA_item = outMiddle[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), high: Math.Exp(-digits));
            QL_item = QL.Lower[i].v;
            TA_item = outLower[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), high: Math.Exp(-digits));
        }
    }
    [Fact]
    public void CCI()
    {
        CCI_Series QL = new(bars, period, false);
        Core.Cci(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    /* CMO in TA-LIB is not valid 
       [Fact]
       public void CMO() {
           CMO_Series QL = new(bars.Close, period, false);
           Core.Cmo(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
           for (int i = QL.Length - 1; i > skip; i--) {
               double QL_item = QL[i].v;
               double TA_item = TALIB[i - outBegIdx];
               Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
           }
       }
    */
    [Fact]
    public void CORR()
    {
        CORR_Series QL = new(bars.Open, bars.Close, period);
        Core.Correl(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, optInTimePeriod: period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void DEMA()
    {
        DEMA_Series QL = new(bars.Close, period, false, useSMA: false);
        Core.Dema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > period * 10; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void DIV()
    {
        DIV_Series QL = new(bars.Open, bars.Close);
        Core.Div(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void EMA()
    {
        EMA_Series QL = new(bars.Close, period, false);
        Core.Ema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void HL2()
    {
        TSeries QL = bars.HL2;
        Core.MedPrice(inhigh, inlow, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void HLC3()
    {
        TSeries QL = bars.HLC3;
        Core.TypPrice(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void HLCC4()
    {
        TSeries QL = bars.HLCC4;
        Core.WclPrice(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void KAMA()
    {
        KAMA_Series QL = new(bars.Close, period, fast: 2, slow: 30);
        Core.Kama(inReal: inclose, startIdx: 0, endIdx: bars.Count - 1, outReal: TALIB, outBegIdx: out int outBegIdx, outNbElement: out _, optInTimePeriod: period);
        for (int i = QL.Length - 1; i > skip * 15; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void MACD()
    {
        double[] macdSignal = new double[bars.Count];
        double[] macdHist = new double[bars.Count];
        MACD_Series QL = new(bars.Close, slow: 26, fast: 12, signal: 9, false);
        // TA-LIB runs EMA without SMA, leaving first 100 values for convergence
        Core.Macd(inclose, 0, bars.Count - 1, outMacd: TALIB, outMacdSignal: macdSignal, outMacdHist: macdHist, out int outBegIdx, out _, optInFastPeriod: 12, optInSlowPeriod: 26, optInSignalPeriod: 9);
        for (int i = QL.Length - 1; i > 100; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
            QL_item = QL.Signal[i].v;
            TA_item = macdSignal[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    /*
    [Fact]
    public void MAMA()
    {
      MAMA_Series QL = new(bars.Close, fastlimit: 0.5, slowlimit: 0.05);
      Core.Mama(inReal: inclose, startIdx: 0, endIdx: bars.Count - 1, outMama: TALIB, outFama: TALIB2, outBegIdx: out int outBegIdx, outNbElement: out _, optInFastLimit: 0.5, optInSlowLimit: 0.05);
      for (int i = QL.Length - 1; i > skip * 10; i--)
      {
        double QL_item = QL[i].v;
        double TA_item = TALIB[i - outBegIdx];
        Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits-1), Math.Exp(-digits-1));
      }
    }
    */
    [Fact]
    public void MAX()
    {
        MAX_Series QL = new(bars.Close, period, false);
        Core.Max(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void MIDPOINT()
    {
        MIDPOINT_Series QL = new(bars.Close, period, false);
        Core.MidPoint(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void MIDPRICE()
    {
        MIDPRICE_Series QL = new(bars, period, false);
        Core.MidPrice(inhigh, inlow, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void MIN()
    {
        MIN_Series QL = new(bars.Close, period, false);
        Core.Min(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void MUL()
    {
        MUL_Series QL = new(bars.Open, bars.Close);
        Core.Mult(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void OBV()
    {
        OBV_Series QL = new(bars, period, false);
        Core.Obv(inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void OHLC4()
    {
        TSeries QL = bars.OHLC4;
        Core.AvgPrice(inopen, inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void RSI()
    {
        RSI_Series QL = new(bars.Close, period, false);
        Core.Rsi(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void SDEV()
    {
        SDEV_Series QL = new(bars.Close, period, false);
        Core.StdDev(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void SMA()
    {
        SMA_Series QL = new(bars.Close, period, false);
        Core.Sma(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void SUB()
    {
        SUB_Series QL = new(bars.Open, bars.Close);
        Core.Sub(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void SUM()
    {
        CUSUM_Series QL = new(bars.Close, period, false);
        Core.Sum(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void T3()
    {
        T3_Series QL = new(source: bars.Close, period: period, vfactor: 0.7, useNaN: false);
        Core.T3(inReal: inclose, startIdx: 0, endIdx: bars.Count - 1, outReal: TALIB, outBegIdx: out int outBegIdx, outNbElement: out _, optInTimePeriod: period, optInVFactor: 0.7);
        for (int i = QL.Length - 1; i > period * 10; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void TEMA()
    {
        TEMA_Series QL = new(bars.Close, period, false);
        Core.Tema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip * 15; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void TR()
    {
        TR_Series QL = new(bars);
        Core.TRange(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void TRIMA()
    {
        TRIMA_Series QL = new(bars.Close, period, false);
        Core.Trima(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void TRIX()
    {
        TRIX_Series QL = new(bars.Close, period, useNaN: false, useSMA: true);
        Core.Trix(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > period * 10; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void VAR()
    {
        VAR_Series QL = new(bars.Close, period, false);
        Core.Var(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip * 15; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact]
    public void WMA()
    {
        WMA_Series QL = new(bars.Close, period, false);
        Core.Wma(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
        for (int i = QL.Length - 1; i > skip; i--)
        {
            double QL_item = QL[i].v;
            double TA_item = TALIB[i - outBegIdx];
            Assert.InRange(TA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

}
