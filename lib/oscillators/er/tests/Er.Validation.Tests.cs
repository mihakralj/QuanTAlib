using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for ER (Efficiency Ratio).
/// ER is not implemented by TA-Lib, Skender, Tulip, or Ooples as a standalone
/// indicator, so validation uses streaming == batch == span mode consistency
/// plus mathematical identity checks against the signal/noise definition.
/// </summary>
public sealed class ErValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const double Tolerance = 1e-12;

    // ── A) Streaming == Batch(TSeries) ────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period10()
    {
        const int N = 200;
        const int period = 10;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        // Streaming
        var er = new Er(period);
        for (int i = 0; i < N; i++)
        {
            er.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]), isNew: true);
        }
        double streamVal = er.Last.Value;

        // Batch span
        var output2 = new double[N];
        Er.Batch(prices.AsSpan(), output2.AsSpan(), period);

        _output.WriteLine($"Streaming ER={streamVal:F10}, Batch ER={output2[N - 1]:F10}");
        Assert.Equal(streamVal, output2[N - 1], Tolerance);
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

        var er = new Er(period);
        for (int i = 0; i < N; i++)
        {
            er.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]), isNew: true);
        }

        var output2 = new double[N];
        Er.Batch(prices.AsSpan(), output2.AsSpan(), period);

        Assert.Equal(er.Last.Value, output2[N - 1], Tolerance);
    }

    // ── B) Batch(TSeries) == Calculate(TSeries) ───────────────────────────────
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

        var batchResult = Er.Batch(series, period);
        var (calcResult, _) = Er.Calculate(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], calcResult.Values[i], 1e-9);
        }
        _output.WriteLine("ER Batch == Calculate: PASSED");
    }

    // ── C) Trending price → ER approaches 1 ─────────────────────────────────
    [Fact]
    public void Validate_StrictlyRising_ErApproachesOne()
    {
        const int N = 100;
        const int period = 10;
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = 100.0 + (i * 1.0); }

        var output2 = new double[N];
        Er.Batch(prices.AsSpan(), output2.AsSpan(), period);

        int warmup = period + 1;
        for (int i = warmup; i < N; i++)
        {
            Assert.True(output2[i] > 0.99,
                $"ER should be near 1.0 for perfectly trending data at index {i}, got {output2[i]}");
        }
        _output.WriteLine("ER strictly rising → ER ≈ 1.0: PASSED");
    }

    // ── D) Choppy price → ER approaches 0 ────────────────────────────────────
    [Fact]
    public void Validate_ChoppyPrice_ErApproachesZero()
    {
        const int N = 100;
        const int period = 10;
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = 100.0 + (i % 2 == 0 ? 1.0 : -1.0); }

        var output2 = new double[N];
        Er.Batch(prices.AsSpan(), output2.AsSpan(), period);

        int warmup = period + 1;
        for (int i = warmup; i < N; i++)
        {
            Assert.True(output2[i] < 0.1,
                $"ER should be near 0 for choppy data at index {i}, got {output2[i]}");
        }
        _output.WriteLine("ER choppy price → ER ≈ 0: PASSED");
    }

    // ── E) Output clamped [0, 1] ──────────────────────────────────────────────
    [Fact]
    public void Validate_OutputClamped_ZeroToOne()
    {
        const int N = 300;
        const int period = 10;
        var gbm = new GBM(100.0, 0.5, 2.0, seed: 42);
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var output2 = new double[N];
        Er.Batch(prices.AsSpan(), output2.AsSpan(), period);

        for (int i = 0; i < N; i++)
        {
            Assert.True(output2[i] >= 0.0 && output2[i] <= 1.0,
                $"ER out of [0,1] range at index {i}: {output2[i]}");
        }
        _output.WriteLine("ER output clamped [0, 1]: PASSED");
    }

    // ── F) Determinism across runs ────────────────────────────────────────────
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
        Er.Batch(prices.AsSpan(), out1.AsSpan(), period);
        Er.Batch(prices.AsSpan(), out2.AsSpan(), period);

        for (int i = 0; i < N; i++)
        {
            Assert.Equal(out1[i], out2[i], 15);
        }
        _output.WriteLine("ER determinism: PASSED");
    }

    // ── G) Different periods produce different results ────────────────────────
    [Fact]
    public void Validate_DifferentPeriods_DifferentResults()
    {
        const int N = 200;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 55);
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var out5 = new double[N];
        var out20 = new double[N];
        Er.Batch(prices.AsSpan(), out5.AsSpan(), 5);
        Er.Batch(prices.AsSpan(), out20.AsSpan(), 20);

        bool anyDiff = false;
        for (int i = 25; i < N; i++)
        {
            if (Math.Abs(out5[i] - out20[i]) > 0.001)
            {
                anyDiff = true;
                break;
            }
        }
        Assert.True(anyDiff, "Different periods should produce different ER values");
        _output.WriteLine("ER different periods produce different results: PASSED");
    }

    // ── H) Constant price → ER = 0 ───────────────────────────────────────────
    [Fact]
    public void Validate_ConstantPrice_ErIsZero()
    {
        const int N = 50;
        const int period = 10;
        double[] prices = new double[N];
        Array.Fill(prices, 100.0);

        var output2 = new double[N];
        Er.Batch(prices.AsSpan(), output2.AsSpan(), period);

        int warmup = period + 1;
        for (int i = warmup; i < N; i++)
        {
            Assert.Equal(0.0, output2[i], 1e-10);
        }
        _output.WriteLine("ER constant price → ER = 0: PASSED");
    }
}
