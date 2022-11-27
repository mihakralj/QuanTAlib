using System;
using QuanTAlib;
using Skender.Stock.Indicators;
using Tulip;
using Python.Runtime;
using Python.Included;
using TALib;
using Validations;
using Xunit;

namespace One.by.one;
public class SMA : IDisposable {
	private readonly GBM_Feed bars;
	private readonly Random rnd = new();
	private readonly int period, skip;
	private readonly double precision;

	private readonly IEnumerable<Quote> quotes;
	private readonly double[] outdata;
	private readonly double[] inopen;
	private readonly double[] inhigh;
	private readonly double[] inlow;
	private readonly double[] inclose;
	private readonly double[] involume;

	private readonly string OStype;
	private readonly dynamic np;
	private readonly dynamic ta;
	private readonly dynamic df;

	public void Dispose() {
		PythonEngine.Shutdown();
		GC.SuppressFinalize(this);
	}
	public SMA() {
		bars = new(Bars: 1000, Volatility: 0.5, Drift: 0.0);
		period = rnd.Next(30) + 5;
		precision = 1e-8;
		skip = period-1;

		quotes = bars.Select(q => new Quote {
			Date = q.t,
			Open = (decimal)q.o,
			High = (decimal)q.h,
			Low = (decimal)q.l,
			Close = (decimal)q.c,
			Volume = (decimal)q.v
		});

		outdata = new double[bars.Count];
		inopen = bars.Open.v.ToArray();
		inhigh = bars.High.v.ToArray();
		inlow = bars.Low.v.ToArray();
		inclose = bars.Close.v.ToArray();
		involume = bars.Volume.v.ToArray();

		// Checking the host OS and setting PythonDLL accordingly
		OStype = Environment.OSVersion.ToString();
		if (OStype == "Unix 13.1.0")
			OStype = @"/usr/local/Cellar/python@3.10/3.10.8/Frameworks/Python.framework/Versions/3.10/lib/libpython3.10.dylib";
		else
			OStype = Path.GetFullPath(".") + @"\python-3.10.0-embed-amd64\python310.dll";

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
	[Fact]
	public void WeirdData() {
		SMA_Series QL = new(source: bars.Close, period);
		var lastData = bars.Close.Last();
		var lastCalc = QL.Last();
		QL.Add((DateTime.Today.AddDays(1), double.NaN), update: true);
		Assert.NotEqual(lastCalc, QL.Last()); //value changed
		QL.Add(lastData, update: true);
		Assert.Equal(lastCalc, QL.Last()); // back to the same data

		QL.Add((DateTime.Today.AddDays(-1), double.NegativeInfinity), update: true);
		Assert.NotEqual(lastCalc, QL.Last()); //value changed
		QL.Add(lastData, update: true);
		Assert.Equal(lastCalc, QL.Last()); // back to the same data

		QL.Add((new DateTime(), double.Epsilon), update: true);
		Assert.NotEqual(lastCalc, QL.Last()); //value changed
		QL.Add(lastData, update: true);
		Assert.Equal(lastCalc, QL.Last()); // back to the same data
	}
	[Fact]
	public void Updating() {
		SMA_Series QL = new(source: bars.Close, period);
		var lastData = bars.Close.Last();
		var lastCalc = QL.Last();
		int lastLen = QL.Count;
		QL.Add((DateTime.Today, 0), update: true);
		Assert.NotEqual(lastCalc, QL.Last()); //value changed
		QL.Add(lastData, update: true);
		Assert.Equal(lastLen, QL.Count); // same size
		Assert.Equal(lastCalc, QL.Last()); // same data
	}
	[Fact]
	public void Skender_Test() {
		SMA_Series QL = new(bars.Close, period, false);
		var SK = quotes.GetSma(period).Select(i => i.Sma.Null2NaN()!);
		for (int i = QL.Length; i > skip; i--) {
			double QL_item = QL[i - 1].v;
			double SK_item = SK.ElementAt(i - 1);
			Assert.InRange(SK_item! - QL_item, -precision, precision);
		}
	}
	[Fact]
	public void TALIB_Test() {
		SMA_Series QL = new(bars.Close, period, false);
		Core.Sma(inclose, 0, bars.Count - 1, outdata, out int outBegIdx, out _, period);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TA_item = outdata[i - outBegIdx];
			Assert.InRange(TA_item! - QL_item, -precision, precision);

		}
	}
	[Fact]
	public void Tulip_Test() {
		double[][] arrin = { inclose };
		double[][] arrout = { outdata };
		SMA_Series QL = new(bars.Close, period, false);
		Tulip.Indicators.sma.Run(inputs: arrin, options: new double[] { period }, outputs: arrout);
		for (int i = QL.Length - 1; i > skip; i--) {
			double QL_item = QL[i].v;
			double TU_item = arrout[0][i - period + 1];
			Assert.InRange(TU_item! - QL_item, -precision, precision);

		}
	}
	[Fact]
	void PandasTA_Test() {
		SMA_Series QL = new(bars.Close, period, false);
		var pta = df.ta.sma(close: df.close, length: period);
		for (int i = QL.Length; i > skip; i--) {
			double QL_item = QL[i - 1].v;
			double PanTA_item = (double)pta[i - 1];
			Assert.InRange(PanTA_item! - QL_item, -precision, precision);
		}
	}
}
