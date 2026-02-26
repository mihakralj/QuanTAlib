using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for KRI (Kairi Relative Index).
/// KRI is not implemented by TA-Lib, Skender, Tulip, or Ooples,
/// so validation uses streaming == batch == span mode consistency
/// plus mathematical identity checks against the SMA-deviation formula:
/// KRI = 100 × (price − SMA) / SMA.
/// </summary>
public sealed class KriValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const double Tolerance = 1e-12;

    // ── A) Streaming == Batch(Span) ───────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period14()
    {
        const int N = 200;
        const int period = 14;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        // Streaming
        var kri = new Kri(period);
        for (int i = 0; i < N; i++)
        {
            kri.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]), isNew: true);
        }
        double streamVal = kri.Last.Value;

        // Batch span
        var batchOut = new double[N];
        Kri.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        _output.WriteLine($"Streaming KRI={streamVal:F10}, Batch KRI={batchOut[N - 1]:F10}");
        Assert.Equal(streamVal, batchOut[N - 1], Tolerance);
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period20()
    {
        const int N = 300;
        const int period = 20;

        var gbm = new GBM(100.0, 0.05, 0.3, seed: 2002);
        var prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var kri = new Kri(period);
        for (int i = 0; i < N; i++)
        {
            kri.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]), isNew: true);
        }

        var batchOut = new double[N];
        Kri.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        Assert.Equal(kri.Last.Value, batchOut[N - 1], Tolerance);
    }

    // ── B) Batch(TSeries) == Calculate ────────────────────────────────────────
    [Fact]
    public void Validate_Batch_Equals_Calculate()
    {
        const int period = 14;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 77);
        var t0 = DateTime.UtcNow;
        var times = new System.Collections.Generic.List<long>(200);
        var vals = new System.Collections.Generic.List<double>(200);
        for (int i = 0; i < 200; i++)
        {
            times.Add(t0.AddSeconds(i).Ticks);
            vals.Add(gbm.Next(isNew: true).Close);
        }
        var series = new TSeries(times, vals);

        var batchResult = Kri.Batch(series, period);
        var (calcResult, _) = Kri.Calculate(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], calcResult.Values[i], 1e-9);
        }
        _output.WriteLine("KRI Batch == Calculate: PASSED");
    }

    // ── C) Price above SMA → KRI > 0 (bullish) ────────────────────────────────
    [Fact]
    public void Validate_PriceAboveSma_KriPositive()
    {
        // Rising prices: each bar is above the rolling SMA
        const int N = 100;
        const int period = 5;
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = 100.0 + i * 2.0; }

        var batchOut = new double[N];
        Kri.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        int warmup = period;
        for (int i = warmup; i < N; i++)
        {
            Assert.True(batchOut[i] > 0,
                $"KRI should be positive (price above SMA) at index {i}, got {batchOut[i]}");
        }
        _output.WriteLine("KRI price above SMA → KRI > 0: PASSED");
    }

    // ── D) Price below SMA → KRI < 0 (bearish) ───────────────────────────────
    [Fact]
    public void Validate_PriceBelowSma_KriNegative()
    {
        // Falling prices: each bar is below the rolling SMA
        const int N = 100;
        const int period = 5;
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = 200.0 - i * 2.0; }

        var batchOut = new double[N];
        Kri.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        int warmup = period;
        for (int i = warmup; i < N; i++)
        {
            Assert.True(batchOut[i] < 0,
                $"KRI should be negative (price below SMA) at index {i}, got {batchOut[i]}");
        }
        _output.WriteLine("KRI price below SMA → KRI < 0: PASSED");
    }

    // ── E) Constant price → KRI = 0 ───────────────────────────────────────────
    [Fact]
    public void Validate_ConstantPrice_KriIsZero()
    {
        const int N = 50;
        const int period = 10;
        double[] prices = new double[N];
        Array.Fill(prices, 100.0);

        var batchOut = new double[N];
        Kri.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        int warmup = period;
        for (int i = warmup; i < N; i++)
        {
            Assert.Equal(0.0, batchOut[i], 1e-10);
        }
        _output.WriteLine("KRI constant price → KRI = 0: PASSED");
    }

    // ── F) Mathematical formula verification ──────────────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        // Hand-crafted 5-bar SMA: prices = [10, 12, 14, 16, 18] → SMA = 14
        // KRI = 100 * (18 - 14) / 14 = 28.571...
        const int period = 5;
        double[] prices = [10.0, 12.0, 14.0, 16.0, 18.0];
        double expectedSma = (10.0 + 12.0 + 14.0 + 16.0 + 18.0) / 5.0;
        double expectedKri = 100.0 * (18.0 - expectedSma) / expectedSma;

        var batchOut = new double[prices.Length];
        Kri.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        Assert.Equal(expectedKri, batchOut[prices.Length - 1], 1e-9);
        _output.WriteLine($"KRI formula check: expected={expectedKri:F6}, actual={batchOut[^1]:F6}: PASSED");
    }

    // ── G) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        const int N = 200;
        const int period = 14;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 99);
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var out1 = new double[N];
        var out2 = new double[N];
        Kri.Batch(prices.AsSpan(), out1.AsSpan(), period);
        Kri.Batch(prices.AsSpan(), out2.AsSpan(), period);

        for (int i = 0; i < N; i++) { Assert.Equal(out1[i], out2[i], 15); }
        _output.WriteLine("KRI determinism: PASSED");
    }
}
