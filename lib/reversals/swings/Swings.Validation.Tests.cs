// SWINGS Validation Tests - Swing High/Low Detection
// No external library validation available (Skender/TA-Lib/Tulip/Ooples don't have configurable swings)
// Tests focus on mathematical correctness and self-consistency

namespace QuanTAlib.Tests;

public sealed class SwingsValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // -- Self-Consistency: Streaming == Batch (SwingHigh) --------------------------

    [Fact]
    public void StreamingMatchesBatch_SwingHigh()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Swings(lookback: 3);
        var streamHigh = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHigh[i] = streaming.SwingHigh;
        }

        // Batch
        var batchResults = Swings.Batch(bars, lookback: 3);

        int warmup = 6; // windowSize - 1
        for (int i = warmup; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHigh[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value), $"Mismatch at {i}: streaming=NaN, batch={batchResults[i].Value}");
            }
            else
            {
                Assert.Equal(streamHigh[i], batchResults[i].Value, precision: 10);
            }
        }
    }

    // -- Self-Consistency: Streaming == Span (Both outputs) -----------------------

    [Fact]
    public void StreamingMatchesSpan_BothSwings()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Swings(lookback: 3);
        var streamHigh = new double[bars.Count];
        var streamLow = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHigh[i] = streaming.SwingHigh;
            streamLow[i] = streaming.SwingLow;
        }

        // Span
        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        Swings.Batch(bars.HighValues, bars.LowValues, spanHigh, spanLow, lookback: 3);

        for (int i = 6; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHigh[i]))
            {
                Assert.True(double.IsNaN(spanHigh[i]));
            }
            else
            {
                Assert.Equal(streamHigh[i], spanHigh[i], precision: 10);
            }

            if (double.IsNaN(streamLow[i]))
            {
                Assert.True(double.IsNaN(spanLow[i]));
            }
            else
            {
                Assert.Equal(streamLow[i], spanLow[i], precision: 10);
            }
        }
    }

    // -- Mathematical correctness: swing highs are actual local maxima -------------

    [Fact]
    public void SwingHigh_IsActualLocalMaximum()
    {
        var bars = CreateGbmBars(count: 200, seed: 77);

        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        int lookback = 3;
        Swings.Batch(bars.HighValues, bars.LowValues, spanHigh, spanLow, lookback);

        int windowSize = 2 * lookback + 1;
        for (int i = windowSize - 1; i < bars.Count; i++)
        {
            if (double.IsNaN(spanHigh[i]))
            {
                continue;
            }

            // The swing high value should be the center bar's high
            int center = i - lookback;
            double centerHigh = bars.HighValues[center];
            Assert.Equal(centerHigh, spanHigh[i], precision: 10);

            // Verify it's strictly greater than all neighbors
            for (int j = center - lookback; j <= center + lookback; j++)
            {
                if (j != center)
                {
                    Assert.True(centerHigh > bars.HighValues[j],
                        $"Bar {center} high={centerHigh} should be > bar {j} high={bars.HighValues[j]}");
                }
            }
        }
    }

    // -- Mathematical correctness: swing lows are actual local minima --------------

    [Fact]
    public void SwingLow_IsActualLocalMinimum()
    {
        var bars = CreateGbmBars(count: 200, seed: 77);

        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        int lookback = 3;
        Swings.Batch(bars.HighValues, bars.LowValues, spanHigh, spanLow, lookback);

        int windowSize = 2 * lookback + 1;
        for (int i = windowSize - 1; i < bars.Count; i++)
        {
            if (double.IsNaN(spanLow[i]))
            {
                continue;
            }

            // The swing low value should be the center bar's low
            int center = i - lookback;
            double centerLow = bars.LowValues[center];
            Assert.Equal(centerLow, spanLow[i], precision: 10);

            // Verify it's strictly less than all neighbors
            for (int j = center - lookback; j <= center + lookback; j++)
            {
                if (j != center)
                {
                    Assert.True(centerLow < bars.LowValues[j],
                        $"Bar {center} low={centerLow} should be < bar {j} low={bars.LowValues[j]}");
                }
            }
        }
    }

    // -- Determinism ---------------------------------------------------------------

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var sw1 = new Swings(lookback: 4);
        var sw2 = new Swings(lookback: 4);

        for (int i = 0; i < bars.Count; i++)
        {
            _ = sw1.Update(bars[i], isNew: true);
            _ = sw2.Update(bars[i], isNew: true);
        }

        Assert.Equal(sw1.SwingHigh, sw2.SwingHigh);
        Assert.Equal(sw1.SwingLow, sw2.SwingLow);
        Assert.Equal(sw1.LastSwingHigh, sw2.LastSwingHigh);
        Assert.Equal(sw1.LastSwingLow, sw2.LastSwingLow);
    }

    // -- Calculate Returns Valid Indicator -----------------------------------------

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = Swings.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // -- BatchDual Returns Both Swings -------------------------------------------

    [Fact]
    public void BatchDual_ReturnsBothSeries()
    {
        var bars = CreateGbmBars(count: 100);

        var (swingHighs, swingLows) = Swings.BatchDual(bars, lookback: 3);

        Assert.Equal(bars.Count, swingHighs.Count);
        Assert.Equal(bars.Count, swingLows.Count);

        // At least some swings should be detected in 100 bars
        bool hasHigh = false;
        bool hasLow = false;
        for (int i = 0; i < swingHighs.Count; i++)
        {
            if (double.IsFinite(swingHighs[i].Value))
            {
                hasHigh = true;
            }
            if (double.IsFinite(swingLows[i].Value))
            {
                hasLow = true;
            }
        }

        Assert.True(hasHigh, "Should detect at least one swing high in 100 bars");
        Assert.True(hasLow, "Should detect at least one swing low in 100 bars");
    }

    // -- Different lookback periods produce different results ----------------------

    [Fact]
    public void DifferentLookbacks_ProduceDifferentResults()
    {
        var bars = CreateGbmBars(count: 200, seed: 55);

        var sw2 = new Swings(lookback: 2);
        var sw5 = new Swings(lookback: 5);

        int swingCount2 = 0;
        int swingCount5 = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            _ = sw2.Update(bars[i], isNew: true);
            _ = sw5.Update(bars[i], isNew: true);

            if (double.IsFinite(sw2.SwingHigh))
            {
                swingCount2++;
            }
            if (double.IsFinite(sw5.SwingHigh))
            {
                swingCount5++;
            }
        }

        // Larger lookback should generally yield fewer swings
        Assert.True(swingCount2 > swingCount5 || swingCount5 == 0,
            $"lookback=2 detected {swingCount2} swings, lookback=5 detected {swingCount5} — shorter lookback should detect more");
    }
}
