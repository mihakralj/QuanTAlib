using Xunit;
using System;
using TALib;
using QuanTAlib;

namespace Validations;
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
        bars = new(5000);
        period = rnd.Next(28) + 3;
        TALIB = new double[bars.Count];
        inopen = bars.Open.v.ToArray();
        inhigh = bars.High.v.ToArray();
        inlow = bars.Low.v.ToArray();
        inclose = bars.Close.v.ToArray();
        involume = bars.Volume.v.ToArray();
    }

    /////////////////////////////////////////

    [Fact]
    public void ADD()
    {
        ADD_Series QL = new(bars.Open, bars.Close);
        Core.Add(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void SUB()
    {
        SUB_Series QL = new(bars.Open, bars.Close);
        Core.Sub(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void MUL()
    {
        MUL_Series QL = new(bars.Open, bars.Close);
        Core.Mult(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void DIV()
    {
        DIV_Series QL = new(bars.Open, bars.Close);
        Core.Div(inopen, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void SDEV()
    {
        SDEV_Series QL = new(bars.Close, period, false);
        Core.StdDev(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void SMA()
    {
        SMA_Series QL = new(bars.Close, period, false);
        Core.Sma(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void SUM()
    {
	    SUM_Series QL = new(bars.Close, period, false);
	    Core.Sum(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

	    Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void MIDPRICE()
    {
	    MIDPRICE_Series QL = new(bars, period, false);
	    Core.MidPrice(inhigh, inlow, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

	    Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }


	[Fact]
	public void VAR()
	{
		VAR_Series QL = new(bars.Close, period, false);
		Core.Var(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

		Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 5, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 5));
	}

	[Fact]
    public void MIDPOINT()
    {
	    MIDPOINT_Series QL = new(bars.Close, period, false);
	    Core.MidPoint(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

	    Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

	[Fact]
    public void TRIMA()
    {
        TRIMA_Series QL = new(bars.Close, period, false);
        Core.Trima(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void EMA()
    {
        EMA_Series QL = new(bars.Close, period, false);
        Core.Ema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void WMA()
    {
        WMA_Series QL = new(bars.Close, period, false);
        Core.Wma(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void DEMA()
    {
        DEMA_Series QL = new(bars.Close, period, false);
        Core.Dema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void TEMA()
    {
        TEMA_Series QL = new(bars.Close, period, false);
        Core.Tema(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void MAX()
    {
        MAX_Series QL = new(bars.Close, period, false);
        Core.Max(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void MIN()
    {
        MIN_Series QL = new(bars.Close, period, false);
        Core.Min(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void ADL()
    {
        ADL_Series QL = new(bars, false);
        Core.Ad(inhigh, inlow, inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void OBV()
    {
        OBV_Series QL = new(bars, period, false);
        Core.Obv(inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void ADOSC()
    {
        ADOSC_Series QL = new(bars, false);
        Core.AdOsc(inhigh, inlow, inclose, involume, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void ATR()
    {
        ATR_Series QL = new(bars, period, false);
        Core.Atr(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void CCI()
    {
        CCI_Series QL = new(bars, period, false);
        Core.Cci(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void RSI()
    {
        RSI_Series QL = new(bars.Close, period, false);
        Core.Rsi(inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _, period);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void TR()
    {
        TR_Series QL = new(bars, false);
        Core.TRange(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void MACD()
    {
        double[] macdSignal = new double[bars.Count];
        double[] macdHist = new double[bars.Count];
        MACD_Series QL = new(bars.Close, slow: 26, fast: 12, signal: 9, false);
        Core.Macd(inclose, 0, bars.Count - 1, outMacd: TALIB, outMacdSignal: macdSignal, outMacdHist: macdHist, out int outBegIdx, out _);
        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(macdSignal[macdSignal.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Signal.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void BBANDS()
    {
        double[] outMiddle = new double[bars.Count];
        double[] outUpper = new double[bars.Count];
        double[] outLower = new double[bars.Count];
        BBANDS_Series QL = new(bars.Close, period: 26, multiplier: 2.0, false);
        Core.Bbands(inclose, 0, bars.Count - 1, outRealUpperBand: outUpper, outRealMiddleBand: outMiddle, outRealLowerBand: outLower, out int outBegIdx, out _, optInTimePeriod: 26, optInNbDevUp: 2.0, optInNbDevDn: 2.0);
        Assert.Equal(Math.Round(outUpper[outUpper.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Upper.Last().v, 6, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(outMiddle[outMiddle.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Mid.Last().v, 6, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(outLower[outLower.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Lower.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void HL2()
    {
        TSeries QL = bars.HL2;
        Core.MedPrice(inhigh, inlow, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void HLC3()
    {
        TSeries QL = bars.HLC3;
        Core.TypPrice(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void OHLC4()
    {
        TSeries QL = bars.OHLC4;
        Core.AvgPrice(inopen, inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void HLCC4()
    {
        TSeries QL = bars.HLCC4;
        Core.WclPrice(inhigh, inlow, inclose, 0, bars.Count - 1, TALIB, out int outBegIdx, out _);

        Assert.Equal(Math.Round(TALIB[TALIB.Length - outBegIdx - 1], 6, MidpointRounding.AwayFromZero), Math.Round(QL.Last().v, 6, MidpointRounding.AwayFromZero));
    }
}
