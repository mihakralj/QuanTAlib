
// PIVOTDEM Validation Tests - DeMark Pivot Points
// Self-consistency validation across all API modes.
//
// Note: No external libraries (Skender, TA-Lib, Tulip, Ooples) implement
// DeMark pivot points. Validation focuses on mathematical correctness,
// conditional logic verification, and mode consistency.

namespace QuanTAlib.Tests;

public sealed class PivotdemValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // -- Mathematical Correctness -------------------------------------------------

    [Fact]
    public void MathCorrectness_PP_MatchesConditionalFormula()
    {
        var bars = CreateGbmBars(count: 100);
        var p = new Pivotdem();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p.Update(bars[i], isNew: true);

            if (i >= 1) // Need previous bar
            {
                double pO = bars[i - 1].Open;
                double pH = bars[i - 1].High;
                double pL = bars[i - 1].Low;
                double pC = bars[i - 1].Close;

                double x;
                if (pC < pO) { x = pH + 2.0 * pL + pC; }
                else if (pC > pO) { x = 2.0 * pH + pL + pC; }
                else { x = pH + pL + 2.0 * pC; }

                double expectedPP = x * 0.25;
                Assert.Equal(expectedPP, p.PP, precision: 10);
            }
        }
    }

    [Fact]
    public void MathCorrectness_AllLevels_MatchFormula()
    {
        var bars = CreateGbmBars(count: 100);
        var p = new Pivotdem();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p.Update(bars[i], isNew: true);

            if (i >= 1)
            {
                double pO = bars[i - 1].Open;
                double pH = bars[i - 1].High;
                double pL = bars[i - 1].Low;
                double pC = bars[i - 1].Close;

                double x;
                if (pC < pO) { x = pH + 2.0 * pL + pC; }
                else if (pC > pO) { x = 2.0 * pH + pL + pC; }
                else { x = pH + pL + 2.0 * pC; }

                double halfX = x * 0.5;
                Assert.Equal(x * 0.25, p.PP, precision: 10);
                Assert.Equal(halfX - pL, p.R1, precision: 10);
                Assert.Equal(halfX - pH, p.S1, precision: 10);
            }
        }
    }

    // -- Self-Consistency: Streaming == Batch --------------------------------------

    [Fact]
    public void StreamingMatchesBatch_PP()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Pivotdem();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Batch
        var batchResults = Pivotdem.Batch(bars);

        for (int i = 1; i < bars.Count; i++)
        {
            if (double.IsNaN(streamPP[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value),
                    $"Mismatch at {i}: streaming=NaN, batch={batchResults[i].Value}");
            }
            else
            {
                Assert.Equal(streamPP[i], batchResults[i].Value, precision: 10);
            }
        }
    }

    // -- Self-Consistency: Streaming == Span ---------------------------------------

    [Fact]
    public void StreamingMatchesSpan_PP()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Pivotdem();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Span
        var spanPP = new double[bars.Count];
        Pivotdem.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues, spanPP);

        for (int i = 1; i < bars.Count; i++)
        {
            if (double.IsNaN(streamPP[i]))
            {
                Assert.True(double.IsNaN(spanPP[i]));
            }
            else
            {
                Assert.Equal(streamPP[i], spanPP[i], precision: 10);
            }
        }
    }

    // -- Self-Consistency: Streaming == BatchAll (all 3 levels) --------------------

    [Fact]
    public void StreamingMatchesBatchAll_AllLevels()
    {
        var bars = CreateGbmBars(count: 300);

        // Streaming
        var streaming = new Pivotdem();
        var sPP = new double[bars.Count];
        var sR1 = new double[bars.Count];
        var sS1 = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            sPP[i] = streaming.PP;
            sR1[i] = streaming.R1;
            sS1[i] = streaming.S1;
        }

        // BatchAll
        var bPP = new double[bars.Count];
        var bR1 = new double[bars.Count];
        var bS1 = new double[bars.Count];

        Pivotdem.BatchAll(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues,
            bPP, bR1, bS1);

        for (int i = 1; i < bars.Count; i++)
        {
            if (double.IsNaN(sPP[i]))
            {
                Assert.True(double.IsNaN(bPP[i]));
                continue;
            }

            Assert.Equal(sPP[i], bPP[i], precision: 10);
            Assert.Equal(sR1[i], bR1[i], precision: 10);
            Assert.Equal(sS1[i], bS1[i], precision: 10);
        }
    }

    // -- Determinism ---------------------------------------------------------------

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var p1 = new Pivotdem();
        var p2 = new Pivotdem();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p1.Update(bars[i], isNew: true);
            _ = p2.Update(bars[i], isNew: true);
        }

        Assert.Equal(p1.PP, p2.PP);
        Assert.Equal(p1.R1, p2.R1);
        Assert.Equal(p1.S1, p2.S1);
    }

    // -- Calculate Returns Valid Indicator -----------------------------------------

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = Pivotdem.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // -- Level Ordering Invariant --------------------------------------------------

    [Fact]
    public void AllBars_LevelsOrdered_S1_PP_R1()
    {
        var bars = CreateGbmBars(count: 200);
        var p = new Pivotdem();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p.Update(bars[i], isNew: true);

            if (p.IsHot)
            {
                Assert.True(p.S1 <= p.PP, $"S1 > PP at bar {i}");
                Assert.True(p.PP <= p.R1, $"PP > R1 at bar {i}");
            }
        }
    }

}
