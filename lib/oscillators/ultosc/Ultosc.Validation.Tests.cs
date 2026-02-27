using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Ultimate Oscillator validation tests.
/// Cross-validates against Skender.Stock.Indicators.GetUltimate,
/// TALib.NETCore, Tulip.NETCore, OoplesFinance, and self-consistency checks.
/// </summary>
public sealed class UltoscValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public UltoscValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _data.Dispose();
            _disposed = true;
        }
    }

    private static TBarSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // --- A) Streaming vs Batch agreement ---

    [Fact]
    public void Streaming_Matches_Batch()
    {
        var series = GenerateSeries(300);
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;

        var ultosc = new Ultosc(p1, p2, p3);
        for (int i = 0; i < series.Count; i++)
        {
            ultosc.Update(series[i]);
        }

        var batch = Ultosc.Batch(series, p1, p2, p3);

        Assert.Equal(ultosc.Last.Value, batch[^1].Value, 1e-6);
    }

    // --- B) Span matches TBarSeries ---

    [Fact]
    public void Span_Matches_TBarSeries()
    {
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;

        double[] hData = _data.HighPrices.ToArray();
        double[] lData = _data.LowPrices.ToArray();
        double[] cData = _data.ClosePrices.ToArray();
        double[] spanOutput = new double[hData.Length];

        Ultosc.Batch(hData, lData, cData, spanOutput, p1, p2, p3);

        var ultosc = new Ultosc(p1, p2, p3);
        var tbarResult = ultosc.Update(_data.Bars);

        for (int i = 0; i < tbarResult.Count; i++)
        {
            Assert.Equal(tbarResult[i].Value, spanOutput[i], 1e-10);
        }

        _output.WriteLine("Span calculation matches TBarSeries batch calculation.");
    }

    // --- C) Constant bars → Ultosc = 50 ---

    [Fact]
    public void ConstantBars_ValueIs_50()
    {
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;
        int count = 60;

        var bars = new TBarSeries();
        for (int i = 0; i < count; i++)
        {
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50, 50, 50, 100));
        }

        var result = Ultosc.Batch(bars, p1, p2, p3);

        // When all OHLC are identical, BP=0, TR=0 → avg=0.5 each → Ultosc=50
        for (int i = p3; i < count; i++)
        {
            Assert.Equal(50.0, result.Values[i], 1e-10);
        }
    }

    // --- D) Directional correctness ---

    [Fact]
    public void Rising_Produces_HighValues()
    {
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;

        var bars = new TBarSeries();
        for (int i = 0; i < 60; i++)
        {
            double price = 100.0 + (i * 2.0);
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price + 0.5, 100));
        }

        var ultosc = new Ultosc(p1, p2, p3);
        for (int i = 0; i < bars.Count; i++)
        {
            ultosc.Update(bars[i]);
        }

        // Close consistently near high → strong buying pressure → Ultosc > 50
        Assert.True(ultosc.Last.Value > 50.0,
            $"Expected > 50 for rising prices, got {ultosc.Last.Value}");
    }

    [Fact]
    public void Falling_Produces_LowValues()
    {
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;

        var bars = new TBarSeries();
        for (int i = 0; i < 60; i++)
        {
            double price = 200.0 - (i * 2.0);
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price - 0.5, 100));
        }

        var ultosc = new Ultosc(p1, p2, p3);
        for (int i = 0; i < bars.Count; i++)
        {
            ultosc.Update(bars[i]);
        }

        // Close consistently near low → weak buying pressure → Ultosc < 50
        Assert.True(ultosc.Last.Value < 50.0,
            $"Expected < 50 for falling prices, got {ultosc.Last.Value}");
    }

    // --- E) Cross-validation with Skender (batch) ---

    [Fact]
    public void Skender_Batch_Matches()
    {
        int[][] periodSets = [[7, 14, 28]];

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_data.Bars);

            var sResult = _data.SkenderQuotes.GetUltimate(p1, p2, p3).ToList();

            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Ultimate, tolerance: ValidationHelper.SkenderTolerance);
        }

        _output.WriteLine("Skender batch validation passed.");
    }

    // --- F) Cross-validation with Skender (streaming) ---

    [Fact]
    public void Skender_Streaming_Matches()
    {
        int[][] periodSets = [[7, 14, 28]];

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            var ultosc = new Ultosc(p1, p2, p3);
            var qResults = new List<double>();
            foreach (var item in _data.Bars)
            {
                qResults.Add(ultosc.Update(item).Value);
            }

            var sResult = _data.SkenderQuotes.GetUltimate(p1, p2, p3).ToList();

            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Ultimate, tolerance: ValidationHelper.SkenderTolerance);
        }

        _output.WriteLine("Skender streaming validation passed.");
    }

    // --- G) Cross-validation with TA-Lib (batch) ---

    [Fact]
    public void TALib_Batch_Matches()
    {
        int[][] periodSets = [[7, 14, 28]];

        double[] hData = _data.HighPrices.ToArray();
        double[] lData = _data.LowPrices.ToArray();
        double[] cData = _data.ClosePrices.ToArray();
        double[] output = new double[hData.Length];

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_data.Bars);

            var retCode = TALib.Functions.UltOsc(hData, lData, cData, 0..^0, output, out var outRange, p1, p2, p3);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.UltOscLookback(p1, p2, p3);

            ValidationHelper.VerifyData(qResult, output, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }

        _output.WriteLine("TA-Lib batch validation passed.");
    }

    // --- H) Cross-validation with TA-Lib (streaming) ---

    [Fact]
    public void TALib_Streaming_Matches()
    {
        int[][] periodSets = [[7, 14, 28]];

        double[] hData = _data.HighPrices.ToArray();
        double[] lData = _data.LowPrices.ToArray();
        double[] cData = _data.ClosePrices.ToArray();
        double[] output = new double[hData.Length];

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            var ultosc = new Ultosc(p1, p2, p3);
            var qResults = new List<double>();
            foreach (var item in _data.Bars)
            {
                qResults.Add(ultosc.Update(item).Value);
            }

            var retCode = TALib.Functions.UltOsc(hData, lData, cData, 0..^0, output, out var outRange, p1, p2, p3);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.UltOscLookback(p1, p2, p3);

            ValidationHelper.VerifyData(qResults, output, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }

        _output.WriteLine("TA-Lib streaming validation passed.");
    }

    // --- I) Cross-validation with Tulip (batch) ---

    [Fact]
    public void Tulip_Batch_Matches()
    {
        int[][] periodSets = [[7, 14, 28]];

        double[] hData = _data.HighPrices.ToArray();
        double[] lData = _data.LowPrices.ToArray();
        double[] cData = _data.ClosePrices.ToArray();

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_data.Bars);

            var ultoscIndicator = Tulip.Indicators.ultosc;
            double[][] inputs = [hData, lData, cData];
            double[] options = [p1, p2, p3];

            int lookback = ultoscIndicator.Start(options);
            double[][] outputs = [new double[hData.Length - lookback]];

            ultoscIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }

        _output.WriteLine("Tulip batch validation passed.");
    }

    // --- J) Cross-validation with Tulip (streaming) ---

    [Fact]
    public void Tulip_Streaming_Matches()
    {
        int[][] periodSets = [[7, 14, 28]];

        double[] hData = _data.HighPrices.ToArray();
        double[] lData = _data.LowPrices.ToArray();
        double[] cData = _data.ClosePrices.ToArray();

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            var ultosc = new Ultosc(p1, p2, p3);
            var qResults = new List<double>();
            foreach (var item in _data.Bars)
            {
                qResults.Add(ultosc.Update(item).Value);
            }

            var ultoscIndicator = Tulip.Indicators.ultosc;
            double[][] inputs = [hData, lData, cData];
            double[] options = [p1, p2, p3];

            int lookback = ultoscIndicator.Start(options);
            double[][] outputs = [new double[hData.Length - lookback]];

            ultoscIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }

        _output.WriteLine("Tulip streaming validation passed.");
    }

    // --- K) Cross-validation with Ooples ---

    [Fact]
    public void Ooples_Batch_Matches()
    {
        int[][] periodSets = [[7, 14, 28]];

        var ooplesData = _data.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Close = (double)q.Close,
            High = (double)q.High,
            Low = (double)q.Low,
            Open = (double)q.Open,
            Volume = (double)q.Volume
        }).ToList();

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_data.Bars);

            var stockData = new StockData(ooplesData);
            var sResult = stockData.CalculateUltimateOscillator(p1, p2, p3).OutputValues.Values.First();

            ValidationHelper.VerifyData(qResult, sResult, (s) => s, 100, ValidationHelper.OoplesTolerance);
        }

        _output.WriteLine("Ooples batch validation passed.");
    }

    // --- L) Range bounded [0, 100] ---

    [Fact]
    public void Output_Bounded_0_To_100()
    {
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;

        var result = Ultosc.Batch(_data.Bars, p1, p2, p3);

        for (int i = p3; i < _data.Bars.Count; i++)
        {
            double val = result.Values[i];
            Assert.True(val >= 0.0 && val <= 100.0,
                $"Ultosc value {val} out of [0, 100] range at bar {i}");
        }

        _output.WriteLine("All Ultosc values within [0, 100] range.");
    }

    // --- M) Determinism ---

    [Fact]
    public void Deterministic_Across_Runs()
    {
        var series = GenerateSeries(200, seed: 99);
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;

        var r1 = Ultosc.Batch(series, p1, p2, p3);
        var r2 = Ultosc.Batch(series, p1, p2, p3);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(r1.Values[i], r2.Values[i], 15);
        }
    }

    // --- N) Multi-period consistency ---

    [Fact]
    public void Different_Periods_Produce_Different_Results()
    {
        var series = GenerateSeries(200);

        var r1 = Ultosc.Batch(series, 5, 10, 20);
        var r2 = Ultosc.Batch(series, 7, 14, 28);

        bool anyDifferent = false;
        for (int i = 28; i < 200; i++)
        {
            if (Math.Abs(r1.Values[i] - r2.Values[i]) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent);
    }

    // --- O) Calculate returns consistent results ---

    [Fact]
    public void Calculate_Produces_Consistent_Results()
    {
        var series = GenerateSeries(100);
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;

        var (results, indicator) = Ultosc.Calculate(series, p1, p2, p3);

        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    // --- P) All outputs finite after warmup ---

    [Fact]
    public void AllOutputsFinite_AfterWarmup()
    {
        const int p1 = 7;
        const int p2 = 14;
        const int p3 = 28;
        var ultosc = new Ultosc(p1, p2, p3);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var result = ultosc.Update(_data.Bars[i]);

            if (i >= p3)
            {
                Assert.True(double.IsFinite(result.Value),
                    $"Non-finite output at bar {i}: {result.Value}");
            }
        }

        _output.WriteLine("All outputs finite after warmup verified.");
    }
}
