using Xunit;
using MathNet.Numerics.Distributions;

namespace QuanTAlib.Tests;

/// <summary>
/// NormdistValidationTests — validates against known mathematical properties
/// of the Normal Distribution CDF and against MathNet.Numerics Normal.
/// Known-value tests call Normdist.StaticCdf / NormalCdf directly (bypassing windowing)
/// so results are exact closed-form comparisons.
/// Tolerance 1e-4 for the 3-term A&amp;S approximation (max error ~2.5e-5);
/// Using 1e-4 to give headroom. MathNet cross-validation uses 1e-4.
/// </summary>
public class NormdistValidationTests
{
    private const double ApproxTolerance = 1e-4;   // A&S 3-term max error ~2.5e-5
    private const double LooseTolerance = 1e-3;

    // ─── Boundary: CDF at extreme negative → 0 ───────────────────────────────

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(0.0, 2.0)]
    public void StaticCdf_AtVeryNegativeX_ApproachesZero(double mu, double sigma)
    {
        double cdf = Normdist.StaticCdf(-100.0, mu, sigma);
        Assert.True(cdf < 1e-6, $"CDF at x=-100 should approach 0, got {cdf}");
    }

    // ─── Boundary: CDF at extreme positive → 1 ───────────────────────────────

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(0.0, 2.0)]
    public void StaticCdf_AtVeryPositiveX_ApproachesOne(double mu, double sigma)
    {
        double cdf = Normdist.StaticCdf(100.0, mu, sigma);
        Assert.True(cdf > 1.0 - 1e-6, $"CDF at x=100 should approach 1, got {cdf}");
    }

    // ─── Symmetry: CDF(mu) = 0.5 ─────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(-2.5, 1.0)]
    [InlineData(0.0, 0.5)]
    [InlineData(3.0, 2.0)]
    public void StaticCdf_AtMean_IsHalf(double mu, double sigma)
    {
        double cdf = Normdist.StaticCdf(mu, mu, sigma);
        Assert.Equal(0.5, cdf, ApproxTolerance);
    }

    // ─── Known percentiles for standard normal (μ=0, σ=1) ───────────────────

    [Fact]
    public void StaticCdf_StandardNormal_AtPlusSigma_Is0841()
    {
        // Φ(1) ≈ 0.8413447...
        double cdf = Normdist.StaticCdf(1.0, 0.0, 1.0);
        Assert.Equal(0.8413, cdf, 3);
    }

    [Fact]
    public void StaticCdf_StandardNormal_AtMinusSigma_Is0159()
    {
        // Φ(-1) ≈ 0.1586553...
        double cdf = Normdist.StaticCdf(-1.0, 0.0, 1.0);
        Assert.Equal(0.1587, cdf, 3);
    }

    [Fact]
    public void StaticCdf_StandardNormal_AtPlus2Sigma_Is0977()
    {
        // Φ(2) ≈ 0.9772499...
        double cdf = Normdist.StaticCdf(2.0, 0.0, 1.0);
        Assert.Equal(0.9772, cdf, 3);
    }

    [Fact]
    public void StaticCdf_StandardNormal_AtMinus2Sigma_Is0023()
    {
        // Φ(-2) ≈ 0.0227501...
        double cdf = Normdist.StaticCdf(-2.0, 0.0, 1.0);
        Assert.Equal(0.0228, cdf, 3);
    }

    [Fact]
    public void StaticCdf_StandardNormal_At196_Is0975()
    {
        // Φ(1.96) ≈ 0.975 (95th percentile)
        double cdf = Normdist.StaticCdf(1.96, 0.0, 1.0);
        Assert.Equal(0.975, cdf, ApproxTolerance);
    }

    [Fact]
    public void StaticCdf_StandardNormal_At2326_Is0990()
    {
        // Φ(2.326) ≈ 0.990 (99th percentile)
        double cdf = Normdist.StaticCdf(2.326, 0.0, 1.0);
        Assert.Equal(0.990, cdf, 2);
    }

    // ─── Complementary: Φ(x) + Φ(-x) = 1 ────────────────────────────────────

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(0.1)]
    public void StaticCdf_Symmetry_ComplementTo1(double z)
    {
        double pos = Normdist.StaticCdf(z, 0.0, 1.0);
        double neg = Normdist.StaticCdf(-z, 0.0, 1.0);
        Assert.Equal(1.0, pos + neg, ApproxTolerance);
    }

    // ─── MathNet.Numerics cross-validation ───────────────────────────────────

    [Theory]
    [InlineData(0.0, 0.0, 1.0)]
    [InlineData(1.0, 0.0, 1.0)]
    [InlineData(-1.0, 0.0, 1.0)]
    [InlineData(2.0, 0.0, 1.0)]
    [InlineData(-2.0, 0.0, 1.0)]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(0.5, 0.0, 2.0)]
    [InlineData(3.0, 2.0, 0.5)]
    [InlineData(-1.0, 0.0, 0.5)]
    [InlineData(1.96, 0.0, 1.0)]
    public void StaticCdf_VsMathNet_KnownValues(double x, double mu, double sigma)
    {
        var dist = new Normal(mu, sigma);
        double expected = dist.CumulativeDistribution(x);
        double actual = Normdist.StaticCdf(x, mu, sigma);
        Assert.Equal(expected, actual, ApproxTolerance);
    }

    [Theory]
    [InlineData(0.0, 0.0, 1.0)]
    [InlineData(1.0, 0.0, 1.0)]
    [InlineData(-1.0, 0.0, 1.0)]
    [InlineData(0.5, 0.5, 1.5)]
    [InlineData(2.5, 1.0, 2.0)]
    public void NormalCdf_VsMathNet_KnownValues(double x, double mu, double sigma)
    {
        var dist = new Normal(mu, sigma);
        double expected = dist.CumulativeDistribution(x);
        double actual = Normdist.NormalCdf(x, mu, sigma);
        Assert.Equal(expected, actual, ApproxTolerance);
    }

    // ─── Monotonicity ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 0.5)]
    [InlineData(-1.0, 2.0)]
    public void StaticCdf_MonotonicIncreasing(double mu, double sigma)
    {
        double prev = -1.0;

        for (int i = -30; i <= 30; i++)
        {
            double x = i * 0.3;
            double cdf = Normdist.StaticCdf(x, mu, sigma);
            Assert.True(cdf >= prev - LooseTolerance,
                $"CDF not monotonic at x={x} (μ={mu}, σ={sigma}): got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    // ─── Output bounded [0, 1] with streaming indicator ──────────────────────

    [Fact]
    public void NormdistCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 75001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Normdist(mu: 0.0, sigma: 1.0, period: 20);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── Flat range → CDF = 0.5 (z=0, erf(0)=0) ─────────────────────────────

    [Fact]
    public void NormdistCdf_FlatRange_ReturnsHalf()
    {
        // When all values in window are identical: stddev=0, z=0 → Φ(0)=0.5
        var ind = new Normdist(mu: 0.0, sigma: 1.0, period: 10);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        Assert.Equal(0.5, ind.Last.Value, LooseTolerance);
    }

    // ─── z-score interpretation: above mean → > 0.5, below mean → < 0.5 ─────

    [Fact]
    public void NormdistCdf_AboveMean_GreaterThanHalf()
    {
        // Feed data with clear trend up; last bar well above rolling mean → CDF > 0.5
        var ind = new Normdist(mu: 0.0, sigma: 1.0, period: 10);
        var time = DateTime.UtcNow;

        // Flat base, then spike
        for (int i = 0; i < 9; i++)
        {
            ind.Update(new TValue(time.AddMinutes(i), 100.0));
        }
        ind.Update(new TValue(time.AddMinutes(9), 110.0));  // spike: well above mean/stddev

        Assert.True(ind.Last.Value > 0.5, $"Above-mean value should give CDF > 0.5, got {ind.Last.Value}");
    }

    [Fact]
    public void NormdistCdf_BelowMean_LessThanHalf()
    {
        // Feed flat data, then dip → CDF < 0.5
        var ind = new Normdist(mu: 0.0, sigma: 1.0, period: 10);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 9; i++)
        {
            ind.Update(new TValue(time.AddMinutes(i), 100.0));
        }
        ind.Update(new TValue(time.AddMinutes(9), 90.0));  // dip: well below mean

        Assert.True(ind.Last.Value < 0.5, $"Below-mean value should give CDF < 0.5, got {ind.Last.Value}");
    }

    // ─── Erf internal correctness ─────────────────────────────────────────────

    [Fact]
    public void Erf_AtZero_IsZero()
    {
        Assert.Equal(0.0, Normdist.Erf(0.0), 1e-10);
    }

    [Fact]
    public void Erf_AtLargePositive_ApproachesOne()
    {
        double v = Normdist.Erf(5.0);
        Assert.True(v > 0.999, $"erf(5) should approach 1, got {v}");
    }

    [Fact]
    public void Erf_IsOddFunction()
    {
        // erf(-x) = -erf(x)
        double[] testX = { 0.5, 1.0, 1.5, 2.0, 3.0 };
        foreach (double x in testX)
        {
            double pos = Normdist.Erf(x);
            double neg = Normdist.Erf(-x);
            Assert.Equal(-pos, neg, ApproxTolerance);
        }
    }

    [Theory]
    [InlineData(0.0, 0.0)]                          // erf(0) = 0
    [InlineData(0.5, 0.5204998778)]                 // known value
    [InlineData(1.0, 0.8427007929)]                 // known value
    [InlineData(2.0, 0.9953222650)]                 // known value
    public void Erf_KnownValues_WithinApproxTolerance(double x, double expected)
    {
        double actual = Normdist.Erf(x);
        Assert.Equal(expected, actual, ApproxTolerance);
    }

    // ─── Span batch consistency ───────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int count = 150;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 75002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Normdist.Batch(bars.Close, period: 30);
        double[] spanResult = new double[count];
        Normdist.Batch(rawValues, spanResult, period: 30);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], 1e-10);
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void NormdistCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Normdist(mu: 0.0, sigma: 1.0, period: period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 75003);
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
    [InlineData(0.0, 0.5, 5)]
    [InlineData(0.0, 1.0, 14)]
    [InlineData(0.5, 1.0, 10)]
    [InlineData(0.0, 2.0, 20)]
    [InlineData(-1.0, 1.0, 30)]
    public void NormdistCdf_ParameterCombos_OutputBounded(double mu, double sigma, int period)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 75004 + (int)(sigma * 100));
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Normdist(mu, sigma, period);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0,
                $"Out of [0,1] at bar {i}: {v} (μ={mu}, σ={sigma}, period={period})");
        }
    }

    // ─── Large dataset stable ─────────────────────────────────────────────────

    [Fact]
    public void NormdistCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 75005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Normdist(mu: 0.0, sigma: 1.0, period: 50);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Multiple points all match MathNet ───────────────────────────────────

    [Fact]
    public void StaticCdf_MultiplePoints_AllMatchMathNet()
    {
        double mu = 0.0, sigma = 1.0;
        var dist = new Normal(mu, sigma);

        double[] testX = { -3.0, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 3.0 };

        foreach (double x in testX)
        {
            double expected = dist.CumulativeDistribution(x);
            double actual = Normdist.StaticCdf(x, mu, sigma);
            Assert.Equal(expected, actual, ApproxTolerance);
        }
    }

    // ─── NormalCdf invalid sigma returns 0.5 ──────────────────────────────────

    [Fact]
    public void NormalCdf_ZeroSigma_ReturnsHalf()
    {
        double cdf = Normdist.NormalCdf(1.0, 0.0, 0.0);
        Assert.Equal(0.5, cdf, 1e-10);
    }

    // ─── Sigma effect: larger sigma compresses S-curve toward 0.5 ─────────────

    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(2.0, 0.0)]
    [InlineData(0.5, 0.0)]
    public void StaticCdf_LargerSigma_CompressesCurve(double x, double mu)
    {
        // For x > mu, larger sigma → smaller (z-mu)/sigma → CDF closer to 0.5
        double cdf1 = Normdist.StaticCdf(x, mu, 0.5);
        double cdf2 = Normdist.StaticCdf(x, mu, 1.0);
        double cdf3 = Normdist.StaticCdf(x, mu, 3.0);

        // Larger sigma → CDF closer to 0.5 (smaller z)
        Assert.True(cdf1 >= cdf2 - LooseTolerance,
            $"σ=0.5 CDF={cdf1} should be >= σ=1.0 CDF={cdf2} for x>mu");
        Assert.True(cdf2 >= cdf3 - LooseTolerance,
            $"σ=1.0 CDF={cdf2} should be >= σ=3.0 CDF={cdf3} for x>mu");
    }
}
