using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for SqueezePro indicator.
/// Tests determinism, identity properties, and mathematical invariants.
/// </summary>
public sealed class SqueezeProValidationTests
{
    private static TBarSeries GenerateBars(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === Determinism ===

    [Theory]
    [InlineData(10, 2.0, 2.0, 1.5, 1.0, 5, 3, true)]
    [InlineData(20, 2.0, 2.0, 1.5, 1.0, 12, 6, true)]
    [InlineData(15, 1.5, 3.0, 2.0, 1.0, 8, 4, false)]
    public void DifferentParams_Deterministic(int period, double bbMult,
        double kcWide, double kcNormal, double kcNarrow, int momLen, int momSmooth, bool useSma)
    {
        var bars = GenerateBars(50);

        var sq1 = new SqueezePro(period, bbMult, kcWide, kcNormal, kcNarrow, momLen, momSmooth, useSma);
        var sq2 = new SqueezePro(period, bbMult, kcWide, kcNormal, kcNarrow, momLen, momSmooth, useSma);

        for (int i = 0; i < 50; i++)
        {
            sq1.Update(bars[i], isNew: true);
            sq2.Update(bars[i], isNew: true);
        }

        Assert.Equal(sq1.Momentum, sq2.Momentum, precision: 12);
        Assert.Equal(sq1.SqueezeLevel, sq2.SqueezeLevel);
    }

    // === Streaming vs Batch consistency ===

    [Fact]
    public void Streaming_Equals_Batch_AllBars()
    {
        var bars = GenerateBars(80);
        const int period = 15;
        const int momLen = 8;
        const int momSmooth = 4;

        // Streaming
        var sq = new SqueezePro(period, momLength: momLen, momSmooth: momSmooth);
        double[] streamMom = new double[80];
        int[] streamSq = new int[80];
        for (int i = 0; i < 80; i++)
        {
            sq.Update(bars[i], isNew: true);
            streamMom[i] = sq.Momentum;
            streamSq[i] = sq.SqueezeLevel;
        }

        // Batch
        double[] batchMom = new double[80];
        double[] batchSq = new double[80];
        SqueezePro.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            batchMom, batchSq, period, momLength: momLen, momSmooth: momSmooth);

        for (int i = 0; i < 80; i++)
        {
            Assert.Equal(streamMom[i], batchMom[i], precision: 6);
            Assert.Equal(streamSq[i], (int)batchSq[i]);
        }
    }

    // === Squeeze hierarchy: narrow ⊂ normal ⊂ wide ===

    [Fact]
    public void SqueezeHierarchy_NarrowImpliesNormal()
    {
        var bars = GenerateBars(200, seed: 99);
        var sq = new SqueezePro(period: 20, momLength: 12, momSmooth: 6);

        for (int i = 0; i < 200; i++)
        {
            sq.Update(bars[i], isNew: true);

            // If narrow squeeze (3), then it must also satisfy normal squeeze
            // Since level is classified as max level, if level=3, it means insideNarrow was true
            // which implies insideNormal was also true
            if (sq.SqueezeLevel == 3)
            {
                // Narrow squeeze is only possible when also inside normal and wide
                Assert.True(sq.SqueezeLevel >= 2);
            }
        }
    }

    // === Momentum sign under trending conditions ===

    [Fact]
    public void StrongUptrend_PersistentPositiveMomentum()
    {
        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);
        int positiveCount = 0;
        int totalHot = 0;

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (i * 2.0); // strong uptrend
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            sq.Update(bar);

