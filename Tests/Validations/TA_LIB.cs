using Xunit;
using System;
using TALib;
using QuanTAlib;

namespace Validations;
public class Ta_Lib
{
  private readonly GBM_Feed bars;
  private readonly Random rnd = new();
  private readonly int period, digits;
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
    digits = 6;

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
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void ADL()
  {
    ADL_Series QL = new(bars, false);
    Core.Ad(inhigh, inlow, inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > 0; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void ADOSC()
  {
    ADOSC_Series QL = new(bars, false);
    Core.AdOsc(inhigh, inlow, inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void ATR()
  {
    ATR_Series QL = new(bars, period, false);
    Core.Atr(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx * 15; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void BBANDS()
  {
    double[] outMiddle = new double[bars.Count];
    double[] outUpper = new double[bars.Count];
    double[] outLower = new double[bars.Count];
    BBANDS_Series QL = new(bars.Close, period: 26, multiplier: 2.0, false);
    Core.Bbands(inclose, 0, bars.Count - 1, outRealUpperBand: outUpper, outRealMiddleBand: outMiddle, outRealLowerBand: outLower, out int outBegIdx, out _, optInTimePeriod: 26, optInNbDevUp: 2.0, optInNbDevDn: 2.0);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL.Upper[i].v, digits: digits);
      double TA_item = Math.Round(outUpper[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
      QL_item = Math.Round(QL.Mid[i].v, digits: digits);
      TA_item = Math.Round(outMiddle[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
      QL_item = Math.Round(QL.Lower[i].v, digits: digits);
      TA_item = Math.Round(outLower[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
    Assert.Equal(Math.Round(outUpper[outUpper.Length - outBegIdx - 1], digits: digits), Math.Round(QL.Upper.Last().v, digits: digits));
    Assert.Equal(Math.Round(outMiddle[outMiddle.Length - outBegIdx - 1], digits: digits), Math.Round(QL.Mid.Last().v, digits: digits));
    Assert.Equal(Math.Round(outLower[outLower.Length - outBegIdx - 1], digits: digits), Math.Round(QL.Lower.Last().v, digits: digits));
  }
  [Fact]
  public void CCI()
  {
    CCI_Series QL = new(bars, period, false);
    Core.Cci(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void CORR()
  {
    CORR_Series QL = new(bars.Open, bars.Close, period);
    Core.Correl(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, optInTimePeriod: period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void DEMA()
  {
    DEMA_Series QL = new(bars.Close, period, false);
    Core.Dema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void DIV()
  {
    DIV_Series QL = new(bars.Open, bars.Close);
    Core.Div(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void EMA()
  {
    EMA_Series QL = new(bars.Close, period, false);
    Core.Ema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void HL2()
  {
    TSeries QL = bars.HL2;
    Core.MedPrice(inhigh, inlow, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void HLC3()
  {
    TSeries QL = bars.HLC3;
    Core.TypPrice(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void HLCC4()
  {
    TSeries QL = bars.HLCC4;
    Core.WclPrice(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void MACD()
  {
    double[] macdSignal = new double[bars.Count];
    double[] macdHist = new double[bars.Count];
    MACD_Series QL = new(bars.Close, slow: 26, fast: 12, signal: 9, false);
    Core.Macd(inclose, 0, bars.Count - 1, outMacd: TALIB, outMacdSignal: macdSignal, outMacdHist: macdHist, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx * 10; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
      QL_item = Math.Round(QL.Signal[i].v, digits: digits);
      TA_item = Math.Round(macdSignal[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void MAMA()
  {
    MAMA_Series QL = new(bars.Close, fastlimit: 0.5, slowlimit: 0.05);
    Core.Mama(inReal: inclose, startIdx: 0, endIdx: bars.Count - 1, outMama: TALIB, outFama: TALIB2, outBegIdx: out int outBegIdx, outNbElement: out _, optInFastLimit: 0.5, optInSlowLimit: 0.05);
    for (int i = QL.Length - 1; i > outBegIdx * 15; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void MAX()
  {
    MAX_Series QL = new(bars.Close, period, false);
    Core.Max(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void MIDPOINT()
  {
    MIDPOINT_Series QL = new(bars.Close, period, false);
    Core.MidPoint(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void MIDPRICE()
  {
    MIDPRICE_Series QL = new(bars, period, false);
    Core.MidPrice(inhigh, inlow, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void MIN()
  {
    MIN_Series QL = new(bars.Close, period, false);
    Core.Min(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void MUL()
  {
    MUL_Series QL = new(bars.Open, bars.Close);
    Core.Mult(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void OBV()
  {
    OBV_Series QL = new(bars, period, false);
    Core.Obv(inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void OHLC4()
  {
    TSeries QL = bars.OHLC4;
    Core.AvgPrice(inopen, inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void RSI()
  {
    RSI_Series QL = new(bars.Close, period, false);
    Core.Rsi(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void SDEV()
  {
    SDEV_Series QL = new(bars.Close, period, false);
    Core.StdDev(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void SMA()
  {
    SMA_Series QL = new(bars.Close, period, false);
    Core.Sma(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void SUB()
  {
    SUB_Series QL = new(bars.Open, bars.Close);
    Core.Sub(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void SUM()
  {
    SUM_Series QL = new(bars.Close, period, false);
    Core.Sum(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void T3()
  {
    T3_Series QL = new(source: bars.Close, period: period, vfactor: 0.7, useNaN: false);
    Core.T3(inReal: inclose, startIdx: 0, endIdx: bars.Count - 1, outReal: TALIB, outBegIdx: out int outBegIdx, outNbElement: out _, optInTimePeriod: period, optInVFactor: 0.7);
    for (int i = QL.Length - 1; i > outBegIdx * 15; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void TEMA()
  {
    TEMA_Series QL = new(bars.Close, period, false);
    Core.Tema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx * 15; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void TR()
  {
    TR_Series QL = new(bars, false);
    Core.TRange(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void TRIMA()
  {
    TRIMA_Series QL = new(bars.Close, period, false);
    Core.Trima(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void VAR()
  {
    VAR_Series QL = new(bars.Close, period, false);
    Core.Var(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx * 15; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }
  [Fact]
  public void WMA()
  {
    WMA_Series QL = new(bars.Close, period, false);
    Core.Wma(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);
    for (int i = QL.Length - 1; i > outBegIdx; i--)
    {
      double QL_item = Math.Round(QL[i].v, digits: digits);
      double TA_item = Math.Round(TALIB[i - outBegIdx], digits: digits);
      Assert.Equal(TA_item!, QL_item);
    }
  }

}
