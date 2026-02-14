// CKSTOP Validation Tests - Chande Kroll Stop
// No external library implements CKSTOP, so validation uses self-consistency checks.

namespace QuanTAlib.Tests;

public sealed class CkstopValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // ── Self-Consistency: Streaming == Batch ──────────────────────────────

    [Fact]
    public void StreamingMatchesBatch_StopLong()
    {
        var bars = CreateGbmBars();
        int atrPeriod = 10;
        double multiplier = 1.0;
        int stopPeriod = 9;

        // Streaming
        var streaming = new Ckstop(atrPeriod, multiplier, stopPeriod);
        var streamStopLong = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamStopLong[i] = streaming.StopLong;
        }

        // Batch
        var batchResults = Ckstop.Batch(bars, atrPeriod, multiplier, stopPeriod);

        int warmup = atrPeriod + stopPeriod;
        for (int i = warmup; i < bars.Count; i++)
        {
            Assert.Equal(streamStopLong[i], batchResults[i].Value, precision: 10);
        }
    }

    // ── Self-Consistency: Streaming == Span ───────────────────────────────

    [Fact]
    public void StreamingMatchesSpan_StopLong()
    {
        var bars = CreateGbmBars();
        int atrPeriod = 10;
        double multiplier = 1.0;
        int stopPeriod = 9;

        // Streaming
        var streaming = new Ckstop(atrPeriod, multiplier, stopPeriod);
        var streamStopLong = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamStopLong[i] = streaming.StopLong;
        }

        // Span
        var spanOutput = new double[bars.Count];
        Ckstop.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues,
            spanOutput, atrPeriod, multiplier, stopPeriod);

        int warmup = atrPeriod + stopPeriod;
        for (int i = warmup; i < bars.Count; i++)
        {
            Assert.Equal(streamStopLong[i], spanOutput[i], precision: 10);
        }
    }

    // ── Directional Correctness ──────────────────────────────────────────

    [Fact]
    public void HigherMultiplier_NarrowsStopGap()
    {
        var bars = CreateGbmBars(count: 100);

        var narrow = new Ckstop(atrPeriod: 10, multiplier: 1.0, stopPeriod: 9);
        var wide = new Ckstop(atrPeriod: 10, multiplier: 3.0, stopPeriod: 9);

        for (int i = 0; i < bars.Count; i++)
        {
            _ = narrow.Update(bars[i], isNew: true);
            _ = wide.Update(bars[i], isNew: true);
        }

        // Higher multiplier: StopLong = LowestLow + q*ATR → goes UP
        //                    StopShort = HighestHigh - q*ATR → goes DOWN
        // The gap (StopShort - StopLong) narrows with higher multiplier.
        double narrowGap = narrow.StopShort - narrow.StopLong;
        double wideGap = wide.StopShort - wide.StopLong;
        Assert.True(wideGap < narrowGap,
            $"Wide gap ({wideGap}) should be < narrow gap ({narrowGap})");
    }

    // ── Determinism ──────────────────────────────────────────────────────

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var ck1 = new Ckstop(atrPeriod: 10, multiplier: 1.0, stopPeriod: 9);
        var ck2 = new Ckstop(atrPeriod: 10, multiplier: 1.0, stopPeriod: 9);

        for (int i = 0; i < bars.Count; i++)
        {
            _ = ck1.Update(bars[i], isNew: true);
            _ = ck2.Update(bars[i], isNew: true);
        }

        Assert.Equal(ck1.StopLong, ck2.StopLong);
        Assert.Equal(ck1.StopShort, ck2.StopShort);
    }

    // ── StopLong and StopShort Finite After Warmup ───────────────────────

    [Fact]
    public void AfterWarmup_BothStopsAreFinite()
    {
        var bars = CreateGbmBars(count: 100);
        var ck = new Ckstop(atrPeriod: 10, multiplier: 1.0, stopPeriod: 9);

        for (int i = 0; i < bars.Count; i++)
        {
            _ = ck.Update(bars[i], isNew: true);

            if (ck.IsHot)
            {
                Assert.True(double.IsFinite(ck.StopLong), $"StopLong should be finite at bar {i}");
                Assert.True(double.IsFinite(ck.StopShort), $"StopShort should be finite at bar {i}");
            }
        }
    }

    // ── Different Seeds Produce Different Results ─────────────────────────

    [Fact]
    public void DifferentSeeds_ProduceDifferentStops()
    {
        var bars1 = CreateGbmBars(count: 100, seed: 42);
        var bars2 = CreateGbmBars(count: 100, seed: 99);

        var ck1 = new Ckstop(atrPeriod: 10, multiplier: 1.0, stopPeriod: 9);
        var ck2 = new Ckstop(atrPeriod: 10, multiplier: 1.0, stopPeriod: 9);

        for (int i = 0; i < 100; i++)
        {
            _ = ck1.Update(bars1[i], isNew: true);
            _ = ck2.Update(bars2[i], isNew: true);
        }

        // Very unlikely to be equal with different random data
        Assert.NotEqual(ck1.StopLong, ck2.StopLong);
    }

    // ── Calculate Returns Valid Indicator ─────────────────────────────────

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = Ckstop.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.StopLong));
        Assert.True(double.IsFinite(indicator.StopShort));
    }
}
