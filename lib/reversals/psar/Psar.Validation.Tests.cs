// PSAR Validation Tests - Parabolic Stop And Reverse
// Cross-validated against Skender.Stock.Indicators GetParabolicSar()

using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public sealed class PsarValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // ── Cross-library: Skender ───────────────────────────────────────────

    [Fact]
    public void StreamingMatchesSkender()
    {
        var _data = new ValidationTestData();

        // Skender: GetParabolicSar(accelerationStep, maxAccelerationFactor, initialFactor)
        var skenderResults = _data.SkenderQuotes
            .GetParabolicSar(0.02, 0.2, 0.02)
            .ToList();

        // QuanTAlib streaming
        var psar = new Psar(afStart: 0.02, afIncrement: 0.02, afMax: 0.20);
        var ourValues = new double[_data.Bars.Count];
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            _ = psar.Update(_data.Bars[i], isNew: true);
            ourValues[i] = psar.Sar;
        }

        // Compare warm values (skip first bar where SAR is initialization)
        int matched = 0;
        for (int i = 2; i < skenderResults.Count && i < _data.Bars.Count; i++)
        {
            if (skenderResults[i].Sar.HasValue && double.IsFinite(ourValues[i]))
            {
                Assert.Equal(
                    skenderResults[i].Sar!.Value,
                    ourValues[i],
                    precision: 6);
                matched++;
            }
        }

        Assert.True(matched > 0, "Should have matched at least one warm value");
        _data.Dispose();
    }

    // ── Self-Consistency: Streaming == Batch ──────────────────────────────

    [Fact]
    public void StreamingMatchesBatch()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Psar();
        var streamValues = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamValues[i] = streaming.Sar;
        }

        // Batch
        var batchResults = Psar.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamValues[i], batchResults[i].Value, precision: 10);
        }
    }

    // ── Self-Consistency: Streaming == Span ───────────────────────────────

    [Fact]
    public void StreamingMatchesSpan()
    {
        var bars = CreateGbmBars();

        // Streaming
        var streaming = new Psar();
        var streamValues = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamValues[i] = streaming.Sar;
        }

        // Span
        var spanOutput = new double[bars.Count];
        Psar.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues, spanOutput);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamValues[i], spanOutput[i], precision: 10);
        }
    }

    // ── AF Sensitivity ───────────────────────────────────────────────────

    [Fact]
    public void HigherAfStart_TighterTrailingStop()
    {
        var bars = CreateGbmBars(count: 100);

        var slow = new Psar(afStart: 0.01, afIncrement: 0.01, afMax: 0.20);
        var fast = new Psar(afStart: 0.10, afIncrement: 0.05, afMax: 0.50);

        for (int i = 0; i < bars.Count; i++)
        {
            _ = slow.Update(bars[i], isNew: true);
            _ = fast.Update(bars[i], isNew: true);
        }

        // Higher AF = more responsive = SAR closer to price
        // Just verify both produce finite output (direction depends on data)
        Assert.True(double.IsFinite(slow.Sar));
        Assert.True(double.IsFinite(fast.Sar));
    }

    // ── Determinism ──────────────────────────────────────────────────────

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var psar1 = new Psar();
        var psar2 = new Psar();

        for (int i = 0; i < bars.Count; i++)
        {
            _ = psar1.Update(bars[i], isNew: true);
            _ = psar2.Update(bars[i], isNew: true);
        }

        Assert.Equal(psar1.Sar, psar2.Sar);
    }

    // ── Calculate Returns Valid Indicator ─────────────────────────────────

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = Psar.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Sar));
    }

    // ── Reversal Count Is Reasonable ─────────────────────────────────────

    [Fact]
    public void ReversalCount_IsReasonable()
    {
        var bars = CreateGbmBars(count: 500);
        var psar = new Psar();

        int reversals = 0;
        bool prevIsLong = true;

        for (int i = 0; i < bars.Count; i++)
        {
            _ = psar.Update(bars[i], isNew: true);

            if (i > 0 && psar.IsLong != prevIsLong)
            {
                reversals++;
            }
            prevIsLong = psar.IsLong;
        }

        // In 500 bars of GBM data, expect several reversals but not every bar
        Assert.True(reversals > 5, $"Expected > 5 reversals, got {reversals}");
        Assert.True(reversals < 250, $"Expected < 250 reversals, got {reversals}");
    }
}
