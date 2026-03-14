using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using QuanTAlib;
using Skender.Stock.Indicators;
using TALib;
using Tulip;

namespace QuanTAlib.Progressive;

// ────────────────────────────────────────────────────────────────
//  Program entry point
// ────────────────────────────────────────────────────────────────
public static class Program
{
    public static void Main(string[] args)
    {
        // Usage:
        //   dotnet run -c Release                          → all 4 indicators
        //   dotnet run -c Release -- --filter *Sma*        → SMA only
        //   dotnet run -c Release -- --filter *Ema*        → EMA only
        //   dotnet run -c Release -- --filter *Wma*        → WMA only
        //   dotnet run -c Release -- --filter *Hma*        → HMA only
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.ShortRun
                .WithRuntime(CoreRuntime.Core10_0)
                .WithId("NET10"))
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.StdDev)
            .HideColumns(Column.Job, Column.Error, Column.RatioSD);

        var benchTypes = new[]
        {
            typeof(ProgressiveSma),
            typeof(ProgressiveEma),
            typeof(ProgressiveWma),
            typeof(ProgressiveHma),
        };

        IEnumerable<Summary> summaries;
        if (args.Length == 0)
        {
            summaries = BenchmarkRunner.Run(benchTypes, config);
        }
        else
        {
            summaries = BenchmarkSwitcher
                .FromTypes(benchTypes)
                .Run(args, config);
        }

        // Print pivot tables after all benchmarks complete
        foreach (Summary summary in summaries)
        {
            PivotPrinter.Print(summary);
        }
    }
}

// ────────────────────────────────────────────────────────────────
//  Shared base: 1 M GBM bars, Skender quotes, Tulip pre-alloc
// ────────────────────────────────────────────────────────────────
public abstract class ProgressiveBase
{
    protected const int BarCount = 1_000_000;

    [Params(10, 50, 100, 500, 1000, 5000)]
    public int Period { get; set; }

    // Raw data
    protected double[] _close = null!;
    protected double[] _output = null!;

    // Skender format
    protected IList<Quote> _quotes = null!;

    // Tulip pre-allocated arrays (re-built per Period in GlobalSetup)
    protected double[][] _tulipInputs = null!;
    protected double[] _tulipOptions = null!;
    protected double[][] _tulipOutputs = null!;

    // TA-Lib output
    protected double[] _talibOutput = null!;

    public virtual void Setup()
    {
        // Generate 1M bars via GBM
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        TBarSeries bars = gbm.Fetch(BarCount, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        _close = bars.Close.Values.ToArray();
        _output = new double[BarCount];
        _talibOutput = new double[BarCount];

        // Build Skender Quote list
        TSeries closeSeries = bars.Close;
        var quotes = new List<Quote>(BarCount);
        for (int i = 0; i < BarCount; i++)
        {
            quotes.Add(new Quote
            {
                Date = new DateTime(closeSeries.Times[i], DateTimeKind.Utc),
                Open = (decimal)bars.Open.Values[i],
                High = (decimal)bars.High.Values[i],
                Low = (decimal)bars.Low.Values[i],
                Close = (decimal)_close[i],
                Volume = (decimal)bars.Volume.Values[i],
            });
        }
        _quotes = quotes;

        // Tulip: base input array (subclasses configure outputs)
        _tulipInputs = new[] { _close };
        _tulipOptions = new double[] { Period };
    }
}

// ────────────────────────────────────────────────────────────────
//  SMA — progressive period benchmark
// ────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[MarkdownExporter]
public class ProgressiveSma : ProgressiveBase
{
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        int lookback = Period - 1;
        _tulipOutputs = new[] { new double[BarCount - lookback] };
    }

    [Benchmark(Description = "QuanTAlib")]
    public void QuanTAlib_Sma() =>
        Sma.Batch(_close.AsSpan(), _output.AsSpan(), Period);

    [Benchmark(Description = "TALib")]
    public Core.RetCode TALib_Sma() =>
        TALib.Functions.Sma<double>(_close, 0..^0, _talibOutput, out _, Period);

    [Benchmark(Description = "Tulip")]
    public void Tulip_Sma() =>
        Indicators.sma.Run(_tulipInputs, _tulipOptions, _tulipOutputs);

    [Benchmark(Description = "Skender")]
    public object Skender_Sma() =>
        _quotes.GetSma(Period);
}

// ────────────────────────────────────────────────────────────────
//  EMA — progressive period benchmark
// ────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[MarkdownExporter]
public class ProgressiveEma : ProgressiveBase
{
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        // Tulip EMA output length = BarCount (no lookback trimming)
        _tulipOutputs = new[] { new double[BarCount] };
    }

    [Benchmark(Description = "QuanTAlib")]
    public void QuanTAlib_Ema() =>
        Ema.Batch(_close.AsSpan(), _output.AsSpan(), Period);

    [Benchmark(Description = "TALib")]
    public Core.RetCode TALib_Ema() =>
        TALib.Functions.Ema<double>(_close, 0..^0, _talibOutput, out _, Period);

    [Benchmark(Description = "Tulip")]
    public void Tulip_Ema() =>
        Indicators.ema.Run(_tulipInputs, _tulipOptions, _tulipOutputs);

    [Benchmark(Description = "Skender")]
    public object Skender_Ema() =>
        _quotes.GetEma(Period);
}

