using Xunit;
using System;
using QuanTAlib;
using Skender.Stock.Indicators;

namespace Series;
public class Update { 
	private readonly GBM_Feed bars;
	private readonly Random rnd = new();
	private readonly int period;

    public Update() {
        bars = new(Bars: 5000, Volatility: 0.8, Drift: 0.0);
        period = rnd.Next(28) + 3;
    }

    [Fact] public void ADL() {
		ADL_Series QL = new(bars);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
		int lastLen = QL.Count;
		QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
		QL.Add(lastData, update: true);
		Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void ADOSC() {
        ADOSC_Series QL = new(bars);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void ALMA() {
        ALMA_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void ATR() {
		ATR_Series QL = new(bars, period: period);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
		int lastLen = QL.Count;
		QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
		QL.Add(lastData, update: true);
		Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void ATRP() {
        ATRP_Series QL = new(bars, period: period);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void BBANDS() {
        BBANDS_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void BIAS() {
        BIAS_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void CCI() {
        CCI_Series QL = new(bars, period: period);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void CORR() {
        CORR_Series QL = new(d1: bars.High, d2: bars.Low, period: period);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), (DateTime.Today, 0), update: true);
        QL.Add((lastData.t, lastData.h), (lastData.t, lastData.l), update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void COVAR() {
        COVAR_Series QL = new(d1: bars.High, d2: bars.Low, period);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), (DateTime.Today, 0), update: true);
        QL.Add((lastData.t, lastData.h), (lastData.t, lastData.l), update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void DEMA() {
        DEMA_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
	[Fact]
	public void DWMA() {
		DWMA_Series QL = new(source: bars.Close, period);
		var lastData = bars.Close.Last();
		var lastCalc = QL.Last();
		int lastLen = QL.Count;
		QL.Add((DateTime.Today, 0), update: true);
		QL.Add(lastData, update: true);
		Assert.Equal(lastLen, QL.Count); // same size
		Assert.Equal(lastCalc, QL.Last()); // same data
	}
	[Fact] public void ENTROPY() {
        ENTROPY_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void EMA() {
        EMA_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void HEMA() {
        HEMA_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void HMA() {
        HMA_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void JMA() {
        JMA_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void KAMA() {
        KAMA_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void KURTOSIS() {
        KURTOSIS_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void LINREG() {
        LINREG_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MACD() {
        MACD_Series QL = new(source: bars.Close);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        var lastC1 = QL.Signal.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
        Assert.Equal(lastC1, QL.Signal.Last()); // same data
    }
    [Fact] public void MAD() {
        MAD_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MAMA() {
        MAMA_Series QL = new(source: bars.Close);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        var lastC1 = QL.Fama.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
        Assert.Equal(lastC1, QL.Fama.Last()); // same data
    }
    [Fact] public void MAPE() {
        MAPE_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MAX() {
        MAX_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MEDIAN() {
        MEDIAN_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MIDPOINT() {
        MIDPOINT_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MIDPRICE() {
        MIDPRICE_Series QL = new(bars, period: period);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MIN() {
        MAX_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void MSE() {
        MSE_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void OBV() {
        OBV_Series QL = new(bars, period: period);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void RSI() {
        RSI_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void RMA() {
        RMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void SDEV() {
        SDEV_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void SMA() {
        SMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void SMAPE() {
        SMAPE_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void SMMA() {
        SMMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void SSDEV() {
        SSDEV_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void SUM() {
        SUM_Series QL = new(source: bars.Close, period: period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void SVAR() {
        SVAR_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void T3() {
        SMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void TEMA() {
        TEMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void TR() {
        TR_Series QL = new(bars);
        var lastData = bars.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0, 0, 0, 0, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void TRIMA() {
        TRIMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void VAR() {
        VAR_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void WMA() {
        WMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void WMAPE() {
        WMAPE_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void ZLEMA() {
        ZLEMA_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
    [Fact] public void ZSCORE() {
        ZSCORE_Series QL = new(source: bars.Close, period);
        var lastData = bars.Close.Last();
        var lastCalc = QL.Last();
        int lastLen = QL.Count;
        QL.Add((DateTime.Today, 0), update: true);
        QL.Add(lastData, update: true);
        Assert.Equal(lastLen, QL.Count); // same size
        Assert.Equal(lastCalc, QL.Last()); // same data
    }
}
