using Xunit;
using System;
using QuanTAlib;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Python.Runtime;

namespace Validations;

public class PandasTA : IDisposable
{
    private bool disposed = false;
    private readonly GBM_Feed bars;
    private readonly Random rnd = new();
    private readonly int period, skip;
    private readonly int digits;
    private readonly dynamic np;
    private readonly dynamic ta;
    private readonly dynamic pd;
    private readonly dynamic df;

    public PandasTA()
    {
        bars = new GBM_Feed(5000, 0.8, 0.0);
        period = rnd.Next(28) + 3;
        skip = period + 50;
        digits = 8;

        var pythonDLL = PythonLibrary.Locate();
        Runtime.PythonDLL = pythonDLL;
        PythonEngine.Initialize();

        np = Py.Import("numpy");
        pd = Py.Import("pandas");
        ta = Py.Import("pandas_ta");

        string[] cols = { "open", "high", "low", "close", "volume" };
        var ary = new double[bars.Count, 5];
        for (var i = 0; i < bars.Count; i++)
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
        Dispose(true);
        PythonEngine.Shutdown();
        GC.SuppressFinalize(this);
    }

    ~PandasTA()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            disposed = true;
        }
    }

    [Fact]
    private void ADL()
    {
        ADL_Series QL = new(bars);
        var pta = df.ta.ad(high: df.high, low: df.low, close: df.close, volume: df.volume);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void BBANDS()
    {
        BBANDS_Series QL = new(bars.Close, period);
        var pta = df.ta.bbands(close: df.close, length: period).to_numpy();
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL.Lower[i].v;
            var PanTA_item = (double)pta[i][0]; //lower
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
            QL_item = QL.Mid[i].v;
            PanTA_item = (double)pta[i][1]; //mid
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
            QL_item = QL.Upper[i].v;
            PanTA_item = (double)pta[i][2]; //upper
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void BIAS()
    {
        BIAS_Series QL = new(bars.Close, period, false);
        var pta = df.ta.bias(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void CCI()
    {
        CCI_Series QL = new(bars, period, false);
        var pta = df.ta.cci(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void DEMA()
    {
        DEMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.dema(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void EMA()
    {
        EMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.ema(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void ENTROPY()
    {
        ENTROPY_Series QL = new(bars.Close, period, false);
        var pta = df.ta.entropy(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void HL2()
    {
        var pta = df.ta.hl2(high: df.high, low: df.low);
        for (var i = bars.HL2.Length - 1; i > skip; i--)
        {
            var QL_item = bars.HL2[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void HLC3()
    {
        var pta = df.ta.hlc3(high: df.high, low: df.low, close: df.close);
        for (var i = bars.HLC3.Length; i > skip; i--)
        {
            var QL_item = bars.HLC3[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void HMA()
    {
        HMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.hma(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void KURTOSIS()
    {
        KURTOSIS_Series QL = new(bars.Close, period, false);
        var pta = df.ta.kurtosis(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void MACD()
    {
        MACD_Series QL = new(bars.Close, 26, 12, 9, false);
        var pta = df.ta.macd(close: df.close).to_numpy();
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1][0];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
            QL_item = QL.Signal[i - 1].v;
            PanTA_item = (double)pta[i - 1][2];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void MAD()
    {
        MAD_Series QL = new(bars.Close, period, false);
        var pta = df.ta.mad(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void MEDIAN()
    {
        MEDIAN_Series QL = new(bars.Close, period);
        var pta = df.ta.median(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void OBV()
    {
        OBV_Series QL = new(bars);
        var pta = df.ta.obv(close: df.close, volume: df.volume);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void OHLC4()
    {
        var pta = df.ta.ohlc4(open: df.open, high: df.high, low: df.low, close: df.close);
        for (var i = bars.OHLC4.Length; i > skip; i--)
        {
            var QL_item = bars.OHLC4[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void SDEV()
    {
        SDEV_Series QL = new(bars.Close, period, false);
        var pta = df.ta.stdev(close: df.close, length: period, ddof: 0);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void SMA()
    {
        SMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.sma(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void SSDEV()
    {
        SSDEV_Series QL = new(bars.Close, period, false);
        var pta = df.ta.stdev(close: df.close, length: period, ddof: 1);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void SVARIANCE()
    {
        SVAR_Series QL = new(bars.Close, period);
        var pta = df.ta.variance(close: df.close, length: period, ddof: 1);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void TEMA()
    {
        TEMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.tema(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void TR()
    {
        TR_Series QL = new(bars);
        var pta = df.ta.true_range(high: df.high, low: df.low, close: df.close);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void TRIMA()
    {
        // TODO: return length to variable length (period) when Pandas-TA fixes trima to calculate even periods right
        TRIMA_Series QL = new(bars.Close, 11);
        var pta = df.ta.trima(close: df.close, length: 11);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void VARIANCE()
    {
        VAR_Series QL = new(bars.Close, period);
        var pta = df.ta.variance(close: df.close, length: period, ddof: 0);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void WMA()
    {
        WMA_Series QL = new(bars.Close, period, false);
        var pta = df.ta.wma(close: df.close, length: period);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }

    [Fact]
    private void ZSCORE()
    {
        ZSCORE_Series QL = new(bars.Close, period, false);
        var pta = df.ta.zscore(close: df.close, length: period, ddof: 0);
        for (var i = QL.Length - 1; i > skip; i--)
        {
            var QL_item = QL[i - 1].v;
            var PanTA_item = (double)pta[i - 1];
            Assert.InRange(PanTA_item! - QL_item, -Math.Exp(-digits), Math.Exp(-digits));
        }
    }
}

public static class PythonLibrary
{
    public static string Locate()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
            foreach (string path in paths)
            {
                string[] pythonDLLs = Directory.GetFiles(path, "python3*.dll");
                if (pythonDLLs.Length > 0)
                {
                    foreach (string item in pythonDLLs)
                    {
                        if (!item.EndsWith("python3.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }

                }
            }
            throw new FileNotFoundException("Python library not found in PATH");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "/usr/lib/x86_64-linux-gnu/libpython3.10.so";
            /*
                        List<string> pythonLibraries = new List<string>();
                        List<string> directoriesToSearch = new List<string> { "/home/runner/.local/lib" }; // Add more directories as needed
                        string filePattern = "libpython3.*.so";
                        SearchFiles(directoriesToSearch, filePattern, pythonLibraries);

                        if (pythonLibraries.Count > 0) {
                            return pythonLibraries[0];
                        }
                        else {
                            throw new FileNotFoundException("Python library not found");
                        }
            */
        }

        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new NotSupportedException("Not supported yet");
        }

        else { throw new NotSupportedException("Unsupported operating system"); }
    }
    static void SearchFiles(List<string> directoriesToSearch, string filePattern, List<string> foundFiles)
    {
        foreach (string directory in directoriesToSearch)
        {
            if (Directory.Exists(directory))
            {
                try
                {
                    string[] files = Directory.GetFiles(directory, filePattern, SearchOption.AllDirectories);
                    foundFiles.AddRange(files);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error searching in directory: " + directory + " - " + e.Message);
                }
            }
        }
    }
}