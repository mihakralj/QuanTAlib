// CHANDELIER Validation Tests - Chandelier Exit
// Cross-validated against Skender.Stock.Indicators ToChandelier()

using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public sealed class ChandelierValidationTests
{
    private static TBarSeries CreateGbmBars(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // ── Cross-library: Skender Long ──────────────────────────────────────

    [Fact]
    public void StreamingMatchesSkender_ExitLong()
    {
        var _data = new ValidationTestData();
        int period = 22;
        double multiplier = 3.0;

        // QuanTAlib streaming
        var ch = new Chandelier(period, multiplier);
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            _ = ch.Update(_data.Bars[i], isNew: true);
        }

        // Skender
        var skenderResults = _data.SkenderQuotes
            .GetChandelier(period, multiplier, ChandelierType.Long)
            .ToList();

        // QuanTAlib streaming values
        var ourValues = new double[_data.Bars.Count];
        var streaming2 = new Chandelier(period, multiplier);
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            _ = streaming2.Update(_data.Bars[i], isNew: true);
            ourValues[i] = streaming2.ExitLong;
        }

        // Compare warm values
        int matched = 0;
        for (int i = period; i < skenderResults.Count && i < _data.Bars.Count; i++)
        {
            if (skenderResults[i].ChandelierExit.HasValue && double.IsFinite(ourValues[i]))
            {
                Assert.Equal(
                    skenderResults[i].ChandelierExit!.Value,
                    ourValues[i],
                    precision: 8);
                matched++;
            }
        }

        Assert.True(matched > 0, "Should have matched at least one warm value");
        _data.Dispose();
    }

    // ── Cross-library: Skender Short ─────────────────────────────────────

    [Fact]
    public void StreamingMatchesSkender_ExitShort()
    {
        var _data = new ValidationTestData();
        int period = 22;
        double multiplier = 3.0;

        // Skender Short
        var skenderResults = _data.SkenderQuotes
            .GetChandelier(period, multiplier, ChandelierType.Short)
            .ToList();

        // QuanTAlib streaming
        var ch = new Chandelier(period, multiplier);
        var ourValues = new double[_data.Bars.Count];
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            _ = ch.Update(_data.Bars[i], isNew: true);
            ourValues[i] = ch.ExitShort;
        }

        int matched = 0;
        for (int i = period; i < skenderResults.Count && i < _data.Bars.Count; i++)
        {
            if (skenderResults[i].ChandelierExit.HasValue && double.IsFinite(ourValues[i]))
            {
                Assert.Equal(
                    skenderResults[i].ChandelierExit!.Value,
                    ourValues[i],
                    precision: 8);
                matched++;
            }
        }

        Assert.True(matched > 0, "Should have matched at least one warm value");
        _data.Dispose();
    }

    // ── Self-Consistency: Streaming == Batch ──────────────────────────────

    [Fact]
    public void StreamingMatchesBatch_ExitLong()
    {
        var bars = CreateGbmBars();
        int period = 22;
        double multiplier = 3.0;

        // Streaming
        var streaming = new Chandelier(period, multiplier);
        var streamExitLong = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamExitLong[i] = streaming.ExitLong;
        }

        // Batch
        var batchResults = Chandelier.Batch(bars, period, multiplier);

        for (int i = period; i < bars.Count; i++)
        {
            Assert.Equal(streamExitLong[i], batchResults[i].Value, precision: 10);
        }
    }

    // ── Self-Consistency: Streaming == Span ───────────────────────────────

    [Fact]
    public void StreamingMatchesSpan_ExitLong()
    {
        var bars = CreateGbmBars();
        int period = 22;
        double multiplier = 3.0;

        // Streaming
        var streaming = new Chandelier(period, multiplier);
        var streamExitLong = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamExitLong[i] = streaming.ExitLong;
        }

        // Span
        var spanOutput = new double[bars.Count];
        Chandelier.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues,
            spanOutput, period, multiplier);

        for (int i = period; i < bars.Count; i++)
        {
            Assert.Equal(streamExitLong[i], spanOutput[i], precision: 10);
        }
    }

    // ── Directional Correctness ──────────────────────────────────────────

    [Fact]
    public void HigherMultiplier_WidensExitGap()
    {
        var bars = CreateGbmBars(count: 100);

        var narrow = new Chandelier(period: 22, multiplier: 1.0);
        var wide = new Chandelier(period: 22, multiplier: 5.0);

        for (int i = 0; i < bars.Count; i++)
        {
            _ = narrow.Update(bars[i], isNew: true);
            _ = wide.Update(bars[i], isNew: true);
        }

        // Higher multiplier: ExitLong = HH - mult*ATR → goes DOWN (wider from HH)
        //                    ExitShort = LL + mult*ATR → goes UP (wider from LL)
        // So ExitLong with high mult should be lower than with low mult
        Assert.True(wide.ExitLong < narrow.ExitLong,
            $"Wide ExitLong ({wide.ExitLong}) should be < narrow ExitLong ({narrow.ExitLong})");
    }

    // ── Determinism ──────────────────────────────────────────────────────

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var bars = CreateGbmBars(count: 200, seed: 123);

        var ch1 = new Chandelier(period: 22, multiplier: 3.0);
        var ch2 = new Chandelier(period: 22, multiplier: 3.0);

        for (int i = 0; i < bars.Count; i++)
        {
            _ = ch1.Update(bars[i], isNew: true);
            _ = ch2.Update(bars[i], isNew: true);
        }

        Assert.Equal(ch1.ExitLong, ch2.ExitLong);
        Assert.Equal(ch1.ExitShort, ch2.ExitShort);
    }

    // ── Calculate Returns Valid Indicator ─────────────────────────────────

    [Fact]
    public void Calculate_ReturnsValidIndicatorAndResults()
    {
        var bars = CreateGbmBars(count: 100);

        var (results, indicator) = Chandelier.Calculate(bars);

        Assert.NotNull(results);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.ExitLong));
        Assert.True(double.IsFinite(indicator.ExitShort));
    }
}