// ────────────────────────────────────────────────────────────────
//  WMA — progressive period benchmark
// ────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[MarkdownExporter]
public class ProgressiveWma : ProgressiveBase
{
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        int lookback = Period - 1;
        _tulipOutputs = new[] { new double[BarCount - lookback] };
    }

    [Benchmark(Description = "QuanTAlib")]
    public void QuanTAlib_Wma() =>
        Wma.Batch(_close.AsSpan(), _output.AsSpan(), Period);

    [Benchmark(Description = "TALib")]
    public Core.RetCode TALib_Wma() =>
        TALib.Functions.Wma<double>(_close, 0..^0, _talibOutput, out _, Period);

    [Benchmark(Description = "Tulip")]
    public void Tulip_Wma() =>
        Indicators.wma.Run(_tulipInputs, _tulipOptions, _tulipOutputs);

    [Benchmark(Description = "Skender")]
    public object Skender_Wma() =>
        _quotes.GetWma(Period);
}

// ────────────────────────────────────────────────────────────────
//  HMA — progressive period benchmark (TALib has no HMA)
// ────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[MarkdownExporter]
public class ProgressiveHma : ProgressiveBase
{
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        int lookback = Period + (int)Math.Sqrt(Period) - 2;
        _tulipOutputs = new[] { new double[BarCount - lookback] };
    }

    [Benchmark(Description = "QuanTAlib")]
    public void QuanTAlib_Hma() =>
        Hma.Batch(_close.AsSpan(), _output.AsSpan(), Period);

    // TALib does NOT implement HMA — omitted intentionally

    [Benchmark(Description = "Tulip")]
    public void Tulip_Hma() =>
        Indicators.hma.Run(_tulipInputs, _tulipOptions, _tulipOutputs);

    [Benchmark(Description = "Skender")]
    public object Skender_Hma() =>
        _quotes.GetHma(Period);
}

// ────────────────────────────────────────────────────────────────
//  Pivot table printer: libraries in rows, periods in columns
// ────────────────────────────────────────────────────────────────
internal static class PivotPrinter
{
    public static void Print(Summary summary)
    {
        if (summary?.Table?.FullContent is null || summary.Table.FullContent.Length == 0)
        {
            return;
        }

        // Extract indicator name from the benchmark class
        string className = summary.BenchmarksCases.FirstOrDefault()?.Descriptor?.Type?.Name ?? "?";
        string indicator = className.Replace("Progressive", "", StringComparison.Ordinal);

        Console.WriteLine();
        Console.WriteLine($"═══ {indicator} — 1 M bars, progressive periods ═══");
        Console.WriteLine();

        // Parse BDN results into (library, period) → mean
        var data = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
        var allPeriods = new SortedSet<int>();

        foreach (BenchmarkReport report in summary.Reports)
        {
            BenchmarkCase bench = report.BenchmarkCase;
            string library = bench.Descriptor.WorkloadMethodDisplayInfo;

            // Extract Period from parameters
            var periodParam = bench.Parameters.Items
                .FirstOrDefault(p => string.Equals(p.Name, "Period", StringComparison.Ordinal));
            if (periodParam is null)
            {
                continue;
            }

            int period = (int)periodParam.Value;
            allPeriods.Add(period);

            // Get mean time
            string mean = "—";
            if (report.ResultStatistics is not null)
            {
                double ns = report.ResultStatistics.Mean;
                mean = FormatTime(ns);
            }

            if (!data.ContainsKey(library))
            {
                data[library] = new Dictionary<int, string>();
            }

            data[library][period] = mean;
        }

        if (data.Count == 0)
        {
            return;
        }

        // Build markdown table
        List<int> periods = allPeriods.ToList();
        string header = "| Library      | " + string.Join(" | ", periods.Select(p => $"p={p,5}")) + " |";
        string separator = "|" + new string('-', 14) + "|" +
            string.Join("|", periods.Select(_ => new string('-', 10))) + "|";

        Console.WriteLine(header);
        Console.WriteLine(separator);

        foreach (var lib in data.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            string row = $"| {lib.Key,-12} | " +
                string.Join(" | ", periods.Select(p =>
                    lib.Value.TryGetValue(p, out string? v) ? $"{v,8}" : $"{"—",8}")) + " |";
            Console.WriteLine(row);
        }

        Console.WriteLine();
    }

    private static string FormatTime(double nanoseconds)
    {
        double ms = nanoseconds / 1_000_000.0;
        return ms < 1.0
            ? $"{ms:F3} ms"
            : $"{ms:F1} ms";
    }
}
