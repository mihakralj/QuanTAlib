// TTM_SCALPER Validation Tests - TTM Scalper Alert
// Self-consistency validation (no external library has TTM Scalper implementation)
// Validates: streaming == batch == span, determinism, dual output, useCloses mode

namespace QuanTAlib.Tests;

public sealed class TtmScalperValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // -- Self-Consistency: Streaming == Batch (PivotHigh) -------------------------

    [Fact]
    public void StreamingMatchesBatch_PivotHigh()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new TtmScalper();
        var streamHigh = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHigh[i] = streaming.PivotHigh;
        }

        // Batch
        var batchResults = TtmScalper.Batch(bars);

        for (int i = 2; i < bars.Count; i++)
        {
            if (double.IsNaN(streamHigh[i]))
            {
                Assert.True(double.IsNaN(batchResults[i].Value),
                    $"Mismatch at {i}: streaming=NaN, batch={batchResults[i].Value}");
            }
            else
            {
                Assert.Equal(streamHigh[i], batchResults[i].Value, precision: 10);
            }
        }
    }

    // -- Self-Consistency: Streaming == Span (Both Pivots) ------------------------

    [Fact]
    public void StreamingMatchesSpan_BothPivots()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new TtmScalper();
        var streamHigh = new double[bars.Count];
        var streamLow = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHigh[i] = streaming.PivotHigh;
            streamLow[i] = streaming.PivotLow;
        }

        // Span
        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        TtmScalper.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, spanHigh, spanLow);

        for (int i = 2; i < bars.Count; i++)
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

    // -- UseCloses: Streaming == Span ---------------------------------------------

    [Fact]
    public void UseCloses_StreamingMatchesSpan()
    {
        var bars = CreateGbmBars();

        // Streaming with useCloses=true
        var streaming = new TtmScalper(useCloses: true);
        var streamHigh = new double[bars.Count];
        var streamLow = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamHigh[i] = streaming.PivotHigh;
            streamLow[i] = streaming.PivotLow;
        }

        // Span with useCloses=true
        var spanHigh = new double[bars.Count];
        var spanLow = new double[bars.Count];
        TtmScalper.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            spanHigh, spanLow, useCloses: true);

        for (int i = 2; i < bars.Count; i++)
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

    // -- Determinism ---------------------------------------------------------------

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var ts1 = new TtmScalper();
        var ts2 = new TtmScalper();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = ts1.Update(bars[i], isNew: true);
            _ = ts2.Update(bars[i], isNew: true);
        }

        Assert.Equal(ts1.PivotHigh, ts2.PivotHigh);
        Assert.Equal(ts1.PivotLow, ts2.PivotLow);
    }

    // -- Calculate Returns Valid Indicator -----------------------------------------

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = TtmScalper.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // -- BatchDual Returns Both Pivots -------------------------------------------

    [Fact]
    public void BatchDual_ReturnsBothSeries()
    {
        var bars = CreateGbmBars(count: 100);

        var (highSeries, lowSeries) = TtmScalper.BatchDual(bars);

        Assert.Equal(bars.Count, highSeries.Count);
        Assert.Equal(bars.Count, lowSeries.Count);

        // With 100 bars of GBM data, should detect both pivot types
        bool hasHigh = false;
        bool hasLow = false;
        for (int i = 0; i < highSeries.Count; i++)
        {
            if (double.IsFinite(highSeries[i].Value))
            {
                hasHigh = true;
            }
            if (double.IsFinite(lowSeries[i].Value))
            {
                hasLow = true;
            }
        }

        Assert.True(hasHigh, "Should detect at least one pivot high in 100 bars");
        Assert.True(hasLow, "Should detect at least one pivot low in 100 bars");
    }

    // -- UseCloses produces different results than default -------------------------

    [Fact]
    public void UseCloses_ProducesDifferentResults()
    {
        var bars = CreateGbmBars(count: 200);

        var defaultMode = new TtmScalper(useCloses: false);
        var closesMode = new TtmScalper(useCloses: true);

        int differenceCount = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            _ = defaultMode.Update(bars[i], isNew: true);
            _ = closesMode.Update(bars[i], isNew: true);

            bool defaultHasHigh = double.IsFinite(defaultMode.PivotHigh);
            bool closesHasHigh = double.IsFinite(closesMode.PivotHigh);
            if (defaultHasHigh != closesHasHigh)
            {
                differenceCount++;
            }
        }

        // With GBM data, high/low and close patterns should diverge at least once
        Assert.True(differenceCount > 0, "UseCloses should produce different pivot detection than default mode");
    }
}