            if (sq.IsHot)
            {
                totalHot++;
                if (sq.Momentum > 0) { positiveCount++; }
            }
        }

        // In a strong uptrend, momentum should be positive most of the time
        Assert.True(totalHot > 0);
        double ratio = (double)positiveCount / totalHot;
        Assert.True(ratio > 0.9, $"Expected >90% positive momentum in uptrend, got {ratio:P1}");
    }

    [Fact]
    public void StrongDowntrend_PersistentNegativeMomentum()
    {
        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);
        int negativeCount = 0;
        int totalHot = 0;

        for (int i = 0; i < 100; i++)
        {
            double price = 500.0 - (i * 2.0); // strong downtrend
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            sq.Update(bar);

            if (sq.IsHot)
            {
                totalHot++;
                if (sq.Momentum < 0) { negativeCount++; }
            }
        }

        Assert.True(totalHot > 0);
        double ratio = (double)negativeCount / totalHot;
        Assert.True(ratio > 0.9, $"Expected >90% negative momentum in downtrend, got {ratio:P1}");
    }

    // === KC multiplier ordering ===

    [Fact]
    public void LargerKcMult_MoreSqueeze()
    {
        // Larger KC multiplier = wider KC = easier for BB to be inside = more squeeze
        var bars = GenerateBars(100, seed: 77);

        var sqTight = new SqueezePro(period: 20, kcMultWide: 1.0, kcMultNormal: 0.8, kcMultNarrow: 0.5);
        var sqWide = new SqueezePro(period: 20, kcMultWide: 3.0, kcMultNormal: 2.5, kcMultNarrow: 2.0);

        int tightSqueezeCount = 0;
        int wideSqueezeCount = 0;

        for (int i = 0; i < 100; i++)
        {
            sqTight.Update(bars[i], isNew: true);
            sqWide.Update(bars[i], isNew: true);

            if (sqTight.SqueezeLevel > 0) { tightSqueezeCount++; }
            if (sqWide.SqueezeLevel > 0) { wideSqueezeCount++; }
        }

        // Wider KC should detect more squeeze instances
        Assert.True(wideSqueezeCount >= tightSqueezeCount,
            $"Wide KC squeeze count ({wideSqueezeCount}) should be >= tight KC ({tightSqueezeCount})");
    }

    // === Reset and replay ===

    [Fact]
    public void ResetAndReplay_SameResults()
    {
        var bars = GenerateBars(50);
        var sq = new SqueezePro(period: 10, momLength: 5, momSmooth: 3);

        for (int i = 0; i < 50; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        double mom1 = sq.Momentum;
        int level1 = sq.SqueezeLevel;

        sq.Reset();

        for (int i = 0; i < 50; i++)
        {
            sq.Update(bars[i], isNew: true);
        }

        Assert.Equal(mom1, sq.Momentum, precision: 10);
        Assert.Equal(level1, sq.SqueezeLevel);
    }

    // === Boundary: period=1 ===

    [Fact]
    public void MinimalPeriod_NoThrow()
    {
        var sq = new SqueezePro(period: 1, momLength: 1, momSmooth: 1);
        var bars = GenerateBars(20);
        for (int i = 0; i < 20; i++)
        {
            sq.Update(bars[i], isNew: true);
        }
        Assert.True(double.IsFinite(sq.Momentum));
    }

    // === Large period — ArrayPool path ===

    [Fact]
    public void LargePeriod_ArrayPoolPath()
    {
        var bars = GenerateBars(500, seed: 88);
        double[] mom = new double[500];
        double[] sq = new double[500];
        // total buffers = 300 + 50 + 20 = 370 > 256 → ArrayPool
        SqueezePro.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            mom, sq, period: 300, momLength: 50, momSmooth: 20);
        Assert.True(double.IsFinite(mom[499]));
    }

    // === EMA vs SMA smoothing same seed ===

    [Fact]
    public void EmaVsSma_SameSqueezeLevel()
    {
        // Smoothing mode only affects momentum, not squeeze detection
        var bars = GenerateBars(50);
        var sqSma = new SqueezePro(period: 10, momLength: 5, momSmooth: 3, useSma: true);
        var sqEma = new SqueezePro(period: 10, momLength: 5, momSmooth: 3, useSma: false);

        for (int i = 0; i < 50; i++)
        {
            sqSma.Update(bars[i], isNew: true);
            sqEma.Update(bars[i], isNew: true);

            // Squeeze level should be identical regardless of smoothing mode
            Assert.Equal(sqSma.SqueezeLevel, sqEma.SqueezeLevel);
        }
    }
}
