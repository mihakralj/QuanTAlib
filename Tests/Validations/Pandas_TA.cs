using Xunit;
using System;
using QuanTAlib;
using Python.Runtime;
using Python.Included;

namespace Validations;
public class PandasTA : IDisposable
{
    private GBM_Feed bars;
    private Random rnd = new();
    private int period;
    private string OStype;
    private dynamic np;
    private dynamic ta;
    private dynamic df;

    public PandasTA()
    {
        bars = new(5000);
        period = rnd.Next(28) + 3;

        // Checking the host OS and setting PythonDLL accordingly
        OStype = Environment.OSVersion.ToString();
        if (OStype == "Unix 13.1.0")
            OStype = @"/usr/local/Cellar/python@3.10/3.10.8/Frameworks/Python.framework/Versions/3.10/lib/libpython3.10.dylib";
        else OStype = Path.GetFullPath(".") + @"\python-3.10.0-embed-amd64\python310.dll";

				Installer.InstallPath = Path.GetFullPath(".");
        Installer.SetupPython().Wait();
        Installer.TryInstallPip();
        Installer.PipInstallModule("pandas-ta");

        Runtime.PythonDLL = OStype;
        PythonEngine.Initialize();
        np = Py.Import("numpy");
        ta = Py.Import("pandas_ta");

        string[] cols = { "open", "high", "low", "close", "volume" };
        double[,] ary = new double[bars.Count, 5];
        for (int i = 0; i < bars.Count; i++)
        {
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
    }

    [Fact]
    void SMA()
    {
        SMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.sma(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }

    [Fact]
    void EMA()
    {
        EMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.ema(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }

    [Fact]
    void TEMA()
    {
        TEMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.tema(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }

    [Fact]
    void ENTP()
    {
        ENTP_Series QL = new(bars.Close, period, useNaN: false);
        var pta = df.ta.entropy(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }

    [Fact]
    void WMA()
    {
        WMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.wma(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }

    [Fact]
    void DEMA()
    {
        DEMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.dema(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }

    [Fact]
    void BIAS()
    {
        BIAS_Series QL = new(bars.Close, period, false);
        var pta = df.ta.bias(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }

    [Fact]
    void KURT()
    {
        KURT_Series QL = new(bars.Close, period, useNaN: false);
        var pta = df.ta.kurtosis(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 4), Math.Round(QL.Last().v, 4));
    }

    [Fact]
    void MAD()
    {
        MAD_Series QL = new(bars.Close, period, useNaN: false);
        var pta = df.ta.mad(close: df.close, length: period);
        Assert.Equal(Math.Round((double)pta.tail(1), 7), Math.Round(QL.Last().v, 7));
    }
}