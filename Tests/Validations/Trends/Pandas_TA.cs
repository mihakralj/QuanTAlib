using Xunit;
using System;
using QuanTAlib;
using Python.Runtime;
using Python.Included;

namespace Validations;
public class PandasTA : IDisposable
{
	private readonly GBM_Feed bars;
    private readonly Random rnd = new();
	private readonly int period, sample;
	private int digits;
	private readonly string OStype;
	private readonly dynamic np;
	private readonly dynamic ta;
	private readonly dynamic df;

	public PandasTA() {
        bars = new(Bars: 5000, Volatility: 0.8, Drift: 0.0);
        period = rnd.Next(maxValue: 28) + 3;
        sample = 200;
	    digits = 10;

		// Checking the host OS and setting PythonDLL accordingly
        OStype = Environment.OSVersion.ToString();
        if (OStype == "Unix 13.1.0")
            OStype = @"/usr/local/Cellar/python@3.10/3.10.8/Frameworks/Python.framework/Versions/3.10/lib/libpython3.10.dylib";
        else OStype = Path.GetFullPath(".") + @"\python-3.10.0-embed-amd64\python310.dll";

		Installer.InstallPath = Path.GetFullPath(path: ".");
		Installer.SetupPython().Wait();
		Installer.TryInstallPip();
		Installer.PipInstallModule(module_name: "pandas-ta");
		Runtime.PythonDLL = OStype;
	    PythonEngine.Initialize();
		np = Py.Import(name: "numpy");
		ta = Py.Import(name: "pandas_ta");

		string[] cols = { "open", "high", "low", "close", "volume" };
		double[,] ary = new double[bars.Count, 5];
		for (int i = 0; i < bars.Count; i++) {
			ary[i, 0] = bars.Open[i].v;
			ary[i, 1] = bars.High[i].v;
			ary[i, 2] = bars.Low[i].v;
			ary[i, 3] = bars.Close[i].v;
			ary[i, 4] = bars.Volume[i].v;
		}
		df = ta.DataFrame(data: np.array(ary), index: np.array(bars.Close.t), columns: np.array(cols));
	}
		public void Dispose()
	{
    PythonEngine.Shutdown();
	GC.SuppressFinalize(this);
	}

	[Fact] void ADL() {
		ADL_Series QL = new(bars);
		var pta = df.ta.ad(high: df.high, low: df.low, close:df.close, volume:df.volume);
		for (int i = QL.Length; i > QL.Length-sample; i--)
		{
            double QL_item = Math.Round(QL[i-1].v, digits: digits);
			double PanTA_item = Math.Round((double)pta[i-1], digits: digits);
			Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));

		}
	}
	[Fact] void ADOSC() {
		ADOSC_Series QL = new(bars);
		var pta = df.ta.adosc(high: df.high, low: df.low, close: df.close, volume: df.volume);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact] void ATR() {
		ATR_Series QL = new(bars, period);
		var pta = df.ta.atr(high: df.high, low: df.low, close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void BIAS() {
		BIAS_Series QL = new(bars.Close, period, false);
		var pta = df.ta.bias(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void DEMA() {
		DEMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.dema(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void EMA() {
		EMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.ema(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void ENTROPY() {
		ENTROPY_Series QL = new(bars.Close, period, useNaN: false);
		var pta = df.ta.entropy(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void HL2() {
		var pta = df.ta.hl2(high: df.high, low: df.low);
        for (int i = bars.HL2.Length; i > bars.HL2.Length-sample; i--)
        {
            double QL_item = Math.Round(bars.HL2[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
	}
	[Fact] void HLC3() {
		var pta = df.ta.hlc3(high: df.high, low: df.low, close: df.close);
        for (int i = bars.HLC3.Length; i > bars.HLC3.Length-sample; i--)
        {
            double QL_item = Math.Round(bars.HLC3[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact] void HMA() {
		HMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.hma(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }

	}
    [Fact] void KAMA() {
        KAMA_Series QL = new(bars.Close, period);
        var pta = df.ta.kama(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void KURTOSIS() {
		KURTOSIS_Series QL = new(bars.Close, period, useNaN: false);
		var pta = df.ta.kurtosis(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void MAD()
	{
		MAD_Series QL = new(bars.Close, period, useNaN: false);
		var pta = df.ta.mad(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void MEDIAN() {
		MEDIAN_Series QL = new(bars.Close, period);
		var pta = df.ta.median(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact] void OBV() {
        OBV_Series QL = new(bars);
        var pta = df.ta.obv(close: df.close, volume: df.volume);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void OHLC4() {
		var pta = df.ta.ohlc4(open: df.open, high: df.high, low: df.low, close: df.close);
        for (int i = bars.OHLC4.Length; i > bars.OHLC4.Length-sample; i--)
        {
            double QL_item = Math.Round(bars.OHLC4[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
	}
	[Fact] void RMA() {
		RMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.rma(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void RSI() {
		RSI_Series QL = new(bars.Close, period);
		var pta = df.ta.rsi(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void SDEV()	{
		SDEV_Series QL = new(bars.Close, period, useNaN: false);
		var pta = df.ta.stdev(close: df.close, length: period, ddof: 0);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void SMA() {
		SMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.sma(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void SSDEV() {
		SSDEV_Series QL = new(bars.Close, period, useNaN: false);
		var pta = df.ta.stdev(close: df.close, length: period, ddof: 1);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	/*
	[Fact] void SVARIANCE() {
		SVAR_Series QL = new(bars.Close, period);
		var pta = df.ta.variance(close: df.close, length: period, ddof: 1);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
*/
    [Fact] void T3() {
        T3_Series QL = new(source: bars.Close, period: period, vfactor: 0.7, useNaN: false);
        var pta = df.ta.t3(close: df.close, length: period, a: 0.7);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
    [Fact] void TEMA() {
		TEMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.tema(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void TR() {
		TR_Series QL = new(bars);
		var pta = df.ta.true_range(high: df.high, low: df.low, close: df.close);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void TRIMA() {
    // TODO: return length to variable length (period) when Pandas-TA fixes trima to calculate even periods right
		TRIMA_Series QL = new(bars.Close, 11);
		var pta = df.ta.trima(close: df.close, length: 11);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void VARIANCE()	{
		VAR_Series QL = new(bars.Close, period);
		var pta = df.ta.variance(close: df.close, length: period, ddof:0);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void WMA() {
		WMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.wma(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void ZLEMA() {
		ZLEMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.zlma(close: df.close, length: period);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
	[Fact] void ZSCORE() {
		ZSCORE_Series QL = new(bars.Close, period, useNaN: false);
		var pta = df.ta.zscore(close: df.close, length: period, ddof: 0);
        for (int i = QL.Length; i > QL.Length-sample; i--)
        {
            double QL_item = Math.Round(QL[i - 1].v, digits: digits);
            double PanTA_item = Math.Round((double)pta[i - 1], digits: digits);
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

}