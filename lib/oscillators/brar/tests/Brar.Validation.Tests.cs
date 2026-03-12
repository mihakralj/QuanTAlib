using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for BRAR.
/// BRAR is not implemented by TA-Lib, Skender, Tulip, or Ooples,
/// so validation uses streaming == batch == span mode consistency
/// plus mathematical identity checks.
/// </summary>
public sealed class BrarValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const double Tolerance = 1e-12;

    // ───── Self-consistency: streaming == batch span ─────

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period14()
    {
        const int N = 200;
        const int period = 14;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            opens[i] = bars[i].Open; highs[i] = bars[i].High;
            lows[i] = bars[i].Low; closes[i] = bars[i].Close;
        }

        // Streaming
        var brar = new Brar(period);
        for (int i = 0; i < N; i++) { brar.Update(bars[i], isNew: true); }
        double streamBr = brar.Br;
        double streamAr = brar.Ar;

        // Batch span
        var brBatch = new double[N];
        var arBatch = new double[N];
        Brar.Batch(opens, highs, lows, closes, brBatch, arBatch, period);

        _output.WriteLine($"Streaming BR={streamBr:F8}, Batch BR={brBatch[N-1]:F8}");
        _output.WriteLine($"Streaming AR={streamAr:F8}, Batch AR={arBatch[N-1]:F8}");

        Assert.Equal(streamBr, brBatch[N - 1], Tolerance);
        Assert.Equal(streamAr, arBatch[N - 1], Tolerance);
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period26()
    {
        const int N = 300;
        const int period = 26;

        var gbm = new GBM(100.0, 0.05, 0.3, seed: 2002);
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            opens[i] = bars[i].Open; highs[i] = bars[i].High;
            lows[i] = bars[i].Low; closes[i] = bars[i].Close;
        }

        var brar = new Brar(period);
        for (int i = 0; i < N; i++) { brar.Update(bars[i], isNew: true); }

        var brBatch = new double[N];
        var arBatch = new double[N];
        Brar.Batch(opens, highs, lows, closes, brBatch, arBatch, period);

        Assert.Equal(brar.Br, brBatch[N - 1], Tolerance);
        Assert.Equal(brar.Ar, arBatch[N - 1], Tolerance);
    }

    // ───── Mathematical identity checks ─────

    [Fact]
    public void Validate_SymmetricBars_ArEquals100()
    {
        // Open at center of range → AR = 100 at all times
        const int N = 50;
        const int period = 10;

        var brar = new Brar(period);
        for (int i = 0; i < N; i++)
        {
            brar.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 100.0, high: 110.0, low: 90.0, close: 100.0, volume: 1000), isNew: true);
        }

        Assert.Equal(100.0, brar.Ar, Tolerance);
        _output.WriteLine($"Symmetric AR (expect 100): {brar.Ar}");
    }

    [Fact]
    public void Validate_EqualBrPressure_BrEquals100()
    {
        // High - PrevClose == PrevClose - Low for every bar → BR = 100
        const int N = 50;
        const int period = 10;

        var brar = new Brar(period);
        double close = 100.0;
        for (int i = 0; i < N; i++)
        {
            // high = close + d, low = close - d → symmetric around prevClose
            double d = 5.0;
            double high = close + d;
            double low = close - d;
            brar.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: close, high: high, low: low, close: close, volume: 1000), isNew: true);
            // close stays constant so prevClose = close always
        }

        Assert.Equal(100.0, brar.Br, Tolerance);
        _output.WriteLine($"Symmetric BR (expect 100): {brar.Br}");
    }

    [Fact]
    public void Validate_AllUpBars_ArAbove100()
    {
        // Open much closer to Low than to High → arNum >> arDen → AR > 100
        const int N = 50;
        const int period = 10;

        var brar = new Brar(period);
        for (int i = 0; i < N; i++)
        {
            // Open just above low, high far above
            brar.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 91.0, high: 110.0, low: 90.0, close: 105.0, volume: 1000), isNew: true);
        }

        Assert.True(brar.Ar > 100.0, $"Expected AR > 100, got {brar.Ar}");
        _output.WriteLine($"Bullish AR: {brar.Ar}");
    }

    [Fact]
    public void Validate_AllDownBars_ArBelow100()
    {
        // Open just below high, low far below → arDen >> arNum → AR < 100
        const int N = 50;
        const int period = 10;

        var brar = new Brar(period);
        for (int i = 0; i < N; i++)
        {
            brar.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 109.0, high: 110.0, low: 90.0, close: 95.0, volume: 1000), isNew: true);
        }

        Assert.True(brar.Ar < 100.0, $"Expected AR < 100, got {brar.Ar}");
        _output.WriteLine($"Bearish AR: {brar.Ar}");
    }

    // ───── Determinism ─────

    [Fact]
    public void Validate_Deterministic_SameSeed_SameResult()
    {
        const int N = 150;
        const int period = 20;

        static double ComputeFinalBr(int n, int p, int seed)
        {
            var gbm = new GBM(100.0, 0.05, 0.2, seed: seed);
            var brar = new Brar(p);
            for (int i = 0; i < n; i++) { brar.Update(gbm.Next(isNew: true), isNew: true); }
            return brar.Br;
        }

        double run1 = ComputeFinalBr(N, period, 777);
        double run2 = ComputeFinalBr(N, period, 777);

        Assert.Equal(run1, run2, Tolerance);
        _output.WriteLine($"Deterministic BR: {run1}");
    }

    // ───── Full intermediate series consistency ─────

    [Fact]
    [SkipLocalsInit]
    public void Validate_AllBars_Streaming_Vs_Batch_Match()
    {
        const int N = 100;
        const int period = 10;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 3333);
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            opens[i] = bars[i].Open; highs[i] = bars[i].High;
            lows[i] = bars[i].Low; closes[i] = bars[i].Close;
        }

        var brBatch = new double[N];
        var arBatch = new double[N];
        Brar.Batch(opens, highs, lows, closes, brBatch, arBatch, period);

        // Compare every bar, not just last
        var brar = new Brar(period);
        int mismatches = 0;
        for (int i = 0; i < N; i++)
        {
            brar.Update(bars[i], isNew: true);
            double diff = Math.Abs(brar.Br - brBatch[i]);
            if (diff > Tolerance)
            {
                mismatches++;
                _output.WriteLine($"BR mismatch at i={i}: streaming={brar.Br}, batch={brBatch[i]}, diff={diff:E3}");
            }
        }

        Assert.Equal(0, mismatches);
        _output.WriteLine($"All {N} bars match between streaming and batch");
    }
}
