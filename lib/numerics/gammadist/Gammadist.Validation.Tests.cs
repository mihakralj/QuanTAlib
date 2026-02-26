using Xunit;
using MathNet.Numerics.Distributions;

namespace QuanTAlib.Tests;

/// <summary>
/// GammadistValidationTests — validates against known mathematical properties
/// of the Gamma Distribution CDF and against MathNet.Numerics Gamma.
/// Known-value tests call Gammadist.GammaCdf / StaticCdf directly (bypassing windowing)
/// so results are exact closed-form comparisons with tolerance 1e-9.
/// Note: MathNet Gamma(shape, rate) uses rate = 1/scale, so rate = 1/beta.
/// </summary>
public class GammadistValidationTests
{
    private const double Tolerance = 1e-9;
    private const double LooseTolerance = 1e-6;

    // ─── Boundary: F(0; α, β) = 0 always ────────────────────────────────────

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(0.5, 2.0)]
    [InlineData(5.0, 3.0)]
    public void GammaCdf_AtZero_IsAlwaysZero(double alpha, double beta)
    {
        Assert.Equal(0.0, Gammadist.GammaCdf(0.0, alpha, beta), Tolerance);
    }

    [Theory]
    [InlineData(-0.1, 1.0, 1.0)]
    [InlineData(-1.0, 2.0, 1.0)]
    [InlineData(-100.0, 5.0, 2.0)]
    public void GammaCdf_Negative_IsAlwaysZero(double x, double alpha, double beta)
    {
        Assert.Equal(0.0, Gammadist.GammaCdf(x, alpha, beta), Tolerance);
    }

    // ─── Boundary: F(+∞; α, β) → 1 ──────────────────────────────────────────

    [Theory]
    [InlineData(1.0, 1.0, 0.9999)]
    [InlineData(2.0, 1.0, 0.9999)]
    [InlineData(5.0, 2.0, 0.999)]
    public void GammaCdf_AtLargeX_ApproachesOne(double alpha, double beta, double minExpected)
    {
        double cdf = Gammadist.GammaCdf(1000.0, alpha, beta);
        Assert.True(cdf > minExpected,
            $"Gamma({alpha},{beta}) CDF at large x={cdf} should be > {minExpected}");
    }

    // ─── Known value: Gamma(1,1) = Exp(1), F(1;1,1) = 1 - e^(-1) ≈ 0.6321 ──

    [Fact]
    public void GammaCdf_Alpha1_Beta1_AtOne_EqualsExpDist()
    {
        // Gamma(α=1, β=1) = Exponential(λ=1): F(1) = 1 - e^(-1)
        double expected = 1.0 - Math.Exp(-1.0); // ≈ 0.63212055882856
        double actual = Gammadist.GammaCdf(1.0, 1.0, 1.0);
        Assert.Equal(expected, actual, Tolerance);
    }

    [Fact]
    public void GammaCdf_Alpha1_Beta2_AtTwo_EqualsExpDist()
    {
        // Gamma(α=1, β=2) = Exponential(λ=0.5): F(2) = 1 - e^(-2/2) = 1 - e^(-1)
        double expected = 1.0 - Math.Exp(-1.0);
        double actual = Gammadist.GammaCdf(2.0, 1.0, 2.0);
        Assert.Equal(expected, actual, Tolerance);
    }

    // ─── MathNet.Numerics cross-validation ───────────────────────────────────

    [Theory]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(2.0, 2.0, 1.0)]
    [InlineData(0.5, 0.5, 0.5)]
    [InlineData(3.0, 2.0, 1.0)]
    [InlineData(1.0, 5.0, 2.0)]
    [InlineData(5.0, 3.0, 1.0)]
    [InlineData(0.1, 1.0, 1.0)]
    [InlineData(10.0, 4.0, 2.0)]
    [InlineData(2.0, 1.5, 0.5)]
    [InlineData(8.0, 2.0, 3.0)]
    public void GammaCdf_VsMathNet_KnownValues(double x, double alpha, double beta)
    {
        // MathNet Gamma(shape, rate) where rate = 1/scale = 1/beta
        var dist = new MathNet.Numerics.Distributions.Gamma(alpha, 1.0 / beta);
        double expected = dist.CumulativeDistribution(x);
        double actual = Gammadist.GammaCdf(x, alpha, beta);
        Assert.Equal(expected, actual, Tolerance);
    }

    [Theory]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(2.0, 2.0, 1.0)]
    [InlineData(3.0, 3.0, 1.0)]
    [InlineData(5.0, 2.0, 2.0)]
    [InlineData(0.5, 1.5, 0.5)]
    public void StaticCdf_VsMathNet_KnownValues(double x, double alpha, double beta)
    {
        var dist = new MathNet.Numerics.Distributions.Gamma(alpha, 1.0 / beta);
        double expected = dist.CumulativeDistribution(x);
        double actual = Gammadist.StaticCdf(x, alpha, beta);
        Assert.Equal(expected, actual, Tolerance);
    }

    // ─── Monotonicity ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(5.0, 2.0)]
    [InlineData(2.0, 0.5)]
    public void GammaCdf_MonotonicIncreasing(double alpha, double beta)
    {
        double prev = -1.0;

        for (int i = 0; i <= 30; i++)
        {
            double x = i * 0.5;
            double cdf = Gammadist.GammaCdf(x, alpha, beta);
            Assert.True(cdf >= prev - LooseTolerance,
                $"CDF not monotonic at x={x} (α={alpha}, β={beta}): got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    // ─── Output bounded [0, 1] with streaming indicator ──────────────────────

    [Fact]
    public void GammadistCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 73001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Gammadist(alpha: 2.0, beta: 1.0, period: 20);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── Flat range → CDF at xGamma = 5.0 / beta ─────────────────────────────

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(2.0, 0.5)]
    [InlineData(5.0, 2.0)]
    public void GammadistCdf_FlatRange_ReturnsCdfAtFive(double alpha, double beta)
    {
        var ind = new Gammadist(alpha, beta, period: 20);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        // xNorm=0.5 → xGamma=5.0 → x/beta = 5/beta
        double expected = Gammadist.GammaCdf(5.0, alpha, beta);
        Assert.Equal(expected, ind.Last.Value, LooseTolerance);
    }

    // ─── Mean: E[Gamma(α,β)] = α*β; median CDF check ─────────────────────────

    [Theory]
    [InlineData(1.0, 1.0)]   // mean = 1
    [InlineData(2.0, 2.0)]   // mean = 4
    [InlineData(3.0, 1.0)]   // mean = 3
    public void GammaCdf_AtMean_IsNearExpected(double alpha, double beta)
    {
        double mean = alpha * beta;
        // For alpha >= 1, CDF at mean is between 0.5 and 1 (shifted right of median)
        double cdf = Gammadist.GammaCdf(mean, alpha, beta);
        Assert.True(cdf > 0.3 && cdf < 1.0,
            $"CDF at mean ({cdf}) should be in (0.3,1) for α={alpha}, β={beta}");
    }

    // ─── Shape shift: larger α shifts CDF right ───────────────────────────────

    [Theory]
    [InlineData(1.0, 5.0)]
    [InlineData(2.0, 5.0)]
    [InlineData(5.0, 5.0)]
    public void GammaCdf_LargerAlpha_ShiftsCdfRight(double x, double beta)
    {
        // At the same x, larger α → lower CDF (mass shifted right)
        double cdf1 = Gammadist.GammaCdf(x, 1.0, beta);
        double cdf2 = Gammadist.GammaCdf(x, 3.0, beta);
        double cdf3 = Gammadist.GammaCdf(x, 7.0, beta);

        Assert.True(cdf1 >= cdf2 - LooseTolerance,
            $"α=1 CDF={cdf1} should be >= α=3 CDF={cdf2} at x={x}");
        Assert.True(cdf2 >= cdf3 - LooseTolerance,
            $"α=3 CDF={cdf2} should be >= α=7 CDF={cdf3} at x={x}");
    }

    // ─── Scale shift: larger β stretches CDF right (same relative shape) ─────

    [Fact]
    public void GammaCdf_ScaleIdentity_Gamma_AlphaBeta_VsMathNet()
    {
        // F(x; α, β) = F(x/β; α, 1) — scaling identity
        double alpha = 3.0, beta = 2.0, x = 6.0;
        double direct = Gammadist.GammaCdf(x, alpha, beta);
        double scaled = Gammadist.GammaCdf(x / beta, alpha, 1.0);
        Assert.Equal(direct, scaled, Tolerance);
    }

    // ─── LnGamma internal correctness ────────────────────────────────────────

    [Theory]
    [InlineData(1.0, 0.0)]          // Γ(1) = 1 → ln(1) = 0
    [InlineData(2.0, 0.0)]          // Γ(2) = 1! = 1 → ln(1) = 0
    [InlineData(3.0, 0.6931471805599453)]  // Γ(3) = 2! = 2 → ln(2)
    [InlineData(4.0, 1.791759469228327)]   // Γ(4) = 3! = 6 → ln(6)
    [InlineData(5.0, 3.178053830347946)]   // Γ(5) = 4! = 24 → ln(24)
    public void LnGamma_IntegerArguments_MatchKnownValues(double z, double expected)
    {
        double actual = Gammadist.LnGamma(z);
        Assert.Equal(expected, actual, 1e-10);
    }

    // ─── Span batch consistency ───────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int count = 150;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 73002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Gammadist.Batch(bars.Close, alpha: 2.0, beta: 1.0, period: 30);
        double[] spanResult = new double[count];
        Gammadist.Batch(rawValues, spanResult, alpha: 2.0, beta: 1.0, period: 30);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void GammadistCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Gammadist(alpha: 2.0, beta: 1.0, period: period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 73003);
        var bars = gbm.Fetch(period + 50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Non-finite output at bar {i}");
        }
    }

    // ─── Parameter combos all within [0,1] ────────────────────────────────────

    [Theory]
    [InlineData(1.0, 1.0, 5)]
    [InlineData(2.0, 1.0, 14)]
    [InlineData(0.5, 0.5, 10)]
    [InlineData(5.0, 2.0, 20)]
    [InlineData(3.0, 0.5, 30)]
    public void GammadistCdf_ParameterCombos_OutputBounded(double alpha, double beta, int period)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 73004 + (int)(alpha * 100));
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Gammadist(alpha, beta, period);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0,
                $"Out of [0,1] at bar {i}: {v} (α={alpha}, β={beta}, period={period})");
        }
    }

    // ─── Large dataset stable ─────────────────────────────────────────────────

    [Fact]
    public void GammadistCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 73005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Gammadist(alpha: 2.0, beta: 1.0, period: 50);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Extreme prices don't blow up ─────────────────────────────────────────

    [Fact]
    public void GammadistCdf_ExtremePrices_StillInRange()
    {
        var indicator = new Gammadist(alpha: 2.0, beta: 1.0, period: 20);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double price = (i % 2 == 0) ? 1e10 : 1e-10;
            indicator.Update(new TValue(time.AddMinutes(i), price));
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Out of range at {i}: {v}");
        }
    }

    // ─── Multiple points all match MathNet ───────────────────────────────────

    [Fact]
    public void GammaCdf_MultiplePoints_AllMatchMathNet()
    {
        double alpha = 2.0, beta = 1.0;
        var dist = new MathNet.Numerics.Distributions.Gamma(alpha, 1.0 / beta);

        double[] testX = { 0.0, 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0 };

        foreach (double x in testX)
        {
            double expected = dist.CumulativeDistribution(x);
            double actual = Gammadist.GammaCdf(x, alpha, beta);
            Assert.Equal(expected, actual, Tolerance);
        }
    }

    // ─── RegularizedIncompleteGamma internal tests ────────────────────────────

    [Fact]
    public void RegularizedIncompleteGamma_AtZero_IsZero()
    {
        double lnGammaA = Gammadist.LnGamma(2.0);
        double result = Gammadist.RegularizedIncompleteGamma(2.0, 0.0, lnGammaA);
        Assert.Equal(0.0, result, Tolerance);
    }

    [Theory]
    [InlineData(1.0, 1.0)]   // P(1, 1) = 1 - e^(-1)
    [InlineData(2.0, 2.0)]   // vs MathNet
    [InlineData(3.0, 1.5)]   // vs MathNet
    public void RegularizedIncompleteGamma_VsMathNet(double a, double x)
    {
        var dist = new MathNet.Numerics.Distributions.Gamma(a, 1.0);
        double expected = dist.CumulativeDistribution(x);
        double lnGammaA = Gammadist.LnGamma(a);
        double actual = Gammadist.RegularizedIncompleteGamma(a, x, lnGammaA);
        Assert.Equal(expected, actual, Tolerance);
    }
}
