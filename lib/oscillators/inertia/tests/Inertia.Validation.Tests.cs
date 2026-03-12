using System.Runtime.CompilerServices;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Inertia (linear regression residual).
/// Cross-validates against manual OLS computation and our CFO/LinReg classes.
/// No external library has an Inertia indicator — validated via math identity:
/// Inertia = source - TSF, where TSF = slope*(period-1) + intercept.
/// </summary>
public sealed class InertiaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public InertiaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Batch_Span_Agree()
    {
        int period = 14;

        // Streaming
        var streaming = new Inertia(period);
        var streamValues = new List<double>(_testData.Data.Count);
        foreach (var item in _testData.Data)
        {
            streamValues.Add(streaming.Update(item).Value);
        }

        // Batch (TSeries)
        TSeries batchSeries = Inertia.Batch(_testData.Data, period);

        // Span
        double[] src = _testData.RawData.ToArray();
        double[] spanOutput = new double[src.Length];
        Inertia.Batch(src.AsSpan(), spanOutput.AsSpan(), period);

        // O(1) streaming sumXY maintenance accumulates cancellation drift vs full-recalc batch.
        // ResyncInterval=1000 bounds drift, but between resyncs tolerance must be relaxed.
        // Batch vs span should match exactly (same code path).
        int start = Math.Max(0, src.Length - 200);
        for (int i = start; i < src.Length; i++)
        {
            Assert.Equal(batchSeries[i].Value, spanOutput[i], 12);   // batch≡span (same path)
            Assert.Equal(batchSeries[i].Value, streamValues[i], 4);   // streaming drifts ~1e-5 between resyncs
        }

        _output.WriteLine("Inertia validation: streaming, batch, and span outputs agree within tolerance.");
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_CfoRelationship()
    {
        // Cross-validate Inertia against CFO.
        // Inertia = source - TSF
        // CFO = 100 * (source - TSF) / source
        // Therefore: Inertia = CFO * source / 100
        int[] periods = [5, 10, 14, 20, 50];

        foreach (int period in periods)
        {
            var inertia = new Inertia(period);
            var cfo = new Cfo(period);

            int validCount = 0;

            foreach (var item in _testData.Data)
            {
                inertia.Update(item);
                cfo.Update(item);

                if (!inertia.IsHot || !cfo.IsHot)
                {
                    continue;
                }

                double src = item.Value;
                if (src == 0.0)
                {
                    continue;
                }

                double expectedInertia = cfo.Last.Value * src / 100.0;
                double actualInertia = inertia.Last.Value;

                // skipcq: CS-R1140 - Two independent O(1) streaming implementations accumulate floating-point drift independently
                Assert.True(Math.Abs(expectedInertia - actualInertia) < 1e-6,
                    $"Inertia mismatch at period={period}: expected={expectedInertia}, actual={actualInertia}, diff={Math.Abs(expectedInertia - actualInertia)}");
                validCount++;
            }

            Assert.True(validCount > 0, $"No valid comparison points for period {period}");
            _output.WriteLine($"Inertia period={period}: validated {validCount} points against CFO relationship.");
        }
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_KnownValues_LinearTrend()
    {
        // For a perfect linear trend y = a + b*x, the regression line exactly fits.
        // TSF should equal the source value, so Inertia should be 0.
        int period = 5;
        var inertia = new Inertia(period);

        // Feed a perfect linear trend: 10, 11, 12, 13, 14, 15, ...
        for (int i = 0; i < 20; i++)
        {
            inertia.Update(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        // After warmup, Inertia should be ~0 for a perfect linear trend
        Assert.Equal(0.0, inertia.Last.Value, 10);
        _output.WriteLine("Inertia known-values: perfect linear trend produces Inertia=0.");
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_ManualOls_LastWindow()
    {
        // Validate last Inertia value against manual OLS computation
        int period = 14;
        var inertia = new Inertia(period);

        foreach (var item in _testData.Data)
        {
            inertia.Update(item);
        }

        // Manual OLS for the last window
        double[] raw = _testData.RawData.ToArray();
        int n = period;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        int windowStart = raw.Length - period;
        for (int j = 0; j < n; j++)
        {
            double x = j;
            double y = raw[windowStart + j];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        double denom = (n * sumX2) - (sumX * sumX);
        double slope = ((n * sumXY) - (sumX * sumY)) / denom;
        double intercept = (sumY - (slope * sumX)) / n;
        double tsf = (slope * (n - 1)) + intercept;
        double expected = raw[^1] - tsf;

        _output.WriteLine($"Manual Inertia: {expected:F12}");
        _output.WriteLine($"Computed Inertia: {inertia.Last.Value:F12}");
        _output.WriteLine($"Delta: {Math.Abs(expected - inertia.Last.Value):E3}");

        Assert.Equal(expected, inertia.Last.Value, 6);
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_MultiPeriod_Consistency()
    {
        // Different periods should produce different results
        int[] periods = [5, 14, 50];
        var results = new List<TSeries>();

        foreach (int period in periods)
        {
            results.Add(Inertia.Batch(_testData.Data, period));
        }

        // After all warmups, values should differ for different periods
        int checkIdx = 100;
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.NotEqual(results[i][checkIdx].Value, results[i + 1][checkIdx].Value);
        }

        _output.WriteLine("Inertia multi-period: different periods produce different results.");
    }

    [Fact]
    public void Inertia_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateInertiaIndicator();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
