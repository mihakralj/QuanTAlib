using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Squeeze — internal consistency checks.
/// No external library implements this indicator identically, so we validate
/// against known mathematical properties and self-consistency.
/// </summary>
public sealed class SqueezeValidationTests
{
    private static TBarSeries GenerateBars(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // 1. Streaming == Batch (TBarSeries) consistency
    [Fact]
    public void Streaming_MatchesBatch_Momentum()
    {
        var bars = GenerateBars(100);
        const int period = 20;

        // Streaming
        var sq = new Squeeze(period);
        for (int i = 0; i < bars.Count; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        double streamMom = sq.Momentum;

        // Batch
        var (momSeries, _) = Squeeze.Batch(bars, period);
        double batchMom = momSeries[^1].Value;

        Assert.Equal(streamMom, batchMom, precision: 8);
    }

    // 2. Streaming == Batch (TBarSeries) for SqueezeOn state
    [Fact]
    public void Streaming_MatchesBatch_SqueezeOn()
    {
        var bars = GenerateBars(100);
        const int period = 20;

        var sq = new Squeeze(period);
        for (int i = 0; i < bars.Count; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        bool streamSqOn = sq.SqueezeOn;

        var (_, sqSeries) = Squeeze.Batch(bars, period);
        bool batchSqOn = sqSeries[^1].Value >= 0.5;

        Assert.Equal(streamSqOn, batchSqOn);
    }

    // 3. Span Batch == Streaming
    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var bars = GenerateBars(100);
        const int period = 20;

        var sq = new Squeeze(period);
        for (int i = 0; i < bars.Count; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        double streamMom = sq.Momentum;

        double[] momOut = new double[100];
        double[] sqOut = new double[100];
        Squeeze.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            momOut, sqOut, period);

        Assert.Equal(streamMom, momOut[99], precision: 8);
    }

    // 4. Constant price → zero momentum (delta always 0)
    [Fact]
    public void ConstantPrice_ZeroMomentum()
    {
        const int period = 10;
        var sq = new Squeeze(period);
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            sq.Update(bar, isNew: true);
        }
        Assert.Equal(0.0, sq.Momentum, precision: 10);
    }

    // 5. Rising price → positive momentum (linreg endpoint positive)
    [Fact]
    public void RisingPrice_PositiveMomentum()
    {
        const int period = 10;
        var sq = new Squeeze(period);
        for (int i = 0; i < 50; i++)
        {
            double p = 100.0 + i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), p, p + 1, p - 1, p, 1000);
            sq.Update(bar, isNew: true);
        }
        Assert.True(sq.Momentum > 0.0);
    }

    // 6. Falling price → negative momentum
    [Fact]
    public void FallingPrice_NegativeMomentum()
    {
        const int period = 10;
        var sq = new Squeeze(period);
        for (int i = 0; i < 50; i++)
        {
            double p = 200.0 - i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), p, p + 1, p - 1, p, 1000);
            sq.Update(bar, isNew: true);
        }
        Assert.True(sq.Momentum < 0.0);
    }

    // 7. Very tight range → BB inside KC → squeeze should be ON
    [Fact]
    public void VeryTightRange_SqueezeOn_True()
    {
        // Extremely tight range → stddev very small → BB narrows inside KC
        const int period = 20;
        var sq = new Squeeze(period, bbMult: 2.0, kcMult: 1.5);
        // Use tiny sigma so BB << KC
        var gbm = new GBM(100.0, 0.0, 0.001, seed: 99); // near-constant with tiny noise
        var bars = gbm.Fetch(60, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < bars.Count; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        // After 60 bars with near-zero sigma, BB should be inside KC
        Assert.True(sq.SqueezeOn);
    }

    // 8. Very high volatility → BB outside KC → squeeze should be OFF
    [Fact]
    public void HighVolatility_SqueezeOn_False()
    {
        const int period = 20;
        var sq = new Squeeze(period, bbMult: 2.0, kcMult: 1.5);
        // Use very high sigma so BB >> KC
        var gbm = new GBM(100.0, 0.0, 5.0, seed: 77); // wild swings
        var bars = gbm.Fetch(60, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        for (int i = 0; i < bars.Count; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        Assert.False(sq.SqueezeOn);
    }

    // 9. Period=1 edge case — should not crash
    [Fact]
    public void Period1_DoesNotCrash()
    {
        var sq = new Squeeze(period: 1);
        for (int i = 0; i < 10; i++)
        {
            double p = 100.0 + i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), p, p + 1, p - 1, p, 1000);
            sq.Update(bar, isNew: true);
        }
        Assert.True(double.IsFinite(sq.Momentum));
    }

    // 10. Bar correction: feeding same bar multiple times with isNew=false restores original result
    [Fact]
    public void MultipleCorrections_Idempotent()
    {
        var bars = GenerateBars(25);
        const int period = 10;

        var sq = new Squeeze(period);
        for (int i = 0; i < 24; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        sq.Update(bars[24], isNew: true);
        double momRef = sq.Momentum;

        // Correct 3 more times with same bar
        for (int k = 0; k < 3; k++)
        {
            sq.Update(bars[24], isNew: false);
        }
        Assert.Equal(momRef, sq.Momentum, precision: 10);
    }

    // 11. Update(TBarSeries) === streaming
    [Fact]
    public void UpdateTBarSeries_MatchesStreaming()
    {
        var bars = GenerateBars(50);
        const int period = 10;

        // Streaming
        var sqStream = new Squeeze(period);
        for (int i = 0; i < bars.Count; i++)
        {
            sqStream.Update(bars[i], isNew: true);
        }

        // TBarSeries update
        var sqBatch = new Squeeze(period);
        _ = sqBatch.Update(bars);

        Assert.Equal(sqStream.Momentum, sqBatch.Momentum, precision: 8);
        Assert.Equal(sqStream.SqueezeOn, sqBatch.SqueezeOn);
    }

    [Fact]
    public void Squeeze_Correction_Recomputes()
    {
        var ind = new Squeeze(period: 20);
        long t0 = TimeSpan.TicksPerSecond;

        // Build state well past warmup (WarmupPeriod = 20)
        for (int i = 0; i < 50; i++)
        {
            double p = 100.0 + i * 0.5;
            ind.Update(new TBar(t0 + i * TimeSpan.TicksPerSecond, p, p + 1, p - 1, p, 1000), isNew: true);
        }

        // Anchor bar
        long anchorTime = t0 + 50 * TimeSpan.TicksPerSecond;
        var anchorBar = new TBar(anchorTime, 125.0, 126.0, 124.0, 125.0, 1000);
        ind.Update(anchorBar, isNew: true);
        double anchorMomentum = ind.Momentum;
        bool anchorSqueezeOn = ind.SqueezeOn;

        // Correction with dramatically different values — Momentum must change
        var corruptBar = new TBar(anchorTime, 1250.0, 1260.0, 1240.0, 1250.0, 1000);
        ind.Update(corruptBar, isNew: false);
        Assert.NotEqual(anchorMomentum, ind.Momentum);

        // Correction back to original — both outputs must restore exactly
        ind.Update(anchorBar, isNew: false);
        Assert.Equal(anchorMomentum, ind.Momentum, 1e-9);
        Assert.Equal(anchorSqueezeOn, ind.SqueezeOn);
    }
}
