// PIVOTFIB Validation Tests - Fibonacci Pivot Points
// Self-consistency validation: math correctness, streaming==batch, streaming==span,
// streaming==batchAll, determinism, Calculate, level ordering.
// No external library implements Fibonacci Pivot Points with bar-to-bar granularity.

using System.Runtime.InteropServices;

namespace QuanTAlib.Tests;

public sealed class PivotfibValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // ── Math correctness ────────────────────────────────────────────
    [Fact]
    public void MathCorrectness_FibonacciFormula()
    {
        var bars = CreateGbmBars();
        var ind = new Pivotfib();

        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);

            if (i < 1) { continue; }

            double pH = bars[i - 1].High;
            double pL = bars[i - 1].Low;
            double pC = bars[i - 1].Close;

            double expectedPP = (pH + pL + pC) / 3.0;
            double range = pH - pL;

            Assert.Equal(expectedPP, ind.PP, 10);
            Assert.Equal(expectedPP + 0.382 * range, ind.R1, 10);
            Assert.Equal(expectedPP - 0.382 * range, ind.S1, 10);
            Assert.Equal(expectedPP + 0.618 * range, ind.R2, 10);
            Assert.Equal(expectedPP - 0.618 * range, ind.S2, 10);
            Assert.Equal(expectedPP + range, ind.R3, 10);
            Assert.Equal(expectedPP - range, ind.S3, 10);
        }
    }

    // ── Streaming == Batch ──────────────────────────────────────────
    [Fact]
    public void Streaming_Matches_Batch_PP()
    {
        var bars = CreateGbmBars();

        // Streaming
        var ind = new Pivotfib();
        var streamPP = new List<double>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
            streamPP.Add(ind.PP);
        }

        // Batch
        var batchResult = Pivotfib.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            if (double.IsNaN(streamPP[i]))
            {
                Assert.True(double.IsNaN(batchResult[i].Value));
                continue;
            }
            Assert.Equal(streamPP[i], batchResult[i].Value, 10);
        }
    }

    // ── Streaming == Span ───────────────────────────────────────────
    [Fact]
    public void Streaming_Matches_Span_PP()
    {
        var bars = CreateGbmBars();

        // Streaming
        var ind = new Pivotfib();
        var streamPP = new List<double>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
            streamPP.Add(ind.PP);
        }

        // Span
        int len = bars.Count;
        var ppOut = new double[len];
        Pivotfib.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, ppOut);

        for (int i = 0; i < len; i++)
        {
            if (double.IsNaN(streamPP[i]))
            {
                Assert.True(double.IsNaN(ppOut[i]));
                continue;
            }
            Assert.Equal(streamPP[i], ppOut[i], 10);
        }
    }

    // ── Streaming == BatchAll (all 7 levels) ────────────────────────
    [Fact]
    public void Streaming_Matches_BatchAll_AllLevels()
    {
        var bars = CreateGbmBars();

        // Streaming
        var ind = new Pivotfib();
        var sPP = new List<double>(bars.Count);
        var sR1 = new List<double>(bars.Count);
        var sS1 = new List<double>(bars.Count);
        var sR2 = new List<double>(bars.Count);
        var sS2 = new List<double>(bars.Count);
        var sR3 = new List<double>(bars.Count);
        var sS3 = new List<double>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
            sPP.Add(ind.PP);
            sR1.Add(ind.R1);
            sS1.Add(ind.S1);
            sR2.Add(ind.R2);
            sS2.Add(ind.S2);
            sR3.Add(ind.R3);
            sS3.Add(ind.S3);
        }

        // BatchAll
        int len = bars.Count;
        var ppOut = new double[len];
        var r1Out = new double[len];
        var s1Out = new double[len];
        var r2Out = new double[len];
        var s2Out = new double[len];
        var r3Out = new double[len];
        var s3Out = new double[len];

        Pivotfib.BatchAll(
            bars.HighValues, bars.LowValues, bars.CloseValues,
            ppOut, r1Out, s1Out, r2Out, s2Out, r3Out, s3Out);

        for (int i = 0; i < len; i++)
        {
            if (double.IsNaN(sPP[i]))
            {
                Assert.True(double.IsNaN(ppOut[i]));
                continue;
            }
            Assert.Equal(sPP[i], ppOut[i], 10);
            Assert.Equal(sR1[i], r1Out[i], 10);
            Assert.Equal(sS1[i], s1Out[i], 10);
            Assert.Equal(sR2[i], r2Out[i], 10);
            Assert.Equal(sS2[i], s2Out[i], 10);
            Assert.Equal(sR3[i], r3Out[i], 10);
            Assert.Equal(sS3[i], s3Out[i], 10);
        }
    }

    // ── Determinism ─────────────────────────────────────────────────
    [Fact]
    public void Determinism_TwoRuns_IdenticalResults()
    {
        var bars = CreateGbmBars();

        var ind1 = new Pivotfib();
        var ind2 = new Pivotfib();

        for (int i = 0; i < bars.Count; i++)
        {
            ind1.Update(bars[i], isNew: true);
            ind2.Update(bars[i], isNew: true);
        }

        Assert.Equal(ind1.PP, ind2.PP, 15);
        Assert.Equal(ind1.R1, ind2.R1, 15);
        Assert.Equal(ind1.S1, ind2.S1, 15);
        Assert.Equal(ind1.R2, ind2.R2, 15);
        Assert.Equal(ind1.S2, ind2.S2, 15);
        Assert.Equal(ind1.R3, ind2.R3, 15);
        Assert.Equal(ind1.S3, ind2.S3, 15);
    }

    // ── Calculate factory ───────────────────────────────────────────
    [Fact]
    public void Calculate_ReturnsValidResults()
    {
        var bars = CreateGbmBars();
        var (results, indicator) = Pivotfib.Calculate(bars);

        Assert.NotNull(results);
        Assert.NotNull(indicator);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Level Ordering ──────────────────────────────────────────────
    [Fact]
    public void LevelOrdering_S3_LessThan_S2_LessThan_S1_LessThan_PP_LessThan_R1_LessThan_R2_LessThan_R3()
    {
        var bars = CreateGbmBars();
        var ind = new Pivotfib();

        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);

            if (!ind.IsHot) { continue; }

            // For Fibonacci pivots with positive range, strict ordering holds
            if (bars[i - 1].High > bars[i - 1].Low)
            {
                Assert.True(ind.S3 < ind.S2, $"S3 ({ind.S3}) should be < S2 ({ind.S2}) at bar {i}");
                Assert.True(ind.S2 < ind.S1, $"S2 ({ind.S2}) should be < S1 ({ind.S1}) at bar {i}");
                Assert.True(ind.S1 < ind.PP, $"S1 ({ind.S1}) should be < PP ({ind.PP}) at bar {i}");
                Assert.True(ind.PP < ind.R1, $"PP ({ind.PP}) should be < R1 ({ind.R1}) at bar {i}");
                Assert.True(ind.R1 < ind.R2, $"R1 ({ind.R1}) should be < R2 ({ind.R2}) at bar {i}");
                Assert.True(ind.R2 < ind.R3, $"R2 ({ind.R2}) should be < R3 ({ind.R3}) at bar {i}");
            }
        }
    }

    // ── Symmetry ────────────────────────────────────────────────────
    [Fact]
    public void Symmetry_DistancesAboveAndBelowPP_AreEqual()
    {
        var bars = CreateGbmBars();
        var ind = new Pivotfib();

        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);

            if (!ind.IsHot) { continue; }

            // Fibonacci pivots are symmetric: R_n - PP == PP - S_n
            Assert.Equal(ind.R1 - ind.PP, ind.PP - ind.S1, 10);
            Assert.Equal(ind.R2 - ind.PP, ind.PP - ind.S2, 10);
            Assert.Equal(ind.R3 - ind.PP, ind.PP - ind.S3, 10);
        }
    }

}
