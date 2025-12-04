using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using QuanTAlib;
using QuanTAlib.Benchmarks;
using Skender.Stock.Indicators;
using TALib;
using Tulip;

namespace QuanTAlib.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.ShortRun
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithId(".NET 10.0"))
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.StdDev)
            .HideColumns(Column.Job, Column.Error, Column.RatioSD);

        if (args.Length == 0)
        {
            BenchmarkRunner.Run<IndicatorBenchmarks>(config);
        }
        else
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}

[MemoryDiagnoser]
[MarkdownExporter, HtmlExporter]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IndicatorBenchmarks
{
    private const int BarCount = 200_000;
    private const int Period = 100;

    private double[] _closeValues = null!;
    private TSeries _closeTseries = null!;
    private List<Quote> _quotes = null!;

    // Pre-allocated outputs for TA-Lib
    private double[] _talibOutput = null!;

    // Pre-allocated outputs for Tulip
    private double[][] _tulipSmaInputs = null!;
    private double[] _tulipSmaOptions = null!;
    private double[][] _tulipSmaOutputs = null!;
    private double[][] _tulipEmaInputs = null!;
    private double[] _tulipEmaOptions = null!;
    private double[][] _tulipEmaOutputs = null!;
    private double[][] _tulipWmaInputs = null!;
    private double[] _tulipWmaOptions = null!;
    private double[][] _tulipWmaOutputs = null!;
    private double[][] _tulipTrimaInputs = null!;
    private double[] _tulipTrimaOptions = null!;
    private double[][] _tulipTrimaOutputs = null!;

    // Pre-allocated outputs for QuanTAlib Span API
    private double[] _quantalibOutput = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Generate data using GBM
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(BarCount, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        _closeValues = bars.Close.Values.ToArray();
        _closeTseries = bars.Close;

        // Create Skender Quote format
        _quotes = new List<Quote>(BarCount);
        for (int i = 0; i < BarCount; i++)
        {
            _quotes.Add(new Quote
            {
                Date = new DateTime(_closeTseries.Times[i]),
                Open = (decimal)bars.Open.Values[i],
                High = (decimal)bars.High.Values[i],
                Low = (decimal)bars.Low.Values[i],
                Close = (decimal)_closeValues[i],
                Volume = (decimal)bars.Volume.Values[i]
            });
        }

        // Pre-allocate TA-Lib output
        _talibOutput = new double[BarCount];

        // Pre-allocate Tulip arrays
        int smaLookback = Period - 1;
        _tulipSmaInputs = new[] { _closeValues };
        _tulipSmaOptions = new double[] { Period };
        _tulipSmaOutputs = new[] { new double[BarCount - smaLookback] };

        _tulipEmaInputs = new[] { _closeValues };
        _tulipEmaOptions = new double[] { Period };
        _tulipEmaOutputs = new[] { new double[BarCount] };

        _tulipWmaInputs = new[] { _closeValues };
        _tulipWmaOptions = new double[] { Period };
        _tulipWmaOutputs = new[] { new double[BarCount - smaLookback] };

        _tulipTrimaInputs = new[] { _closeValues };
        _tulipTrimaOptions = new double[] { Period };
        _tulipTrimaOutputs = new[] { new double[BarCount - smaLookback] };

        // Pre-allocate QuanTAlib output
        _quantalibOutput = new double[BarCount];
    }

    // ==================== SMA ====================
    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "QuanTAlib SMA (Span)")]
    public void QuanTAlib_Sma_Span() => Sma.Calculate(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "QuanTAlib SMA (TSeries)")]
    public TSeries QuanTAlib_Sma_TSeries() => Sma.Calculate(_closeTseries, Period);

    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "QuanTAlib SMA (Streaming)")]
    public void QuanTAlib_Sma_Streaming()
    {
        var sma = new Sma(Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            _quantalibOutput[i] = sma.Update(new TValue(_closeTseries.Times[i], _closeValues[i])).Value;
        }
    }

    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "Tulip SMA")]
    public void Tulip_Sma() => Tulip.Indicators.sma.Run(_tulipSmaInputs, _tulipSmaOptions, _tulipSmaOutputs);

    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "TALib SMA")]
    public Core.RetCode TALib_Sma() => TALib.Functions.Sma<double>(_closeValues, 0..^0, _talibOutput, out _, Period);

    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "Skender SMA")]
    public double Skender_Sma()
    {
        double sum = 0;
        foreach (var r in _quotes.GetSma(Period))
        {
            sum += (double)(r.Sma ?? 0);
        }
        return sum;
    }

    // ==================== EMA ====================
    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "QuanTAlib EMA (Span)")]
    public void QuanTAlib_Ema_Span() => Ema.Calculate(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "QuanTAlib EMA (TSeries)")]
    public TSeries QuanTAlib_Ema_TSeries() => Ema.Calculate(_closeTseries, Period);

    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "QuanTAlib EMA (Streaming)")]
    public void QuanTAlib_Ema_Streaming()
    {
        var ema = new Ema(Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            _quantalibOutput[i] = ema.Update(new TValue(_closeTseries.Times[i], _closeValues[i])).Value;
        }
    }

    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "Tulip EMA")]
    public void Tulip_Ema() => Tulip.Indicators.ema.Run(_tulipEmaInputs, _tulipEmaOptions, _tulipEmaOutputs);

    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "TALib EMA")]
    public Core.RetCode TALib_Ema() => TALib.Functions.Ema<double>(_closeValues, 0..^0, _talibOutput, out _, Period);

    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "Skender EMA")]
    public double Skender_Ema()
    {
        double sum = 0;
        foreach (var r in _quotes.GetEma(Period))
        {
            sum += (double)(r.Ema ?? 0);
        }
        return sum;
    }

    // ==================== WMA ====================
    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "QuanTAlib WMA (Span)")]
    public void QuanTAlib_Wma_Span() => Wma.Calculate(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "QuanTAlib WMA (TSeries)")]
    public TSeries QuanTAlib_Wma_TSeries() => Wma.Calculate(_closeTseries, Period);

    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "QuanTAlib WMA (Streaming)")]
    public void QuanTAlib_Wma_Streaming()
    {
        var wma = new Wma(Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            _quantalibOutput[i] = wma.Update(new TValue(_closeTseries.Times[i], _closeValues[i])).Value;
        }
    }

    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "Tulip WMA")]
    public void Tulip_Wma() => Tulip.Indicators.wma.Run(_tulipWmaInputs, _tulipWmaOptions, _tulipWmaOutputs);

    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "TALib WMA")]
    public Core.RetCode TALib_Wma() => TALib.Functions.Wma<double>(_closeValues, 0..^0, _talibOutput, out _, Period);

    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "Skender WMA")]
    public double Skender_Wma()
    {
        double sum = 0;
        foreach (var r in _quotes.GetWma(Period))
        {
            sum += (double)(r.Wma ?? 0);
        }
        return sum;
    }

    // ==================== TRIMA ====================
    [BenchmarkCategory("TRIMA")]
    [Benchmark(Description = "QuanTAlib TRIMA (Span)")]
    public void QuanTAlib_Trima_Span() => Trima.Calculate(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("TRIMA")]
    [Benchmark(Description = "QuanTAlib TRIMA (TSeries)")]
    public TSeries QuanTAlib_Trima_TSeries() => Trima.Calculate(_closeTseries, Period);

    [BenchmarkCategory("TRIMA")]
    [Benchmark(Description = "QuanTAlib TRIMA (Streaming)")]
    public void QuanTAlib_Trima_Streaming()
    {
        var trima = new Trima(Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            _quantalibOutput[i] = trima.Update(new TValue(_closeTseries.Times[i], _closeValues[i])).Value;
        }
    }

    [BenchmarkCategory("TRIMA")]
    [Benchmark(Description = "Tulip TRIMA")]
    public void Tulip_Trima() => Tulip.Indicators.trima.Run(_tulipTrimaInputs, _tulipTrimaOptions, _tulipTrimaOutputs);

    [BenchmarkCategory("TRIMA")]
    [Benchmark(Description = "TALib TRIMA")]
    public Core.RetCode TALib_Trima() => TALib.Functions.Trima<double>(_closeValues, 0..^0, _talibOutput, out _, Period);

}
