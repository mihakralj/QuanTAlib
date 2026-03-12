using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for PSL (Psychological Line).
/// PSL is not implemented by TA-Lib, Skender, Tulip, or Ooples,
/// so validation uses streaming == batch == span mode consistency
/// plus mathematical identity checks against the formula:
/// PSL = 100 × (count of up-bars in period) / period.
/// </summary>
public sealed class PslValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const double Tolerance = 1e-12;

    // ── A) Streaming == Batch(Span) ───────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period12()
    {
        const int N = 200;
        const int period = 12;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        // Streaming
        var psl = new Psl(period);
        for (int i = 0; i < N; i++)
        {
            psl.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]), isNew: true);
        }
        double streamVal = psl.Last.Value;

        // Batch span
        var batchOut = new double[N];
        Psl.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        _output.WriteLine($"Streaming PSL={streamVal:F10}, Batch PSL={batchOut[N - 1]:F10}");
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

        var psl = new Psl(period);
        for (int i = 0; i < N; i++)
        {
            psl.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]), isNew: true);
        }

        var batchOut = new double[N];
        Psl.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        Assert.Equal(psl.Last.Value, batchOut[N - 1], Tolerance);
    }

    // ── B) Batch(TSeries) == Calculate ────────────────────────────────────────
    [Fact]
    public void Validate_Batch_Equals_Calculate()
    {
        const int period = 12;
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

        var batchResult = Psl.Batch(series, period);
        var (calcResult, _) = Psl.Calculate(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], calcResult.Values[i], 1e-9);
        }
        _output.WriteLine("PSL Batch == Calculate: PASSED");
    }

    // ── C) All up-bars → PSL = 100 ────────────────────────────────────────────
    [Fact]
    public void Validate_AllUpBars_PslIs100()
    {
        // Monotonically rising prices: every bar is an up-bar
        const int N = 50;
        const int period = 12;
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = 100.0 + i; }

        var batchOut = new double[N];
        Psl.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        int warmup = period;
        for (int i = warmup; i < N; i++)
        {
            Assert.Equal(100.0, batchOut[i], 1e-9);
        }
        _output.WriteLine("PSL all up-bars → PSL = 100: PASSED");
    }

    // ── D) All down-bars → PSL = 0 ────────────────────────────────────────────
    [Fact]
    public void Validate_AllDownBars_PslIsZero()
    {
        // Monotonically falling prices: every bar is a down-bar
        const int N = 50;
        const int period = 12;
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = 200.0 - i; }

        var batchOut = new double[N];
        Psl.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        int warmup = period;
        for (int i = warmup; i < N; i++)
        {
            Assert.Equal(0.0, batchOut[i], 1e-9);
        }
        _output.WriteLine("PSL all down-bars → PSL = 0: PASSED");
    }

    // ── E) Alternating bars → PSL = 50 (when period is even) ─────────────────
    [Fact]
    public void Validate_AlternatingBars_PslIs50()
    {
        // Alternating up/down after warmup (even period)
        const int N = 100;
        const int period = 10; // even period
        double[] prices = new double[N];
        prices[0] = 100.0;
        for (int i = 1; i < N; i++)
        {
            prices[i] = prices[i - 1] + (i % 2 == 0 ? 1.0 : -1.0);
        }

        var batchOut = new double[N];
        Psl.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        // After warmup, alternating pattern → 5/10 = 50%
        int checkStart = period + 10;
        for (int i = checkStart; i < N; i++)
        {
            Assert.Equal(50.0, batchOut[i], 1e-9);
        }
        _output.WriteLine("PSL alternating bars (period=10) → PSL = 50: PASSED");
    }

    // ── F) Output range [0, 100] ──────────────────────────────────────────────
    [Fact]
    public void Validate_OutputRange_ZeroToHundred()
    {
        const int N = 300;
        const int period = 12;
        var gbm = new GBM(100.0, 0.5, 2.0, seed: 42);
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var batchOut = new double[N];
        Psl.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        for (int i = 0; i < N; i++)
        {
            Assert.True(batchOut[i] >= 0.0 && batchOut[i] <= 100.0,
                $"PSL out of [0,100] range at index {i}: {batchOut[i]}");
        }
        _output.WriteLine("PSL output range [0, 100]: PASSED");
    }

    // ── G) Formula verification — manual calculation ──────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        // prices = [10, 11, 9, 12, 11, 13] → up-bars: {11>10, 12>9, 13>11} = 3 of 5
        // After 6 bars with period=5: last 5 are [11, 9, 12, 11, 13]
        // up-bars in that window: 9<11(down), 12>9(up), 11<12(down), 13>11(up) → 2/5?
        // Actually includes first transition into the buffer, let's use known window:
        // window [9, 12, 11, 13, ?] — use 6 bars so window is last 5
        // prices[1..5] = [11,9,12,11,13]: diffs=[11-10=up, 9-11=down, 12-9=up, 11-12=down, 13-11=up] = 3/5 = 60
        const int period = 5;
        double[] prices = [10.0, 11.0, 9.0, 12.0, 11.0, 13.0];

        var batchOut = new double[prices.Length];
        Psl.Batch(prices.AsSpan(), batchOut.AsSpan(), period);

        // At index 5: window = last 5 prices [11,9,12,11,13]
        // up comparisons: 11>10=yes, 9>11=no, 12>9=yes, 11>12=no, 13>11=yes → 3/5 = 60
        Assert.Equal(60.0, batchOut[prices.Length - 1], 1e-9);
        _output.WriteLine($"PSL formula check: expected=60, actual={batchOut[^1]}: PASSED");
    }

    // ── H) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        const int N = 200;
        const int period = 12;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 99);
        double[] prices = new double[N];
        for (int i = 0; i < N; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var out1 = new double[N];
        var out2 = new double[N];
        Psl.Batch(prices.AsSpan(), out1.AsSpan(), period);
        Psl.Batch(prices.AsSpan(), out2.AsSpan(), period);

        for (int i = 0; i < N; i++) { Assert.Equal(out1[i], out2[i], 15); }
        _output.WriteLine("PSL determinism: PASSED");
    }
}
