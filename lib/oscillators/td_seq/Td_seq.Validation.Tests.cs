using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// TD_SEQ Validation Tests — self-consistency only (no external library equivalent).
/// Validates: streaming == batch, determinism, NaN safety, direction reversal logic.
/// </summary>
public sealed class TdSeqValidationTests
{
    private static TBar[] MakeBars(int count, int seed = 42)
    {
        var gbm = new GBM(100.0, 0.02, 0.1, seed: seed);
        var tbarSeries = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var bars = new TBar[count];
        for (int i = 0; i < count; i++)
        {
            bars[i] = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                tbarSeries.Close.Values[i],
                tbarSeries.High.Values[i],
                tbarSeries.Low.Values[i],
                tbarSeries.Close.Values[i],
                1000);
        }

        return bars;
    }

    // ─── Self-consistency: streaming == batch ───

    [Fact]
    public void Streaming_EqualsBatch_Period4()
    {
        var bars = MakeBars(500);
        var barSeries = new TBarSeries();
        foreach (var b in bars) { barSeries.Add(b); }

        // Streaming
        var streaming = new TdSeq(4);
        var streamResults = new double[bars.Length];
        for (int i = 0; i < bars.Length; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        // Batch via Calculate
        TSeries batchResults = TdSeq.Calculate(barSeries, 4);

        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i]);
        }
    }

    [Fact]
    public void Streaming_EqualsBatch_Period2()
    {
        var bars = MakeBars(200, seed: 13);
        var barSeries = new TBarSeries();
        foreach (var b in bars) { barSeries.Add(b); }

        var streaming = new TdSeq(2);
        var streamResults = new double[bars.Length];
        for (int i = 0; i < bars.Length; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        TSeries batchResults = TdSeq.Calculate(barSeries, 2);

        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i]);
        }
    }

    // ─── Determinism: same input → same output ───

    [Fact]
    public void Determinism_SameSeed_SameResults()
    {
        var bars1 = MakeBars(100, seed: 99);
        var bars2 = MakeBars(100, seed: 99);

        var td1 = new TdSeq(4);
        var td2 = new TdSeq(4);

        for (int i = 0; i < bars1.Length; i++)
        {
            double v1 = td1.Update(bars1[i]).Value;
            double v2 = td2.Update(bars2[i]).Value;
            Assert.Equal(v1, v2);
        }
    }

    // ─── Known-value spot check ───

    [Fact]
    public void SellSetup_PureRising_CountsCorrectly()
    {
        // Pure monotone rising: bars 0-3 prime, bars 4-12 each qualify as sell setup
        // After 9 qualifying bars the setup count clamps to 9
        var td = new TdSeq(4);
        int maxSetup = 0;
        for (int i = 0; i < 20; i++)
        {
            double p = 100.0 + i;
            td.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p + 2, p - 2, p, 1000));
            if (td.Setup > maxSetup) { maxSetup = td.Setup; }
        }

        Assert.Equal(9, maxSetup);
    }

    [Fact]
    public void BuySetup_PureFalling_CountsNegativeNine()
    {
        var td = new TdSeq(4);
        int minSetup = 0;
        for (int i = 0; i < 20; i++)
        {
            double p = 200.0 - i;
            td.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p + 2, p - 2, p, 1000));
            if (td.Setup < minSetup) { minSetup = td.Setup; }
        }

        Assert.Equal(-9, minSetup);
    }

    // ─── Setup clamp: never exceeds ±9 ───

    [Fact]
    public void Setup_NeverExceedsNine()
    {
        var bars = MakeBars(500, seed: 7);
        var td = new TdSeq(4);
        foreach (var b in bars)
        {
            td.Update(b);
            Assert.True(td.Setup >= -9 && td.Setup <= 9,
                $"Setup {td.Setup} out of range");
        }
    }

    // ─── Countdown clamp: never exceeds ±13 ───

    [Fact]
    public void Countdown_NeverExceedsThirteen()
    {
        var bars = MakeBars(500, seed: 7);
        var td = new TdSeq(4);
        foreach (var b in bars)
        {
            td.Update(b);
            Assert.True(td.Countdown >= -13 && td.Countdown <= 13,
                $"Countdown {td.Countdown} out of range");
        }
    }

    // ─── Pre-warmup output is zero ───

    [Fact]
    public void PreWarmup_OutputIsZero()
    {
        var td = new TdSeq(4);
        for (int i = 0; i < 4; i++)
        {
            double v = td.Update(new TBar(DateTime.UtcNow, 100 + i, 102 + i, 98 + i, 100 + i, 1000)).Value;
            Assert.Equal(0.0, v);
        }
    }

    // ─── NaN inputs: output remains finite ───

    [Fact]
    public void NaN_OutputRemainsFinite()
    {
        var td = new TdSeq(4);
        var bars = MakeBars(20);
        foreach (var b in bars) { td.Update(b); }

        // Insert NaN bar
        td.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        Assert.True(double.IsFinite(td.Last.Value));
    }

    // ─── Event-based matches streaming ───

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var bars = MakeBars(300, seed: 55);

        var streaming = new TdSeq(4);
        var streamResults = new double[bars.Length];
        for (int i = 0; i < bars.Length; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        var barSource = new TBarSeries();
        var eventTd = new TdSeq(barSource, 4);
        var eventResults = new double[bars.Length];
        for (int i = 0; i < bars.Length; i++)
        {
            barSource.Add(bars[i]);
            eventResults[i] = eventTd.Last.Value;
        }

        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i]);
        }
    }

    // ─── Different periods produce different results ───

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var bars = MakeBars(100);
        var td4 = new TdSeq(4);
        var td2 = new TdSeq(2);

        bool anyDiff = false;
        foreach (var b in bars)
        {
            double v4 = td4.Update(b).Value;
            double v2 = td2.Update(b).Value;
            if (v4 != v2) { anyDiff = true; }
        }

        Assert.True(anyDiff, "Period 4 and period 2 should produce different results on real data");
    }
}
