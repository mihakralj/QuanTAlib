using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Coppock Validation Tests.
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements the Coppock Curve,
/// so validation uses self-consistency checks: streaming==batch(TSeries)==batch(Span),
/// directional correctness, and constant-price identity.
/// </summary>
public sealed class CoppockValidationTests(ITestOutputHelper output)
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
        int lr = 5, sr = 4, wp = 4;
        double[] prices = GeneratePrices(200);

        // Streaming
        var cStream = new Coppock(lr, sr, wp);
        var streamOut = new double[prices.Length];
        for (int i = 0; i < prices.Length; i++)
        {
            cStream.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
            streamOut[i] = cStream.Last.Value;
        }

        // Batch TSeries
        var series = MakeSeries(prices);
        var cBatch = new Coppock(lr, sr, wp);
        var batchOut = cBatch.Update(series);

        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(streamOut[i], batchOut.Values[i], 1e-6);
        }

        _output.WriteLine("Coppock Streaming == Batch(TSeries): PASSED");
    }

    // ── B) Batch(TSeries) == Span ─────────────────────────────────────────────
    [Fact]
    public void Validate_Batch_Equals_Span()
    {
        int lr = 5, sr = 4, wp = 4;
        double[] prices = GeneratePrices(200, seed: 77);

        // Span
        var spanOut = new double[prices.Length];
        Coppock.Batch(prices, spanOut, lr, sr, wp);

        // Batch TSeries
        var series = MakeSeries(prices);
        var batchOut = Coppock.Batch(series, lr, sr, wp);

        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(spanOut[i], batchOut.Values[i], 1e-9);
        }

        _output.WriteLine("Coppock Batch(TSeries) == Span: PASSED");
    }

    // ── C) Rising prices → positive ROC → positive Coppock ───────────────────
    [Fact]
    public void Validate_StrictlyRising_CoppockPositive()
    {
        double startPrice = 100.0;
        int n = 60;
        double[] prices = new double[n];
        for (int i = 0; i < n; i++) { prices[i] = startPrice + (i * 0.5); }

        var spanOut = new double[n];
        Coppock.Batch(prices, spanOut, longRoc: 5, shortRoc: 4, wmaPeriod: 4);

        int warmup = new Coppock(5, 4, 4).WarmupPeriod;
        for (int i = warmup; i < n; i++)
        {
            Assert.True(spanOut[i] > 0, $"Coppock should be positive at index {i}, got {spanOut[i]}");
        }

        _output.WriteLine("Coppock directional correctness (rising price → positive): PASSED");
    }

    // ── D) Falling prices → negative Coppock ─────────────────────────────────
    [Fact]
    public void Validate_StrictlyFalling_CoppockNegative()
    {
        double startPrice = 200.0;
        int n = 60;
        double[] prices = new double[n];
        for (int i = 0; i < n; i++) { prices[i] = startPrice - (i * 0.5); }

        var spanOut = new double[n];
        Coppock.Batch(prices, spanOut, longRoc: 5, shortRoc: 4, wmaPeriod: 4);

        int warmup = new Coppock(5, 4, 4).WarmupPeriod;
        for (int i = warmup; i < n; i++)
        {
            Assert.True(spanOut[i] < 0, $"Coppock should be negative at index {i}, got {spanOut[i]}");
        }

        _output.WriteLine("Coppock directional correctness (falling price → negative): PASSED");
    }

    // ── E) Constant price → Coppock = 0 ──────────────────────────────────────
    [Fact]
    public void Validate_ConstantPrice_CoppockZero()
    {
        int n = 60;
        double[] prices = new double[n];
        Array.Fill(prices, 100.0);

        var spanOut = new double[n];
        Coppock.Batch(prices, spanOut, longRoc: 5, shortRoc: 4, wmaPeriod: 4);

        for (int i = 0; i < n; i++)
        {
            Assert.Equal(0.0, spanOut[i], 1e-10);
        }

        _output.WriteLine("Coppock constant price → Coppock=0: PASSED");
    }

    // ── F) Default parameters produce finite values ───────────────────────────
    [Fact]
    public void Validate_DefaultParameters_FiniteOutput()
    {
        double[] prices = GeneratePrices(500, seed: 123);

        var spanOut = new double[prices.Length];
        Coppock.Batch(prices, spanOut); // all defaults

        int warmup = new Coppock().WarmupPeriod;
        for (int i = warmup; i < prices.Length; i++)
        {
            Assert.True(double.IsFinite(spanOut[i]), $"Coppock[{i}] not finite: {spanOut[i]}");
        }

        _output.WriteLine($"Coppock default parameters (warmup={warmup}), 500 bars: all finite. PASSED");
    }

    // ── G) Different parameters produce distinct results ──────────────────────
    [Fact]
    public void Validate_DifferentParams_ProduceDifferentResults()
    {
        double[] prices = GeneratePrices(100, seed: 88);

        var out1 = new double[prices.Length];
        var out2 = new double[prices.Length];

        Coppock.Batch(prices, out1, longRoc: 5, shortRoc: 4, wmaPeriod: 4);
        Coppock.Batch(prices, out2, longRoc: 10, shortRoc: 8, wmaPeriod: 7);

        int warmup = Math.Max(
            new Coppock(5, 4, 4).WarmupPeriod,
            new Coppock(10, 8, 7).WarmupPeriod);

        bool anyDifferent = false;
        for (int i = warmup; i < prices.Length; i++)
        {
            if (Math.Abs(out1[i] - out2[i]) > 1e-6) { anyDifferent = true; break; }
        }
        Assert.True(anyDifferent, "Different parameters should produce different Coppock values");

        _output.WriteLine("Coppock different parameters → different results: PASSED");
    }

    // ── H) Static Batch(TSeries) and Calculate() produce same results ─────────
    [Fact]
    public void Validate_StaticBatch_Equals_Calculate()
    {
        int lr = 5, sr = 4, wp = 4;
        double[] prices = GeneratePrices(100, seed: 55);
        var series = MakeSeries(prices);

        var batchOut = Coppock.Batch(series, lr, sr, wp);
        var (calcOut, _) = Coppock.Calculate(series, lr, sr, wp);

        for (int i = 0; i < prices.Length; i++)
        {
            Assert.Equal(batchOut.Values[i], calcOut.Values[i], 1e-9);
        }

        _output.WriteLine("Coppock static Batch == Calculate: PASSED");
    }
}
