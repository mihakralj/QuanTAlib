using Xunit;
using MathNet.Numerics.Distributions;

namespace QuanTAlib.Tests;

/// <summary>
/// LognormdistValidationTests — validates against known mathematical properties
/// of the Log-Normal Distribution CDF and against MathNet.Numerics LogNormal.
/// Known-value tests call Lognormdist.StaticCdf / LogNormalCdf directly (bypassing windowing).
/// Tolerance 1e-6 for the 5-term A&amp;S 7.1.26 approximation (max error ~1.5e-7;
/// using 1e-6 to give headroom). MathNet cross-validation uses 1e-6.
/// </summary>
public class LognormdistValidationTests
{
    private const double ApproxTolerance = 1e-6;   // A&S 7.1.26 five-term max error ~1.5e-7
    private const double LooseTolerance  = 1e-4;

    // ─── Boundary: x <= 0 → CDF = 0 ──────────────────────────────────────────

    [Theory]
    [InlineData(0.0,  0.0, 1.0)]
    [InlineData(-1.0, 0.0, 1.0)]
    [InlineData(-5.0, 0.0, 1.0)]
    [InlineData(0.0, -1.0, 0.5)]
    public void StaticCdf_NonPositiveX_IsZero(double x, double mu, double sigma)
    {
        double cdf = Lognormdist.StaticCdf(x, mu, sigma);
        Assert.Equal(0.0, cdf, 1e-10);
    }

    // ─── CDF always in [0, 1] ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0.001, 0.0, 1.0)]
    [InlineData(0.5,   0.0, 1.0)]
    [InlineData(1.0,   0.0, 1.0)]
    [InlineData(10.0,  0.0, 1.0)]
    [InlineData(1000.0, 0.0, 1.0)]
    [InlineData(0.1,  -1.0, 0.5)]
    [InlineData(2.0,   1.0, 2.0)]
    public void StaticCdf_OutputBounded_ZeroToOne(double x, double mu, double sigma)
    {
        double cdf = Lognormdist.StaticCdf(x, mu, sigma);
        Assert.True(cdf >= 0.0 && cdf <= 1.0,
            $"CDF({x},{mu},{sigma})={cdf} out of [0,1]");
    }

    // ─── Median: F(exp(μ)) = 0.5 ─────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(-2.0, 0.5)]
    [InlineData(0.0, 2.0)]
    [InlineData(3.0, 0.25)]
    public void StaticCdf_AtMedian_IsHalf(double mu, double sigma)
    {
        // Median of LogNormal(μ, σ²) = exp(μ)
        double median = Math.Exp(mu);
        double cdf = Lognormdist.StaticCdf(median, mu, sigma);
        Assert.Equal(0.5, cdf, ApproxTolerance);
    }

    // ─── Standard LogNormal(0,1) known percentiles ────────────────────────────

    [Fact]
    public void StaticCdf_LogNormal01_At1_IsHalf()
    {
        // ln(1)=0=μ, so z=0 → Φ(0)=0.5
        double cdf = Lognormdist.StaticCdf(1.0, 0.0, 1.0);
        Assert.Equal(0.5, cdf, ApproxTolerance);
    }

    [Fact]
    public void StaticCdf_LogNormal01_AtExpPlusSigma_Is0841()
    {
        // F(exp(μ+σ)) = F(exp(1)) = Φ(1) ≈ 0.8413
        double x = Math.Exp(1.0);   // exp(μ+σ) with μ=0, σ=1
        double cdf = Lognormdist.StaticCdf(x, 0.0, 1.0);
        Assert.Equal(0.8413, cdf, 3);
    }

    [Fact]
    public void StaticCdf_LogNormal01_AtExpMinusSigma_Is0159()
    {
        // F(exp(μ-σ)) = F(exp(-1)) = Φ(-1) ≈ 0.1587
        double x = Math.Exp(-1.0);  // exp(μ-σ) with μ=0, σ=1
        double cdf = Lognormdist.StaticCdf(x, 0.0, 1.0);
        Assert.Equal(0.1587, cdf, 3);
    }

    [Fact]
    public void StaticCdf_LogNormal01_AtExpPlus2Sigma_Is0977()
    {
        // F(exp(μ+2σ)) = Φ(2) ≈ 0.9772
        double x = Math.Exp(2.0);
        double cdf = Lognormdist.StaticCdf(x, 0.0, 1.0);
        Assert.Equal(0.9772, cdf, 3);
    }

    [Fact]
    public void StaticCdf_LogNormal01_AtExpMinus2Sigma_Is0023()
    {
        // F(exp(-2)) = Φ(-2) ≈ 0.0228
        double x = Math.Exp(-2.0);
        double cdf = Lognormdist.StaticCdf(x, 0.0, 1.0);
        Assert.Equal(0.0228, cdf, 3);
    }

