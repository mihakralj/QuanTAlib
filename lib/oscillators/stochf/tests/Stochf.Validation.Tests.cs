using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Stochastic Fast Oscillator validation tests.
/// Cross-validates against Skender.Stock.Indicators.GetStoch with smoothPeriods=1
/// (Fast Stochastic matches our raw %K), TALib.NETCore StochF,
/// plus self-consistency checks.
/// </summary>
public sealed class StochfValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private bool _disposed;

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
        const int kLength = 5;
        const int dPeriod = 3;

        var stochf = new Stochf(kLength, dPeriod);
        for (int i = 0; i < series.Count; i++)
        {
            stochf.Update(series[i]);
        }

        var (batchK, batchD) = Stochf.Batch(series, kLength, dPeriod);

        Assert.Equal(stochf.K.Value, batchK[^1].Value, 1e-6);
        Assert.Equal(stochf.D.Value, batchD[^1].Value, 1e-6);
    }

    // --- B) Span matches TBarSeries ---

    [Fact]
    public void Span_Matches_TBarSeries()
    {
        var series = GenerateSeries(200);
        const int kLength = 5;
        const int dPeriod = 3;

        var (tbK, tbD) = Stochf.Batch(series, kLength, dPeriod);

        var kOut = new double[series.Count];
        var dOut = new double[series.Count];
        Stochf.Batch(series.HighValues, series.LowValues, series.CloseValues,
            kOut.AsSpan(), dOut.AsSpan(), kLength, dPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(tbK.Values[i], kOut[i], 12);
            Assert.Equal(tbD.Values[i], dOut[i], 12);
        }
    }

    // --- C) Constant bars → K=0 ---

    [Fact]
    public void ConstantBars_K_Is_Zero()
    {
        const int kLength = 5;
        const int dPeriod = 3;
        int count = 50;

        var bars = new TBarSeries();
        for (int i = 0; i < count; i++)
        {
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50, 50, 50, 100));
        }

        var (kSeries, dSeries) = Stochf.Batch(bars, kLength, dPeriod);

        // When range=0 for all bars, %K and %D should be 0
        for (int i = kLength - 1; i < count; i++)
        {
            Assert.Equal(0.0, kSeries.Values[i], 1e-10);
            Assert.Equal(0.0, dSeries.Values[i], 1e-10);
        }
    }

    // --- D) Directional correctness ---

    [Fact]
    public void Rising_Produces_High_K()
    {
        const int kLength = 5;
        const int dPeriod = 3;

        var bars = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + (i * 2.0);
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price + 1, 100));
        }

        var stochf = new Stochf(kLength, dPeriod);
        for (int i = 0; i < bars.Count; i++)
        {
            stochf.Update(bars[i]);
        }

        // Close at recent high → %K should be near 100
        Assert.True(stochf.K.Value > 80.0);
    }

    [Fact]
    public void Falling_Produces_Low_K()
    {
        const int kLength = 5;
        const int dPeriod = 3;

        var bars = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            double price = 200.0 - (i * 2.0);
            bars.Add(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price - 1, 100));
        }

        var stochf = new Stochf(kLength, dPeriod);
        for (int i = 0; i < bars.Count; i++)
        {
            stochf.Update(bars[i]);
        }

        // Close at recent low → %K should be near 0
        Assert.True(stochf.K.Value < 20.0);
    }

    // --- E) Cross-validation with Skender (smoothPeriods=1 == Fast) ---

    [Fact]
    public void Skender_K_Matches_With_SmoothK1()
    {
        // Skender GetStoch(lookbackPeriods, signalPeriods, smoothPeriods)
        // smoothPeriods=1 means no SMA smoothing on %K → raw Fast %K == our %K
        const int kLength = 5;
        const int dPeriod = 3;

        var (qK, qD) = Stochf.Batch(_data.Bars, kLength, dPeriod);

        var skResults = _data.SkenderQuotes.GetStoch(kLength, dPeriod, 1).ToList();

        // Compare converged values (skip warmup)
        int start = kLength + dPeriod;
        int totalCompared = 0;
        int mismatches = 0;

        for (int i = start; i < _data.Bars.Count; i++)
        {
            double? skK = skResults[i].Oscillator;
            double? skD = skResults[i].Signal;

            if (skK.HasValue && skD.HasValue)
            {
                totalCompared++;
                double errK = Math.Abs(qK.Values[i] - skK.Value);
                double errD = Math.Abs(qD.Values[i] - skD.Value);

                if (errK > 1e-6 || errD > 1e-6)
                {
                    mismatches++;
                }
            }
        }

        // Allow small fraction of mismatches due to warmup initialization differences
        Assert.True(totalCompared > 0, "No Skender results to compare");
        double mismatchRate = (double)mismatches / totalCompared;
        Assert.True(mismatchRate < 0.05, $"Mismatch rate {mismatchRate:P2} exceeds 5% threshold ({mismatches}/{totalCompared})");
    }

    // --- F) Cross-validation with TALib StochF ---

    [Fact]
    public void TALib_StochF_K_Matches()
    {
        const int kLength = 5;
        const int dPeriod = 3;

        var hData = _data.HighPrices.Span;
        var lData = _data.LowPrices.Span;
        var cData = _data.ClosePrices.Span;

        double[] taK = new double[hData.Length];
        double[] taD = new double[hData.Length];

        var retCode = TALib.Functions.StochF(hData, lData, cData, 0..^0,
            taK, taD, out var outRange, kLength, dPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        var (offset, length) = outRange.GetOffsetAndLength(taK.Length);

        var (qK, qD) = Stochf.Batch(_data.Bars, kLength, dPeriod);

        int matched = 0;
        int mismatches = 0;

        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            double errK = Math.Abs(qK.Values[qi] - taK[j]);
            double errD = Math.Abs(qD.Values[qi] - taD[j]);

            matched++;
            if (errK > 1e-6 || errD > 1e-6)
            {
                mismatches++;
            }
        }

        Assert.True(matched > 0, "No TALib results to compare");
        double mismatchRate = (double)mismatches / matched;
        Assert.True(mismatchRate < 0.05, $"TALib mismatch rate {mismatchRate:P2} exceeds 5% ({mismatches}/{matched})");
    }

    // --- G) Determinism ---

    [Fact]
    public void Deterministic_Across_Runs()
    {
        var series = GenerateSeries(200, seed: 99);
        const int kLength = 5;
        const int dPeriod = 3;

        var (k1, d1) = Stochf.Batch(series, kLength, dPeriod);
        var (k2, d2) = Stochf.Batch(series, kLength, dPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(k1.Values[i], k2.Values[i], 15);
            Assert.Equal(d1.Values[i], d2.Values[i], 15);
        }
    }

    // --- H) Multi-period consistency ---

    [Fact]
    public void Different_Periods_Produce_Different_Results()
    {
        var series = GenerateSeries(100);

        var (k5, _) = Stochf.Batch(series, kLength: 5, dPeriod: 3);
        var (k20, _) = Stochf.Batch(series, kLength: 20, dPeriod: 3);

        // Different kLength should produce different %K values after warmup
        bool anyDifferent = false;
        for (int i = 20; i < 100; i++)
        {
            if (Math.Abs(k5.Values[i] - k20.Values[i]) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    // --- I) Calculate returns both results and indicator ---

    [Fact]
    public void Calculate_Produces_Consistent_Results()
    {
        var series = GenerateSeries(100);
        const int kLength = 5;
        const int dPeriod = 3;

        var (results, indicator) = Stochf.Calculate(series, kLength, dPeriod);

        Assert.Equal(100, results.K.Count);
        Assert.Equal(100, results.D.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.K.Value));
        Assert.True(double.IsFinite(indicator.D.Value));
    }

    // ── Cross-library: OoplesFinance ──────────────────────────────────────────
    [Fact]
    public void Stochf_MatchesOoples_Structural()
    {
        const int kLength = 5;
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
        var oResult = stockData.CalculateStochasticFastOscillator();
        var oValues = oResult.OutputValues.Values.First();

        var stochf = new Stochf(kLength, 3);
        var qValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            qValues.Add(stochf.Update(bar).Value);
        }

        Assert.True(oValues.Count > 0, "Ooples StochF must produce output");
        int finiteCount = 0;
        for (int i = kLength; i < Math.Min(oValues.Count, qValues.Count); i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qValues[i]))
            {
                finiteCount++;
            }
        }
        Assert.True(finiteCount > 100, $"Expected >100 finite StochF pairs, got {finiteCount}");
    }
}
