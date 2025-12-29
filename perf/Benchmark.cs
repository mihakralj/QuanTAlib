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
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;

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
    private const int BarCount = 500_000;
    private const int Period = 220;

    private double[] _closeValues = null!;
    private TSeries _closeTseries = null!;
    private List<Quote> _quotes = null!;
    private List<TickerData> _ooplesData = null!;

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
    private double[][] _tulipHmaInputs = null!;
    private double[] _tulipHmaOptions = null!;
    private double[][] _tulipHmaOutputs = null!;

    // Pre-allocated outputs for ADOSC
    private double[] _highValues = null!;
    private double[] _lowValues = null!;
    private double[] _volumeValues = null!;
    private TBarSeries _bars = null!;
    private double[][] _tulipAdoscInputs = null!;
    private double[] _tulipAdoscOptions = null!;
    private double[][] _tulipAdoscOutputs = null!;

    // Pre-allocated outputs for QuanTAlib Span API
    private double[] _quantalibOutput = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Generate data using GBM
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(BarCount, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        _bars = bars;
        _closeValues = bars.Close.Values.ToArray();
        _highValues = bars.High.Values.ToArray();
        _lowValues = bars.Low.Values.ToArray();
        _volumeValues = bars.Volume.Values.ToArray();
        _closeTseries = bars.Close;

        // Create Skender Quote format
        _quotes = new List<Quote>(BarCount);
        for (int i = 0; i < BarCount; i++)
        {
            _quotes.Add(new Quote
            {
                Date = new DateTime(_closeTseries.Times[i], DateTimeKind.Utc),
                Open = (decimal)bars.Open.Values[i],
                High = (decimal)bars.High.Values[i],
                Low = (decimal)bars.Low.Values[i],
                Close = (decimal)_closeValues[i],
                Volume = (decimal)bars.Volume.Values[i]
            });
        }

        // Create Ooples TickerData format
        _ooplesData = new List<TickerData>(BarCount);
        for (int i = 0; i < BarCount; i++)
        {
            _ooplesData.Add(new TickerData
            {
                Date = new DateTime(_closeTseries.Times[i], DateTimeKind.Utc),
                Open = bars.Open.Values[i],
                High = bars.High.Values[i],
                Low = bars.Low.Values[i],
                Close = _closeValues[i],
                Volume = bars.Volume.Values[i]
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

        int hmaLookback = Period + (int)Math.Sqrt(Period) - 2;
        _tulipHmaInputs = new[] { _closeValues };
        _tulipHmaOptions = new double[] { Period };
        _tulipHmaOutputs = new[] { new double[BarCount - hmaLookback] };

        // Pre-allocate Tulip ADOSC
        _tulipAdoscInputs = new[] { _highValues, _lowValues, _closeValues, _volumeValues };
        _tulipAdoscOptions = new double[] { 3, 10 }; // Fast=3, Slow=10
        _tulipAdoscOutputs = new[] { new double[BarCount - 1] }; // Tulip ADOSC starts at index 1?

        // Pre-allocate QuanTAlib output
        _quantalibOutput = new double[BarCount];
    }

    // ==================== ADOSC ====================
    [BenchmarkCategory("ADOSC")]
    [Benchmark(Description = "QuanTAlib ADOSC (Span)")]
    public void QuanTAlib_Adosc_Span() => Adosc.Calculate(_highValues.AsSpan(), _lowValues.AsSpan(), _closeValues.AsSpan(), _volumeValues.AsSpan(), _quantalibOutput.AsSpan(), 3, 10);

    [BenchmarkCategory("ADOSC")]
    [Benchmark(Description = "QuanTAlib ADOSC (Batch)")]
    public TSeries QuanTAlib_Adosc_TSeries() => Adosc.Batch(_bars, 3, 10);

    [BenchmarkCategory("ADOSC")]
    [Benchmark(Description = "QuanTAlib ADOSC (Streaming)")]
    public void QuanTAlib_Adosc_Streaming()
    {
        var adosc = new Adosc(3, 10);
        for (int i = 0; i < _bars.Count; i++)
        {
            _quantalibOutput[i] = adosc.Update(_bars[i]).Value;
        }
    }

    [BenchmarkCategory("ADOSC")]
    [Benchmark(Description = "Tulip ADOSC")]
    public void Tulip_Adosc() => Tulip.Indicators.adosc.Run(_tulipAdoscInputs, _tulipAdoscOptions, _tulipAdoscOutputs);

    [BenchmarkCategory("ADOSC")]
    [Benchmark(Description = "TALib ADOSC")]
    public Core.RetCode TALib_Adosc() => TALib.Functions.AdOsc(_highValues, _lowValues, _closeValues, _volumeValues, 0..^0, _quantalibOutput, out _, 3, 10);

    [BenchmarkCategory("ADOSC")]
    [Benchmark(Description = "Skender ADOSC")]
    public object Skender_Adosc() => _quotes.GetChaikinOsc(3, 10);

    [BenchmarkCategory("ADOSC")]
    [Benchmark(Description = "Ooples ADOSC")]
    public object Ooples_Adosc() => new StockData(_ooplesData).CalculateChaikinOscillator(MovingAvgType.ExponentialMovingAverage, 3, 10);

    // ==================== SMA ====================
    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "QuanTAlib SMA (Span)")]
    public void QuanTAlib_Sma_Span() => Sma.Batch(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "QuanTAlib SMA (Batch)")]
    public TSeries QuanTAlib_Sma_TSeries() => Sma.Calculate(_closeTseries, Period).Results;

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
    [Benchmark(Description = "QuanTAlib SMA (Eventing)")]
    public void QuanTAlib_Sma_Eventing()
    {
        var source = new TSeries();
        var sma = new Sma(source, Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            source.Add(new TValue(_closeTseries.Times[i], _closeValues[i]));
            _quantalibOutput[i] = sma.Last.Value;
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
    public object Skender_Sma() => _quotes.GetSma(Period);

    [BenchmarkCategory("SMA")]
    [Benchmark(Description = "Ooples SMA")]
    public object Ooples_Sma() => new StockData(_ooplesData).CalculateSimpleMovingAverage(Period);

    // ==================== EMA ====================
    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "QuanTAlib EMA (Span)")]
    public void QuanTAlib_Ema_Span() => Ema.Batch(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "QuanTAlib EMA (Batch)")]
    public TSeries QuanTAlib_Ema_TSeries() => Ema.Calculate(_closeTseries, Period).Results;

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
    [Benchmark(Description = "QuanTAlib EMA (Eventing)")]
    public void QuanTAlib_Ema_Eventing()
    {
        var source = new TSeries();
        var ema = new Ema(source, Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            source.Add(new TValue(_closeTseries.Times[i], _closeValues[i]));
            _quantalibOutput[i] = ema.Last.Value;
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
    public object Skender_Ema() => _quotes.GetEma(Period);

    [BenchmarkCategory("EMA")]
    [Benchmark(Description = "Ooples EMA")]
    public object Ooples_Ema() => new StockData(_ooplesData).CalculateExponentialMovingAverage(Period);

    // ==================== WMA ====================
    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "QuanTAlib WMA (Span)")]
    public void QuanTAlib_Wma_Span() => Wma.Batch(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "QuanTAlib WMA (Batch)")]
    public TSeries QuanTAlib_Wma_TSeries() => Wma.Batch(_closeTseries, Period);

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
    [Benchmark(Description = "QuanTAlib WMA (Eventing)")]
    public void QuanTAlib_Wma_Eventing()
    {
        var source = new TSeries();
        var wma = new Wma(source, Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            source.Add(new TValue(_closeTseries.Times[i], _closeValues[i]));
            _quantalibOutput[i] = wma.Last.Value;
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
    public object Skender_Wma() => _quotes.GetWma(Period);

    [BenchmarkCategory("WMA")]
    [Benchmark(Description = "Ooples WMA")]
    public object Ooples_Wma() => new StockData(_ooplesData).CalculateWeightedMovingAverage(Period);

    // ==================== HMA ====================
    [BenchmarkCategory("HMA")]
    [Benchmark(Description = "QuanTAlib HMA (Span)")]
    public void QuanTAlib_Hma_Span() => Hma.Calculate(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("HMA")]
    [Benchmark(Description = "QuanTAlib HMA (Batch)")]
    public TSeries QuanTAlib_Hma_TSeries() => Hma.Batch(_closeTseries, Period);

    [BenchmarkCategory("HMA")]
    [Benchmark(Description = "QuanTAlib HMA (Streaming)")]
    public void QuanTAlib_Hma_Streaming()
    {
        var hma = new Hma(Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            _quantalibOutput[i] = hma.Update(new TValue(_closeTseries.Times[i], _closeValues[i])).Value;
        }
    }

    [BenchmarkCategory("HMA")]
    [Benchmark(Description = "QuanTAlib HMA (Eventing)")]
    public void QuanTAlib_Hma_Eventing()
    {
        var source = new TSeries();
        var hma = new Hma(source, Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            source.Add(new TValue(_closeTseries.Times[i], _closeValues[i]));
            _quantalibOutput[i] = hma.Last.Value;
        }
    }

    [BenchmarkCategory("HMA")]
    [Benchmark(Description = "Tulip HMA")]
    public void Tulip_Hma() => Tulip.Indicators.hma.Run(_tulipHmaInputs, _tulipHmaOptions, _tulipHmaOutputs);

    [BenchmarkCategory("HMA")]
    [Benchmark(Description = "Skender HMA")]
    public object Skender_Hma() => _quotes.GetHma(Period);

    [BenchmarkCategory("HMA")]
    [Benchmark(Description = "Ooples HMA")]
    public object Ooples_Hma() => new StockData(_ooplesData).CalculateHullMovingAverage(MovingAvgType.WeightedMovingAverage, Period);

    // ==================== SKEW ====================
    [BenchmarkCategory("SKEW")]
    [Benchmark(Description = "QuanTAlib Skew (Span)")]
    public void QuanTAlib_Skew_Span() => Skew.Batch(_closeValues.AsSpan(), _quantalibOutput.AsSpan(), Period);

    [BenchmarkCategory("SKEW")]
    [Benchmark(Description = "QuanTAlib Skew (Batch)")]
    public TSeries QuanTAlib_Skew_TSeries() => Skew.Calculate(_closeTseries, Period);

    [BenchmarkCategory("SKEW")]
    [Benchmark(Description = "QuanTAlib Skew (Streaming)")]
    public void QuanTAlib_Skew_Streaming()
    {
        var skew = new Skew(Period);
        for (int i = 0; i < _closeValues.Length; i++)
        {
            _quantalibOutput[i] = skew.Update(new TValue(_closeTseries.Times[i], _closeValues[i])).Value;
        }
    }
}
