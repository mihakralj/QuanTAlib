using Xunit;

namespace QuanTAlib.Tests;

public sealed class ConvexityTests
{
    // ── A. Constructor & Properties ──────────────────────────────────────

    [Fact]
    public void Constructor_ValidatesPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Convexity(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Convexity(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Convexity(-1));

        var c = new Convexity(2);
        Assert.NotNull(c);
    }

    [Fact]
    public void Constructor_DefaultPeriod()
    {
        var c = new Convexity();
        Assert.Equal(20, c.Period);
        Assert.Equal(21, c.WarmupPeriod); // period + 1
    }

    [Fact]
    public void Constructor_CustomPeriod()
    {
        var c = new Convexity(60);
        Assert.Equal(60, c.Period);
        Assert.Equal(61, c.WarmupPeriod);
    }

    [Fact]
    public void Properties_InitialState()
    {
        var c = new Convexity(10);
        Assert.Equal(0, c.Last.Value);
        Assert.Equal(0, c.BetaStd);
        Assert.Equal(0, c.BetaUp);
        Assert.Equal(0, c.BetaDown);
        Assert.Equal(0, c.Ratio);
        Assert.Equal(0, c.ConvexityValue);
        Assert.False(c.IsHot);
        Assert.Contains("Convexity", c.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleInput_ThrowsNotSupported()
    {
        var c = new Convexity(10);
        Assert.Throws<NotSupportedException>(() => c.Update(new TValue(DateTime.UtcNow, 100)));
        Assert.Throws<NotSupportedException>(() => c.Update(new TSeries()));
        Assert.Throws<NotSupportedException>(() => c.Prime([1, 2, 3]));
    }

    // ── B. IsHot warmup ──────────────────────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrueAfterPeriodPlusOne()
    {
        const int period = 5;
        var c = new Convexity(period);

        // First update initializes prev prices, no return computed yet
        for (int i = 0; i <= period; i++)
        {
            Assert.False(c.IsHot, $"IsHot should be false at index {i}");
            c.Update(100.0 + i, 100.0 + i);
        }

        Assert.True(c.IsHot, "IsHot should be true after period+1 updates");
    }

    // ── C. Known values: symmetric beta ────────────────────────────────

    [Fact]
    public void SymmetricBeta_ConvexityIsZero()
    {
        // Asset = 2x market returns in BOTH directions
        // Use varying magnitudes so up/down subsets have variance
        // → BetaUp ≈ 2, BetaDown ≈ 2 → Convexity ≈ 0
        const int period = 10;
        var c = new Convexity(period);
        var rng = new Random(42);

        double mkt = 100;
        double ast = 100;
        c.Update(ast, mkt);

        for (int i = 1; i <= period; i++)
        {
            double sign = (i % 2 == 0) ? 1 : -1;
            double magnitude = 0.01 + (rng.NextDouble() * 0.03);
            double mktReturn = sign * magnitude;
            mkt *= (1 + mktReturn);
            ast *= (1 + (2 * mktReturn)); // exactly 2x market return
            c.Update(ast, mkt);
        }

        Assert.True(c.IsHot);
        Assert.True(c.BetaStd > 1.5, $"BetaStd={c.BetaStd} should be near 2");
        Assert.True(c.BetaStd < 2.5, $"BetaStd={c.BetaStd} should be near 2");
        Assert.True(c.ConvexityValue < 0.5, $"Convexity={c.ConvexityValue} should be near 0 for symmetric beta");
    }

    [Fact]
    public void AsymmetricBeta_ConvexityIsPositive()
    {
        // Asset amplifies gains (3x up) but dampens losses (1x down)
        // Use VARYING magnitude returns so there's variance within up/down subsets
        // (identical magnitudes → zero variance → beta undefined)
        const int period = 20;
        var c = new Convexity(period);
        var rng = new Random(42);

        double mkt = 100;
        double ast = 100;
        c.Update(ast, mkt);

        for (int i = 1; i <= period; i++)
        {
            double sign = (i % 2 == 0) ? 1 : -1;
            double magnitude = 0.01 + (rng.NextDouble() * 0.03); // 1%-4% varying
            double mktReturn = sign * magnitude;
            double astReturn;
            if (mktReturn > 0)
            {
                astReturn = 3 * mktReturn;  // 3x on up days
            }
            else
            {
                astReturn = 1 * mktReturn;  // 1x on down days
            }
            mkt *= (1 + mktReturn);
            ast *= (1 + astReturn);
            c.Update(ast, mkt);
        }

        Assert.True(c.IsHot);
        Assert.True(c.BetaUp > 2.0, $"BetaUp={c.BetaUp} should be near 3");
        Assert.True(c.BetaDown > 0.5, $"BetaDown={c.BetaDown} should be near 1");
        Assert.True(c.ConvexityValue > 1.0, $"Convexity={c.ConvexityValue} should be > 1 for asymmetric beta");
        Assert.True(c.Ratio > 1.0, $"Ratio={c.Ratio} should be > 1 (favorable asymmetry)");
    }

    // ── D. Convexity is always non-negative ──────────────────────────────

    [Fact]
    public void ConvexityIsAlwaysNonNegative()
    {
        var c = new Convexity(10);
        var rng = new Random(42);

        c.Update(100.0, 100.0);
        for (int i = 0; i < 50; i++)
        {
            double ast = 100.0 + (rng.NextDouble() * 20) - 10;
            double mkt = 100.0 + (rng.NextDouble() * 20) - 10;
            c.Update(ast, mkt);
            Assert.True(c.ConvexityValue >= 0, $"Convexity must be ≥ 0, got {c.ConvexityValue} at i={i}");
        }
    }

    // ── E. Identical series → Beta = 1, Convexity = 0 ────────────────────

    [Fact]
    public void IdenticalSeries_BetaIsOne()
    {
        const int period = 10;
        var c = new Convexity(period);
        var rng = new Random(42);

        double price = 100;
        c.Update(price, price);

        for (int i = 1; i <= period + 5; i++)
        {
            double sign = (i % 2 == 0) ? 1 : -1;
            double magnitude = 0.005 + (rng.NextDouble() * 0.02);
            price *= (1 + (sign * magnitude));
            c.Update(price, price);
        }

        Assert.True(c.IsHot);
        Assert.True(Math.Abs(c.BetaStd - 1.0) < 0.01, $"BetaStd={c.BetaStd} should be 1.0 for identical series");
    }

    // ── F. Reset ─────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var c = new Convexity(5);
        c.Update(100.0, 100.0);
        c.Update(101.0, 101.0);
        c.Update(102.0, 102.0);

        c.Reset();

        Assert.False(c.IsHot);
        Assert.Equal(0, c.BetaStd);
        Assert.Equal(0, c.BetaUp);
        Assert.Equal(0, c.BetaDown);
        Assert.Equal(0, c.Ratio);
        Assert.Equal(0, c.ConvexityValue);
    }

    [Fact]
    public void Reset_RestartsCleanly()
    {
        var c = new Convexity(5);

        // First run
        c.Update(100.0, 100.0);
        for (int i = 1; i <= 6; i++)
        {
            c.Update(100.0 + i, 100.0 + i);
        }
        double firstBeta = c.BetaStd;

        // Reset and run again with same data
        c.Reset();
        c.Update(100.0, 100.0);
        for (int i = 1; i <= 6; i++)
        {
            c.Update(100.0 + i, 100.0 + i);
        }
        double secondBeta = c.BetaStd;

        Assert.Equal(firstBeta, secondBeta, 10);
    }

    // ── G. Bar correction ────────────────────────────────────────────────

    [Fact]
    public void BarCorrection_UpdatesSameBar()
    {
        var c = new Convexity(5);

        c.Update(100.0, 100.0);
        c.Update(101.0, 101.0);
        c.Update(102.0, 102.0);

        // Correct last bar
        c.Update(103.0, 103.0, isNew: false);

        // Should not crash, and should produce a valid result
        Assert.True(double.IsFinite(c.ConvexityValue));
    }

    [Fact]
    public void BarCorrection_MatchesFreshCalculation()
    {
        const int period = 5;

        // Path A: feed N bars, then update last bar with correction
        var cA = new Convexity(period);
        double[] assets = [100, 101, 99, 102, 98, 103, 97, 104];
        double[] markets = [100, 100.5, 99.5, 101, 99, 101.5, 98.5, 102];

        for (int i = 0; i < assets.Length - 1; i++)
        {
            cA.Update(assets[i], markets[i]);
        }
        // Feed last bar, then correct it
        cA.Update(999.0, 999.0);
        cA.Update(assets[^1], markets[^1], isNew: false);

        // Path B: feed all bars cleanly
        var cB = new Convexity(period);
        for (int i = 0; i < assets.Length; i++)
        {
            cB.Update(assets[i], markets[i]);
        }

        Assert.Equal(cB.BetaStd, cA.BetaStd, 6);
        Assert.Equal(cB.ConvexityValue, cA.ConvexityValue, 6);
        Assert.Equal(cB.BetaUp, cA.BetaUp, 6);
        Assert.Equal(cB.BetaDown, cA.BetaDown, 6);
    }

    // ── H. Batch API ─────────────────────────────────────────────────────

    [Fact]
    public void Batch_MatchesStreaming()
    {
        const int period = 5;
        var assetSeries = new TSeries(10);
        var marketSeries = new TSeries(10);
        var rng = new Random(42);

        double ast = 100, mkt = 100;
        for (int i = 0; i < 10; i++)
        {
            double sign = (i % 2 == 0) ? 1 : -1;
            double magnitude = 0.005 + (rng.NextDouble() * 0.02);
            ast *= (1 + (sign * magnitude * 1.5));
            mkt *= (1 + (sign * magnitude));
            assetSeries.Add(new TValue(i, ast));
            marketSeries.Add(new TValue(i, mkt));
        }

        var (betaStdS, _, _, _, convexityS) = Convexity.Batch(assetSeries, marketSeries, period);

        // Compare last value with streaming
        var streaming = new Convexity(period);
        for (int i = 0; i < 10; i++)
        {
            streaming.Update(assetSeries[i], marketSeries[i]);
        }

        Assert.Equal(streaming.BetaStd, betaStdS[^1].Value, 8);
        Assert.Equal(streaming.ConvexityValue, convexityS[^1].Value, 8);
        Assert.Equal(10, convexityS.Count);
    }

    [Fact]
    public void Batch_MismatchedLengths_Throws()
    {
        var a = new TSeries(5);
        var b = new TSeries(3);
        for (int i = 0; i < 5; i++)
        {
            a.Add(new TValue(i, 100 + i));
        }
        for (int i = 0; i < 3; i++)
        {
            b.Add(new TValue(i, 100 + i));
        }

        Assert.Throws<ArgumentException>(() => Convexity.Batch(a, b, 5));
    }

    // ── I. Double overload ───────────────────────────────────────────────

    [Fact]
    public void DoubleOverload_ProducesFiniteResults()
    {
        var c = new Convexity(5);
        c.Update(100.0, 100.0);
        c.Update(101.0, 101.0);
        c.Update(99.0, 99.5);
        c.Update(102.0, 101.5);
        c.Update(98.0, 99.0);
        c.Update(103.0, 102.0);

        Assert.True(double.IsFinite(c.ConvexityValue));
        Assert.True(double.IsFinite(c.BetaStd));
        Assert.True(double.IsFinite(c.BetaUp));
        Assert.True(double.IsFinite(c.BetaDown));
    }

    // ── J. Edge cases ────────────────────────────────────────────────────

    [Fact]
    public void ConstantPrices_BetaIsZero()
    {
        var c = new Convexity(5);
        for (int i = 0; i < 10; i++)
        {
            c.Update(100.0, 100.0);
        }

        // Constant prices → zero returns → zero variance → beta = 0
        Assert.Equal(0, c.BetaStd);
        Assert.Equal(0, c.ConvexityValue);
    }

    [Fact]
    public void ZeroPrevPrice_ReturnsZero()
    {
        var c = new Convexity(5);
        c.Update(0.0, 0.0);
        c.Update(100.0, 100.0);

        // Division by zero for return computation should be handled
        Assert.True(double.IsFinite(c.ConvexityValue));
    }

    [Fact]
    public void NegativeBeta_HandledCorrectly()
    {
        // Asset moves opposite to market → negative beta
        const int period = 10;
        var c = new Convexity(period);
        var rng = new Random(42);

        double mkt = 100, ast = 100;
        c.Update(ast, mkt);

        for (int i = 1; i <= period; i++)
        {
            double sign = (i % 2 == 0) ? 1 : -1;
            double magnitude = 0.01 + (rng.NextDouble() * 0.03);
            double mktRet = sign * magnitude;
            mkt *= (1 + mktRet);
            ast *= (1 - mktRet); // inverse
            c.Update(ast, mkt);
        }

        Assert.True(c.IsHot);
        Assert.True(c.BetaStd < 0, $"BetaStd={c.BetaStd} should be negative for inverse relationship");
    }

    [Fact]
    public void Ratio_DivisionByZero_ReturnsZero()
    {
        // If all market bars go up, BetaDown = 0, Ratio should be 0
        const int period = 5;
        var c = new Convexity(period);

        double mkt = 100, ast = 100;
        c.Update(ast, mkt);

        for (int i = 1; i <= period; i++)
        {
            mkt *= 1.01; // always up
            ast *= 1.02;
            c.Update(ast, mkt);
        }

        Assert.True(c.IsHot);
        Assert.Equal(0, c.BetaDown);
        Assert.Equal(0, c.Ratio);
    }

    // ── K. Streaming consistency ─────────────────────────────────────────

    [Fact]
    public void LongStream_RemainsFinite()
    {
        var c = new Convexity(20);
        var rng = new Random(123);

        double ast = 100, mkt = 100;
        c.Update(ast, mkt);

        for (int i = 0; i < 1000; i++)
        {
            ast *= (1 + ((rng.NextDouble() - 0.5) * 0.04));
            mkt *= (1 + ((rng.NextDouble() - 0.5) * 0.02));
            c.Update(ast, mkt);

            Assert.True(double.IsFinite(c.ConvexityValue), $"ConvexityValue not finite at i={i}");
            Assert.True(double.IsFinite(c.BetaStd), $"BetaStd not finite at i={i}");
        }
    }

    [Fact]
    public void GBM_ProducesReasonableValues()
    {
        var gbmAsset = new GBM(100.0, 0.05, 0.3, seed: 42);
        var gbmMarket = new GBM(100.0, 0.04, 0.15, seed: 99);

        var assetBars = gbmAsset.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromDays(1));
        var marketBars = gbmMarket.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromDays(1));

        var c = new Convexity(20);
        for (int i = 0; i < assetBars.Count; i++)
        {
            c.Update(assetBars[i].Close, marketBars[i].Close);
        }

        Assert.True(c.IsHot);
        Assert.True(double.IsFinite(c.ConvexityValue));
        Assert.True(double.IsFinite(c.BetaStd));
        Assert.True(c.ConvexityValue >= 0);
    }
}
