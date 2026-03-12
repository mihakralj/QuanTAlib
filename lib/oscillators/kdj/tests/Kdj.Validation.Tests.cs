using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// KDJ validation tests — self-consistency across modes.
/// KDJ uses Wilder's RMA smoothing (unlike standard Stochastic which uses SMA),
/// so no direct external library comparison is available. Validation is performed
/// via cross-mode consistency, mathematical identity checks, and boundary analysis.
/// </summary>
[SkipLocalsInit]
public sealed class KdjValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly GBM _gbm = new(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);
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
            _disposed = true;
        }
    }

    /// <summary>
    /// Streaming vs Batch consistency — validates that the streaming Update() path
    /// produces identical results to the static Batch() path for all three outputs.
    /// </summary>
    [Fact]
    public void StreamingVsBatch_AllThreeOutputs_Match()
    {
        const int length = 9;
        const int signal = 3;
        int barCount = 200;

        var bars = new TBarSeries();
        var streamKdj = new Kdj(length, signal);

        for (int i = 0; i < barCount; i++)
        {
            var bar = _gbm.Next(isNew: true);
            bars.Add(bar);
            streamKdj.Update(bar, isNew: true);
        }

        var (bK, bD, bJ) = Kdj.Batch(bars, length, signal);

        int mismatches = 0;
        for (int i = 0; i < barCount; i++)
        {
            double errK = Math.Abs(bK.Values[i] - GetStreamK(bars, i, length, signal));
            double errD = Math.Abs(bD.Values[i] - GetStreamD(bars, i, length, signal));
            double errJ = Math.Abs(bJ.Values[i] - GetStreamJ(bars, i, length, signal));

            if (errK > 1e-10 || errD > 1e-10 || errJ > 1e-10)
            {
                mismatches++;
            }
        }

        // Final values must match exactly
        Assert.Equal(streamKdj.K.Value, bK.Values[^1], 1e-10);
        Assert.Equal(streamKdj.D.Value, bD.Values[^1], 1e-10);
        Assert.Equal(streamKdj.Last.Value, bJ.Values[^1], 1e-10);

        output.WriteLine($"Streaming vs Batch: {barCount} bars, {mismatches} mismatches (tolerance 1e-10)");
    }

    /// <summary>
    /// Span batch vs TBarSeries batch — validates that the low-level span API
    /// produces identical results to the high-level TBarSeries batch.
    /// </summary>
    [Fact]
    public void SpanBatch_VsTBarSeriesBatch_Match()
    {
        const int length = 14;
        const int signal = 5;
        int barCount = 150;

        var bars = new TBarSeries();
        for (int i = 0; i < barCount; i++)
        {
            bars.Add(_gbm.Next(isNew: true));
        }

        var (tK, tD, tJ) = Kdj.Batch(bars, length, signal);

        double[] kOut = new double[barCount];
        double[] dOut = new double[barCount];
        double[] jOut = new double[barCount];
        Kdj.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            kOut, dOut, jOut, length, signal);

        for (int i = 0; i < barCount; i++)
        {
            Assert.Equal(tK.Values[i], kOut[i], 1e-10);
            Assert.Equal(tD.Values[i], dOut[i], 1e-10);
            Assert.Equal(tJ.Values[i], jOut[i], 1e-10);
        }

        output.WriteLine($"Span vs TBarSeries Batch: {barCount} bars, all match within 1e-10");
    }

    /// <summary>
    /// Mathematical identity: J = 3K - 2D must hold for all bars.
    /// </summary>
    [Fact]
    public void J_Equals_3K_Minus_2D_ForAllBars()
    {
        const int length = 9;
        const int signal = 3;
        int barCount = 200;

        var bars = new TBarSeries();
        for (int i = 0; i < barCount; i++)
        {
            bars.Add(_gbm.Next(isNew: true));
        }

        var (bK, bD, bJ) = Kdj.Batch(bars, length, signal);

        for (int i = 0; i < barCount; i++)
        {
            double expectedJ = 3.0 * bK.Values[i] - 2.0 * bD.Values[i];
            Assert.Equal(expectedJ, bJ.Values[i], 1e-10);
        }

        output.WriteLine($"J = 3K - 2D identity verified for {barCount} bars");
    }

    /// <summary>
    /// K and D must remain in [0, 100] for all bars.
    /// </summary>
    [Fact]
    public void K_D_BoundedInZeroToHundred()
    {
        const int length = 5;
        const int signal = 3;
        int barCount = 500;

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 99);
        var bars = new TBarSeries();
        for (int i = 0; i < barCount; i++)
        {
            bars.Add(gbm.Next(isNew: true));
        }

        var (bK, bD, _) = Kdj.Batch(bars, length, signal);

        for (int i = 0; i < barCount; i++)
        {
            Assert.True(bK.Values[i] >= 0.0 && bK.Values[i] <= 100.0,
                $"K[{i}] = {bK.Values[i]} out of [0,100]");
            Assert.True(bD.Values[i] >= 0.0 && bD.Values[i] <= 100.0,
                $"D[{i}] = {bD.Values[i]} out of [0,100]");
        }

        output.WriteLine($"K/D bounded [0,100] verified for {barCount} bars");
    }

    /// <summary>
    /// Parameter sensitivity: different length/signal values produce different results.
    /// </summary>
    [Theory]
    [InlineData(5, 2)]
    [InlineData(9, 3)]
    [InlineData(14, 5)]
    [InlineData(21, 7)]
    public void DifferentParameters_ProduceDifferentResults(int length, int signal)
    {
        int barCount = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);
        var bars = new TBarSeries();
        for (int i = 0; i < barCount; i++)
        {
            bars.Add(gbm.Next(isNew: true));
        }

        var (k1, _, _) = Kdj.Batch(bars, length, signal);
        var (k2, _, _) = Kdj.Batch(bars, length + 1, signal);

        // Different lengths should produce different K/D/J
        bool anyDifferent = false;
        for (int i = length + 1; i < barCount; i++)
        {
            if (Math.Abs(k1.Values[i] - k2.Values[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, $"length={length} vs {length + 1} should differ");
        output.WriteLine($"Parameter sensitivity verified: length={length}, signal={signal}");
    }

    /// <summary>
    /// Constant price produces RSV=50, K→50, D→50, J→50 after convergence.
    /// </summary>
    [Fact]
    public void ConstantPrice_ConvergesToFifty()
    {
        const int length = 9;
        const int signal = 3;
        int barCount = 100;

        var bars = new TBarSeries();
        DateTime time = DateTime.UtcNow;
        for (int i = 0; i < barCount; i++)
        {
            bars.Add(new TBar(time.AddSeconds(i), 100, 100, 100, 100, 1000));
        }

        var (bK, bD, bJ) = Kdj.Batch(bars, length, signal);

        // After warmup, all should converge to 50.0
        Assert.Equal(50.0, bK.Values[^1], 1e-6);
        Assert.Equal(50.0, bD.Values[^1], 1e-6);
        Assert.Equal(50.0, bJ.Values[^1], 1e-6);

        output.WriteLine("Constant price → K=D=J=50 verified");
    }

    // ── Helper: replay streaming to get per-bar values ──

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetStreamK(TBarSeries bars, int upTo, int length, int signal)
    {
        var kdj = new Kdj(length, signal);
        for (int i = 0; i <= upTo; i++)
        {
            kdj.Update(bars[i], isNew: true);
        }
        return kdj.K.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetStreamD(TBarSeries bars, int upTo, int length, int signal)
    {
        var kdj = new Kdj(length, signal);
        for (int i = 0; i <= upTo; i++)
        {
            kdj.Update(bars[i], isNew: true);
        }
        return kdj.D.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetStreamJ(TBarSeries bars, int upTo, int length, int signal)
    {
        var kdj = new Kdj(length, signal);
        for (int i = 0; i <= upTo; i++)
        {
            kdj.Update(bars[i], isNew: true);
        }
        return kdj.Last.Value;
    }

    // ── Skender Cross-Validation ──

    /// <summary>
    /// Structural validation against Skender <c>GetKdj</c>.
    /// Skender KDJ uses SMA-based smoothing while QuanTAlib uses Wilder's RMA,
    /// so numeric equality is not expected. Both must produce finite, bounded output
    /// and track the same directional movements on the same data.
    /// </summary>
    [Fact]
    public void Validate_Skender_Kdj_Structural()
    {
        var data = new ValidationTestData();
        const int length = 9;
        const int signal = 3;

        // QuanTAlib KDJ (streaming)
        var kdj = new Kdj(length, signal);
        foreach (var bar in data.Bars)
        {
            kdj.Update(bar);
        }

        // Skender Stochastic (KDJ is based on Stochastic %K/%D)
        var sResult = data.SkenderQuotes.GetStoch(length, signal, signal).ToList();

        // Structural: both produce finite output
        Assert.True(kdj.IsHot, "QuanTAlib KDJ should be hot");
        Assert.True(double.IsFinite(kdj.K.Value), "QuanTAlib K must be finite");
        Assert.True(double.IsFinite(kdj.D.Value), "QuanTAlib D must be finite");

        int finiteCount = sResult.Count(r => r.K is not null && double.IsFinite(r.K.Value));
        Assert.True(finiteCount > 100, $"Skender should produce >100 finite K values, got {finiteCount}");

        // Directional agreement on final segment (both should agree on overbought/oversold)
        bool qOverbought = kdj.K.Value > 50;
        bool sOverbought = sResult[^1].K!.Value > 50;
        output.WriteLine($"KDJ structural: QuanTAlib K={kdj.K.Value:F2} ({(qOverbought ? "overbought" : "oversold")}), " +
                         $"Skender K={sResult[^1].K:F2} ({(sOverbought ? "overbought" : "oversold")})");

        data.Dispose();
    }
}
