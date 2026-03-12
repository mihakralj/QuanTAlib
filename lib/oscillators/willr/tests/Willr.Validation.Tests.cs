using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Williams %R validation tests.
/// Cross-validates against Skender.Stock.Indicators.GetWilliamsR,
/// TALib.NETCore, Tulip.NETCore, and self-consistency checks.
/// </summary>
public sealed class WillrValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public WillrValidationTests(ITestOutputHelper output)
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
        const int period = 14;

        var willr = new Willr(period);
        for (int i = 0; i < series.Count; i++)
        {
            willr.Update(series[i]);
        }

        var batch = Willr.Batch(series, period);

        Assert.Equal(willr.Last.Value, batch[^1].Value, 1e-6);
    }

    // --- B) Span matches TBarSeries ---

    [Fact]
    public void Span_Matches_TBarSeries()
    {
        var series = GenerateSeries(200);
        const int period = 14;

        var batchResult = Willr.Batch(series, period);

        var output = new double[series.Count];
        Willr.Batch(series.HighValues, series.LowValues, series.CloseValues,
            output.AsSpan(), period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], output[i], 12);
        }
    }

    // --- C) Constant bars → WillR = -50 ---

    [Fact]
    public void ConstantBars_ValueIs_Neg50()
    {
        const int period = 14;
        int count = 50;

        var bars = new TBarSeries();
        for (int i = 0; i < count; i++)
        {
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50, 50, 50, 100));
        }

        var result = Willr.Batch(bars, period);

        // When range=0 for all bars, WillR = -50
        for (int i = period - 1; i < count; i++)
        {
            Assert.Equal(-50.0, result.Values[i], 1e-10);
        }
    }

    // --- D) Directional correctness ---

    [Fact]
    public void Rising_Produces_NearZero()
    {
        const int period = 5;

        var bars = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + (i * 2.0);
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price + 1, 100));
        }

        var willr = new Willr(period);
        for (int i = 0; i < bars.Count; i++)
        {
            willr.Update(bars[i]);
        }

        // Close at recent high → WillR near 0 (> -20)
        Assert.True(willr.Last.Value > -20.0);
    }

    [Fact]
    public void Falling_Produces_NearNeg100()
    {
        const int period = 5;

        var bars = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            double price = 200.0 - (i * 2.0);
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price - 1, 100));
        }

        var willr = new Willr(period);
        for (int i = 0; i < bars.Count; i++)
        {
            willr.Update(bars[i]);
        }

        // Close at recent low → WillR near -100 (< -80)
        Assert.True(willr.Last.Value < -80.0);
    }

    // --- E) Cross-validation with Skender ---

    [Fact]
    public void Skender_Matches()
    {
        const int period = 14;

        var qResult = Willr.Batch(_data.Bars, period);

        var skResults = _data.SkenderQuotes.GetWilliamsR(period).ToList();

        // Compare converged values (skip warmup)
        int start = period;
        int totalCompared = 0;
        int mismatches = 0;

        for (int i = start; i < _data.Bars.Count; i++)
        {
            double? skWillR = skResults[i].WilliamsR;

            if (skWillR.HasValue)
            {
                totalCompared++;
                double err = Math.Abs(qResult.Values[i] - skWillR.Value);

                if (err > 1e-9)
                {
                    mismatches++;
                }
            }
        }

        Assert.True(totalCompared > 0, "No Skender results to compare");
        double mismatchRate = (double)mismatches / totalCompared;
        Assert.True(mismatchRate < 0.01,
            $"Mismatch rate {mismatchRate:P2} exceeds 1% threshold ({mismatches}/{totalCompared})");

        _output.WriteLine($"Skender validation: {totalCompared} compared, {mismatches} mismatches ({mismatchRate:P2})");
    }

    // --- F) Cross-validation with TA-Lib ---

    [Fact]
    public void TALib_Matches()
    {
        const int period = 14;
        int len = _data.Bars.Count;

        var qResult = Willr.Batch(_data.Bars, period);

        double[] taOutput = new double[len];

        var retCode = TALib.Functions.WillR(
            _data.HighPrices.Span, _data.LowPrices.Span, _data.ClosePrices.Span,
            0..^0, taOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.WillRLookback(period);

        ValidationHelper.VerifyData(qResult, taOutput, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);

        _output.WriteLine("TA-Lib validation passed.");
    }

    // --- G) Cross-validation with Tulip ---

    [Fact]
    public void Tulip_Matches()
    {
        const int period = 14;
        int len = _data.Bars.Count;

        var qResult = Willr.Batch(_data.Bars, period);

        double[][] tulipInputs = [_data.HighPrices.ToArray(), _data.LowPrices.ToArray(), _data.ClosePrices.ToArray()];
        double[][] tulipOutputs = [new double[len - period + 1]];

        _ = Tulip.Indicators.willr.Run(tulipInputs, [period], tulipOutputs);

        int lookback = period - 1;
        ValidationHelper.VerifyData(qResult, tulipOutputs[0], lookback, tolerance: ValidationHelper.TulipTolerance);

        _output.WriteLine("Tulip validation passed.");
    }

    // --- H) Inverse Stochastic identity ---

    [Fact]
    public void WillR_Is_Inverse_Stoch()
    {
        var series = GenerateSeries(500, seed: 77);
        const int period = 14;

        var willr = Willr.Batch(series, period);
        var (stochK, _) = Stoch.Batch(series, kLength: period);

        // WillR = Stoch%K - 100 when range > 0
        int totalCompared = 0;
        for (int i = period; i < series.Count; i++)
        {
            double stochVal = stochK.Values[i];
            double willrVal = willr.Values[i];

            // Skip degenerate range=0 cases (Stoch returns 0, WillR returns -50)
            if (Math.Abs(stochVal) > 1e-10 || Math.Abs(willrVal + 50.0) > 1e-10)
            {
                Assert.Equal(stochVal - 100.0, willrVal, 1e-9);
                totalCompared++;
            }
        }

        Assert.True(totalCompared > 0, "No valid comparison points");
        _output.WriteLine($"Inverse Stochastic identity: validated {totalCompared} points.");
    }

    // --- I) Determinism ---

    [Fact]
    public void Deterministic_Across_Runs()
    {
        var series = GenerateSeries(200, seed: 99);
        const int period = 14;

        var r1 = Willr.Batch(series, period);
        var r2 = Willr.Batch(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(r1.Values[i], r2.Values[i], 15);
        }
    }

    // --- J) Multi-period consistency ---

    [Fact]
    public void Different_Periods_Produce_Different_Results()
    {
        var series = GenerateSeries(100);

        var r5 = Willr.Batch(series, period: 5);
        var r20 = Willr.Batch(series, period: 20);

        bool anyDifferent = false;
        for (int i = 20; i < 100; i++)
        {
            if (Math.Abs(r5.Values[i] - r20.Values[i]) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    // --- K) Calculate returns consistent results ---

    [Fact]
    public void Calculate_Produces_Consistent_Results()
    {
        var series = GenerateSeries(100);
        const int period = 14;

        var (results, indicator) = Willr.Calculate(series, period);

        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    // --- L) All outputs finite after warmup ---

    [Fact]
    public void AllOutputsFinite_AfterWarmup()
    {
        const int period = 14;
        var willr = new Willr(period);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var result = willr.Update(_data.Bars[i]);

            if (i >= period - 1)
            {
                Assert.True(double.IsFinite(result.Value),
                    $"Non-finite output at bar {i}: {result.Value}");
            }
        }

        _output.WriteLine("All outputs finite after warmup verified.");
    }

    // --- M) Range bounded ---

    [Fact]
    public void Output_Bounded_Neg100_To_Zero()
    {
        const int period = 14;
        var result = Willr.Batch(_data.Bars, period);

        for (int i = period - 1; i < _data.Bars.Count; i++)
        {
            double val = result.Values[i];
            Assert.True(val >= -100.0 && val <= 0.0,
                $"WillR value {val} out of [-100, 0] range at bar {i}");
        }

        _output.WriteLine("All WillR values within [-100, 0] range.");
    }

    // ── Cross-library: OoplesFinance ──────────────────────────────────────────
    [Fact]
    public void Willr_MatchesOoples_Structural()
    {
        const int period = 14;
        var ooplesData = _data.Bars.Select(static b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateWilliamsR(length: period);
        var oValues = oResult.OutputValues.Values.First();

        var willr = new Willr(period);
        var qValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            qValues.Add(willr.Update(bar).Value);
        }

        Assert.True(oValues.Count > 0, "Ooples WillR must produce output");
        int finiteCount = 0;
        for (int i = period; i < Math.Min(oValues.Count, qValues.Count); i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qValues[i]))
            {
                finiteCount++;
            }
        }
        Assert.True(finiteCount > 100, $"Expected >100 finite WillR pairs, got {finiteCount}");
        _output.WriteLine($"WillR Ooples structural: {finiteCount} finite pairs verified.");
    }
}
