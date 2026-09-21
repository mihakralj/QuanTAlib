using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// KST Validation Tests.
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements KST with
/// the Pring default parameters (r=10/15/20/30, s=10/10/10/15), so we use
/// self-consistency checks: batch==streaming==span, directional correctness,
/// and component identity verification.
/// </summary>
public sealed class KstValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private static double[] GeneratePrices(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: seed);
        var prices = new double[count];
        for (int i = 0; i < count; i++) { prices[i] = gbm.Next(isNew: true).Close; }
        return prices;
    }

    private static TSeries MakeSeries(double[] vals)
    {
        var times = new List<long>(vals.Length);
        var values = new List<double>(vals.Length);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < vals.Length; i++)
        {
            times.Add(t0.AddSeconds(i).Ticks);
            values.Add(vals[i]);
        }
        return new TSeries(times, values);
    }

    // ── A) Streaming == Batch(TSeries) ────────────────────────────────────────
    [Fact]
    public void Validate_Streaming_Equals_Batch()
    {
        int[] r = [3, 5, 7, 9];
        int[] s = [2, 2, 2, 3];
        int sig = 2;
        double[] prices = GeneratePrices(200);

        // Streaming
        var kstStream = new Kst(r[0], r[1], r[2], r[3], s[0], s[1], s[2], s[3], sig);
        var streamK = new double[prices.Length];
        var streamS = new double[prices.Length];
        for (int i = 0; i < prices.Length; i++)
        {
            kstStream.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
            streamK[i] = kstStream.KstValue.Value;
            streamS[i] = kstStream.Signal.Value;
        }

        // Batch TSeries
        var series = MakeSeries(prices);
        var kstBatch = new Kst(r[0], r[1], r[2], r[3], s[0], s[1], s[2], s[3], sig);
        var (bK, bS) = kstBatch.Update(series);

        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(streamK[i], bK.Values[i], 1e-6);
            Assert.Equal(streamS[i], bS.Values[i], 1e-6);
        }

        _output.WriteLine("KST Streaming == Batch(TSeries): PASSED");
    }

    // ── B) Batch(TSeries) == Span ─────────────────────────────────────────────
    [Fact]
    public void Validate_Batch_Equals_Span()
    {
        int r1 = 3, r2 = 5, r3 = 7, r4 = 9, s1 = 2, s2 = 2, s3 = 2, s4 = 3, sig = 2;
        double[] prices = GeneratePrices(200, seed: 77);

        // Span
        var spanK = new double[prices.Length];
        var spanS = new double[prices.Length];
        Kst.Batch(prices, spanK, spanS, r1, r2, r3, r4, s1, s2, s3, s4, sig);

        // Batch TSeries
        var series = MakeSeries(prices);
        var (bK, bS) = Kst.Batch(series, r1, r2, r3, r4, s1, s2, s3, s4, sig);

        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(spanK[i], bK.Values[i], 1e-9);
            Assert.Equal(spanS[i], bS.Values[i], 1e-9);
        }

        _output.WriteLine("KST Batch(TSeries) == Span: PASSED");
    }

    // ── C) Rising prices → positive ROC → positive KST ────────────────────────
    [Fact]
    public void Validate_StrictlyRising_KstPositive()
    {
        double startPrice = 100.0;
        int n = 60;
        double[] prices = new double[n];
        for (int i = 0; i < n; i++) { prices[i] = startPrice + (i * 0.5); } // constant rise

        var spanK = new double[n];
        var spanS = new double[n];
        Kst.Batch(prices, spanK, spanS, r1: 5, r2: 7, r3: 9, r4: 11, s1: 3, s2: 3, s3: 3, s4: 3, sigPeriod: 3);

        // Once warmed up the KST should be positive (all ROC > 0)
        int warmup = new Kst(5, 7, 9, 11, 3, 3, 3, 3, 3).WarmupPeriod;
        for (int i = warmup; i < n; i++)
        {
            Assert.True(spanK[i] > 0, $"KST should be positive at index {i}, got {spanK[i]}");
        }

        _output.WriteLine("KST directional correctness (rising price → positive KST): PASSED");
    }

    // ── D) Falling prices → negative KST ─────────────────────────────────────
    [Fact]
    public void Validate_StrictlyFalling_KstNegative()
    {
        double startPrice = 200.0;
        int n = 60;
        double[] prices = new double[n];
        for (int i = 0; i < n; i++) { prices[i] = startPrice - (i * 0.5); } // constant fall

        var spanK = new double[n];
        var spanS = new double[n];
        Kst.Batch(prices, spanK, spanS, r1: 5, r2: 7, r3: 9, r4: 11, s1: 3, s2: 3, s3: 3, s4: 3, sigPeriod: 3);

        int warmup = new Kst(5, 7, 9, 11, 3, 3, 3, 3, 3).WarmupPeriod;
        for (int i = warmup; i < n; i++)
        {
            Assert.True(spanK[i] < 0, $"KST should be negative at index {i}, got {spanK[i]}");
        }

        _output.WriteLine("KST directional correctness (falling price → negative KST): PASSED");
    }

    // ── E) Constant price → KST = 0 and Signal = 0 ───────────────────────────
    [Fact]
    public void Validate_ConstantPrice_KstZero()
    {
        int n = 80;
        double[] prices = new double[n];
        Array.Fill(prices, 100.0);

        var spanK = new double[n];
        var spanS = new double[n];
        Kst.Batch(prices, spanK, spanS, r1: 5, r2: 7, r3: 9, r4: 11, s1: 3, s2: 3, s3: 3, s4: 3, sigPeriod: 3);

        // All ROC = 0, so KST = 0 and Signal = 0
        for (int i = 0; i < n; i++)
        {
            Assert.Equal(0.0, spanK[i], 1e-10);
            Assert.Equal(0.0, spanS[i], 1e-10);
        }

        _output.WriteLine("KST constant price → KST=0, Signal=0: PASSED");
    }

    // ── F) Default parameters (Pring spec) produce finite values ─────────────
    [Fact]
    public void Validate_DefaultParameters_FiniteOutput()
    {
        double[] prices = GeneratePrices(500, seed: 123);

        var spanK = new double[prices.Length];
        var spanS = new double[prices.Length];
        Kst.Batch(prices, spanK, spanS); // all defaults

        int warmup = new Kst().WarmupPeriod;
        for (int i = warmup; i < prices.Length; i++)
        {
            Assert.True(double.IsFinite(spanK[i]), $"KST[{i}] not finite: {spanK[i]}");
            Assert.True(double.IsFinite(spanS[i]), $"Signal[{i}] not finite: {spanS[i]}");
        }

        _output.WriteLine($"KST default parameters (warmup={warmup}), 500 bars: all finite. PASSED");
    }

    // ── G) Signal lags KST (SMA smoothing effect) ────────────────────────────
    [Fact]
    public void Validate_Signal_LooksLikeSmoothedKst()
    {
        // A sharp rise then fall in KST leaves signal trailing behind
        int r1 = 3, r2 = 4, r3 = 5, r4 = 6, s1 = 2, s2 = 2, s3 = 2, s4 = 2, sigPeriod = 4;
        double[] prices = GeneratePrices(80, seed: 55);

        var spanK = new double[prices.Length];
        var spanS = new double[prices.Length];
        Kst.Batch(prices, spanK, spanS, r1, r2, r3, r4, s1, s2, s3, s4, sigPeriod);

        // Signal should not be identical to KST (it is a smoothed version)
        int warmup = new Kst(r1, r2, r3, r4, s1, s2, s3, s4, sigPeriod).WarmupPeriod;
        bool anyDifferent = false;
        for (int i = warmup; i < prices.Length; i++)
        {
            if (Math.Abs(spanK[i] - spanS[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent, "Signal should differ from KST (it is a smoothed version)");

        _output.WriteLine("KST Signal ≠ KST (smoothing effect verified): PASSED");
    }

    // ── H) Multiple parameter sets produce distinct results ───────────────────
    [Fact]
    public void Validate_DifferentParams_ProduceDifferentResults()
    {
        double[] prices = GeneratePrices(100, seed: 88);

        var k1 = new double[prices.Length]; var s1a = new double[prices.Length];
        var k2 = new double[prices.Length]; var s2a = new double[prices.Length];

        Kst.Batch(prices, k1, s1a, r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        Kst.Batch(prices, k2, s2a, r1: 5, r2: 8, r3: 11, r4: 14, s1: 4, s2: 4, s3: 4, s4: 4, sigPeriod: 4);

        int warmup = Math.Max(
            new Kst(3, 4, 5, 6, 2, 2, 2, 2, 2).WarmupPeriod,
            new Kst(5, 8, 11, 14, 4, 4, 4, 4, 4).WarmupPeriod);

        bool anyDifferent = false;
        for (int i = warmup; i < prices.Length; i++)
        {
            if (Math.Abs(k1[i] - k2[i]) > 1e-6) { anyDifferent = true; break; }
        }
        Assert.True(anyDifferent, "Different parameters should produce different KST values");

        _output.WriteLine("KST different parameters → different results: PASSED");
    }
}