    // ─── Monotonicity for x > 0 ───────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 0.5)]
    [InlineData(-1.0, 2.0)]
    public void StaticCdf_MonotonicIncreasing_ForPositiveX(double mu, double sigma)
    {
        double prev = -1.0;

        for (int i = -20; i <= 20; i++)
        {
            double x = Math.Exp(i * 0.25);   // x in (exp(-5), exp(5)) — always positive
            double cdf = Lognormdist.StaticCdf(x, mu, sigma);
            Assert.True(cdf >= prev - LooseTolerance,
                $"CDF not monotonic at x={x} (μ={mu}, σ={sigma}): got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    // ─── MathNet.Numerics cross-validation ────────────────────────────────────

    [Theory]
    [InlineData(1.0,    0.0, 1.0)]
    [InlineData(2.0,    0.0, 1.0)]
    [InlineData(0.5,    0.0, 1.0)]
    [InlineData(0.1,    0.0, 1.0)]
    [InlineData(10.0,   0.0, 1.0)]
    [InlineData(1.0,    1.0, 1.0)]
    [InlineData(0.5,    0.0, 2.0)]
    [InlineData(3.0,    2.0, 0.5)]
    [InlineData(0.1,   -1.0, 0.5)]
    [InlineData(1.0,    0.0, 0.25)]
    public void StaticCdf_VsMathNet_KnownValues(double x, double mu, double sigma)
    {
        var dist = new LogNormal(mu, sigma);
        double expected = dist.CumulativeDistribution(x);
        double actual = Lognormdist.StaticCdf(x, mu, sigma);
        Assert.Equal(expected, actual, ApproxTolerance);
    }

    [Theory]
    [InlineData(1.0,    0.0, 1.0)]
    [InlineData(2.718,  0.0, 1.0)]
    [InlineData(0.368,  0.0, 1.0)]
    [InlineData(1.0,    1.0, 2.0)]
    [InlineData(5.0,    1.0, 0.5)]
    public void LogNormalCdf_VsMathNet_KnownValues(double x, double mu, double sigma)
    {
        var dist = new LogNormal(mu, sigma);
        double expected = dist.CumulativeDistribution(x);
        double actual = Lognormdist.LogNormalCdf(x, mu, sigma);
        Assert.Equal(expected, actual, ApproxTolerance);
    }

    // ─── Multiple points all match MathNet ───────────────────────────────────

    [Fact]
    public void StaticCdf_MultiplePoints_AllMatchMathNet()
    {
        double mu = 0.0, sigma = 1.0;
        var dist = new LogNormal(mu, sigma);

        double[] testX = { 0.01, 0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 50.0, 100.0 };

        foreach (double x in testX)
        {
            double expected = dist.CumulativeDistribution(x);
            double actual = Lognormdist.StaticCdf(x, mu, sigma);
            Assert.Equal(expected, actual, ApproxTolerance);
        }
    }

    // ─── Output bounded [0,1] with streaming indicator ───────────────────────

    [Fact]
    public void LognormdistCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 85001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Lognormdist(mu: 0.0, sigma: 1.0, period: 20);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── Flat range → finite output ──────────────────────────────────────────

    [Fact]
    public void LognormdistCdf_FlatRange_IsFinite()
    {
        var ind = new Lognormdist(mu: 0.0, sigma: 1.0, period: 10);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.Last.Value >= 0.0 && ind.Last.Value <= 1.0);
    }

    // ─── NormalCdf internal correctness ──────────────────────────────────────

    [Fact]
    public void NormalCdf_AtZero_IsHalf()
    {
        double v = Lognormdist.NormalCdf(0.0);
        Assert.Equal(0.5, v, ApproxTolerance);
    }

    [Fact]
    public void NormalCdf_AtLargePositive_ApproachesOne()
    {
        double v = Lognormdist.NormalCdf(10.0);
        Assert.True(v > 0.9999, $"Φ(10) should approach 1, got {v}");
    }

    [Fact]
    public void NormalCdf_AtLargeNegative_ApproachesZero()
    {
        double v = Lognormdist.NormalCdf(-10.0);
        Assert.True(v < 1e-4, $"Φ(-10) should approach 0, got {v}");
    }

    [Fact]
    public void NormalCdf_IsSymmetric()
    {
        // Φ(z) + Φ(-z) = 1
        double[] testZ = { 0.5, 1.0, 1.5, 2.0, 3.0 };
        foreach (double z in testZ)
        {
            double pos = Lognormdist.NormalCdf(z);
            double neg = Lognormdist.NormalCdf(-z);
            Assert.Equal(1.0, pos + neg, ApproxTolerance);
        }
    }

    // ─── Parameter combos all within [0,1] ────────────────────────────────────

    [Theory]
    [InlineData(0.0,  1.0,  5)]
    [InlineData(0.0,  1.0, 14)]
    [InlineData(-1.0, 0.5, 10)]
    [InlineData(0.0,  2.0, 20)]
    [InlineData(1.0,  1.0, 30)]
    public void LognormdistCdf_ParameterCombos_OutputBounded(double mu, double sigma, int period)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 85002 + (int)(sigma * 100));
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Lognormdist(mu, sigma, period);

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
    public void LognormdistCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 85003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Lognormdist(mu: 0.0, sigma: 1.0, period: 50);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Span batch vs TSeries consistency ────────────────────────────────────

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int count = 150;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 85004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Lognormdist.Batch(bars.Close, period: 30);
        double[] spanResult = new double[count];
        Lognormdist.Batch(rawValues, spanResult, period: 30);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], 1e-10);
        }
    }

    // ─── High-period streaming convergence ────────────────────────────────────

    [Fact]
    public void LognormdistCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Lognormdist(mu: 0.0, sigma: 1.0, period: period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 85005);
        var bars = gbm.Fetch(period + 50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Non-finite output at bar {i}");
        }
    }

    // ─── MathNet parameter sweep ──────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(0.0, 1.0)]
    [InlineData(0.0, 2.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(-1.0, 0.5)]
    public void StaticCdf_SweepX_VsMathNet(double mu, double sigma)
    {
        var dist = new LogNormal(mu, sigma);
        double[] xs = { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0 };

        foreach (double x in xs)
        {
            double expected = dist.CumulativeDistribution(x);
            double actual = Lognormdist.StaticCdf(x, mu, sigma);
            Assert.Equal(expected, actual, ApproxTolerance);
        }
    }
}
