
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
// PIVOTCAM Validation Tests - Camarilla Pivot Points
// Self-consistency validation across all API modes.
//
// Note: No external library (Skender, TA-Lib, Tulip, Ooples) implements
// Camarilla Pivot Points. Validation focuses on mathematical correctness
// and mode consistency.

namespace QuanTAlib.Tests;

public sealed class PivotcamValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // -- Mathematical Correctness -------------------------------------------------

    [Fact]
    public void MathCorrectness_PP_EqualsHLC_Over3()
    {
        var bars = CreateGbmBars(count: 100);
        var p = new Pivotcam();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p.Update(bars[i], isNew: true);

            if (i >= 1)
            {
                double prevH = bars[i - 1].High;
                double prevL = bars[i - 1].Low;
                double prevC = bars[i - 1].Close;
                double expectedPP = (prevH + prevL + prevC) / 3.0;

                Assert.Equal(expectedPP, p.PP, precision: 10);
            }
        }
    }

    [Fact]
    public void MathCorrectness_AllLevels_MatchCamarillaFormula()
    {
        var bars = CreateGbmBars(count: 100);
        var p = new Pivotcam();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p.Update(bars[i], isNew: true);

            if (i >= 1)
            {
                double pH = bars[i - 1].High;
                double pL = bars[i - 1].Low;
                double pC = bars[i - 1].Close;
                double pp = (pH + pL + pC) / 3.0;
                double range = pH - pL;

                Assert.Equal(pp, p.PP, precision: 10);
                Assert.Equal(pC + range * 1.0833 / 12.0, p.R1, precision: 4);
                Assert.Equal(pC - range * 1.0833 / 12.0, p.S1, precision: 4);
                Assert.Equal(pC + range * 1.1666 / 12.0, p.R2, precision: 4);
                Assert.Equal(pC - range * 1.1666 / 12.0, p.S2, precision: 4);
                Assert.Equal(pC + range * 1.25 / 12.0, p.R3, precision: 4);
                Assert.Equal(pC - range * 1.25 / 12.0, p.S3, precision: 4);
                Assert.Equal(pC + range * 1.5 / 12.0, p.R4, precision: 4);
                Assert.Equal(pC - range * 1.5 / 12.0, p.S4, precision: 4);
            }
        }
    }

    // -- Self-Consistency: Streaming == Batch --------------------------------------

    [Fact]
    public void StreamingMatchesBatch_PP()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Pivotcam();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Batch
        var batchResults = Pivotcam.Batch(bars);

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
        var streaming = new Pivotcam();
        var streamPP = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamPP[i] = streaming.PP;
        }

        // Span
        var spanPP = new double[bars.Count];
        Pivotcam.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, spanPP);

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

    // -- Self-Consistency: Streaming == BatchAll (all 9 levels) --------------------

    [Fact]
    public void StreamingMatchesBatchAll_AllLevels()
    {
        var bars = CreateGbmBars(count: 300);

        // Streaming
        var streaming = new Pivotcam();
        var sPP = new double[bars.Count];
        var sR1 = new double[bars.Count];
        var sS1 = new double[bars.Count];
        var sR2 = new double[bars.Count];
        var sS2 = new double[bars.Count];
        var sR3 = new double[bars.Count];
        var sS3 = new double[bars.Count];
        var sR4 = new double[bars.Count];
        var sS4 = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            sPP[i] = streaming.PP;
            sR1[i] = streaming.R1;
            sS1[i] = streaming.S1;
            sR2[i] = streaming.R2;
            sS2[i] = streaming.S2;
            sR3[i] = streaming.R3;
            sS3[i] = streaming.S3;
            sR4[i] = streaming.R4;
            sS4[i] = streaming.S4;
        }

        // BatchAll
        var bPP = new double[bars.Count];
        var bR1 = new double[bars.Count];
        var bS1 = new double[bars.Count];
        var bR2 = new double[bars.Count];
        var bS2 = new double[bars.Count];
        var bR3 = new double[bars.Count];
        var bS3 = new double[bars.Count];
        var bR4 = new double[bars.Count];
        var bS4 = new double[bars.Count];

        Pivotcam.BatchAll(bars.HighValues, bars.LowValues, bars.CloseValues,
            bPP, bR1, bS1, bR2, bS2, bR3, bS3, bR4, bS4);

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
            Assert.Equal(sR2[i], bR2[i], precision: 10);
            Assert.Equal(sS2[i], bS2[i], precision: 10);
            Assert.Equal(sR3[i], bR3[i], precision: 10);
            Assert.Equal(sS3[i], bS3[i], precision: 10);
            Assert.Equal(sR4[i], bR4[i], precision: 10);
            Assert.Equal(sS4[i], bS4[i], precision: 10);
        }
    }

    // -- Determinism ---------------------------------------------------------------

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var p1 = new Pivotcam();
        var p2 = new Pivotcam();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p1.Update(bars[i], isNew: true);
            _ = p2.Update(bars[i], isNew: true);
        }

        Assert.Equal(p1.PP, p2.PP);
        Assert.Equal(p1.R1, p2.R1);
        Assert.Equal(p1.S1, p2.S1);
        Assert.Equal(p1.R2, p2.R2);
        Assert.Equal(p1.S2, p2.S2);
        Assert.Equal(p1.R3, p2.R3);
        Assert.Equal(p1.S3, p2.S3);
        Assert.Equal(p1.R4, p2.R4);
        Assert.Equal(p1.S4, p2.S4);
    }

    // -- Calculate Returns Valid Indicator -----------------------------------------

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = Pivotcam.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // -- Level Ordering Invariant --------------------------------------------------

    [Fact]
    public void AllBars_SupportResistanceLevelsOrdered()
    {
        // Camarilla: S4 < S3 < S2 < S1 < Close-based < R1 < R2 < R3 < R4
        // Note: PP is based on HLC/3 and may be above or below close,
        // but resistance levels are always ordered R1 < R2 < R3 < R4
        // and support levels are always ordered S4 < S3 < S2 < S1
        var bars = CreateGbmBars(count: 200);
        var p = new Pivotcam();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = p.Update(bars[i], isNew: true);

            if (p.IsHot)
            {
                Assert.True(p.S4 <= p.S3, $"S4 > S3 at bar {i}");
                Assert.True(p.S3 <= p.S2, $"S3 > S2 at bar {i}");
                Assert.True(p.S2 <= p.S1, $"S2 > S1 at bar {i}");
                Assert.True(p.R1 <= p.R2, $"R1 > R2 at bar {i}");
                Assert.True(p.R2 <= p.R3, $"R2 > R3 at bar {i}");
                Assert.True(p.R3 <= p.R4, $"R3 > R4 at bar {i}");
            }
        }
    }

    [Fact(Skip = "Ooples pivot indicators group by calendar day — 500×1-min bars yields ~3 daily pivots. Requires daily OHLCV input; not comparable with intraday GBM data.")]
    public void Pivotcam_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateCamarillaPivotPoints();
        var values = result.OutputValues.Values.First();
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}