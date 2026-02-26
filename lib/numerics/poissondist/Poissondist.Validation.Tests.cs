using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Poissondist validation tests — validates CDF against exact mathematical properties
/// and MathNet.Numerics reference values. StaticCdf calls bypass windowing so results
/// are exact. Streaming/batch tests verify invariants that hold regardless of window state.
/// </summary>
public class PoissondistValidationTests
{
    private const double Tolerance = 1e-9;
    private const double LooseTolerance = 1e-6;

    // ─── Known CDF values ────────────────────────────────────────────────────
    // P(X <= k; λ) = e^(-λ) * Σ[j=0 to k] (λ^j / j!)

    [Theory]
    // P(X<=0; λ=1) = e^(-1) ≈ 0.36787944117144233
    [InlineData(0, 1.0, 0.36787944117144233)]
    // P(X<=1; λ=1) = e^(-1) + e^(-1) = 2e^(-1) ≈ 0.73575888234288467
    [InlineData(1, 1.0, 0.73575888234288467)]
    // P(X<=2; λ=1) = e^(-1)(1 + 1 + 0.5) = 2.5e^(-1) ≈ 0.91969860292860584
    [InlineData(2, 1.0, 0.91969860292860584)]
    // P(X<=0; λ=0.5) = e^(-0.5) ≈ 0.60653065971263342
    [InlineData(0, 0.5, 0.60653065971263342)]
    // P(X<=0; λ=2) = e^(-2) ≈ 0.13533528323661270
    [InlineData(0, 2.0, 0.13533528323661270)]
    // P(X<=5; λ=5) = known value from tables ≈ 0.61596065
    [InlineData(5, 5.0, 0.61596065)]
    // P(X<=10; λ=5) ≈ 0.9863047314 (should be high for k >> lambda)
    [InlineData(10, 5.0, 0.9863047314)]
    public void StaticCdf_KnownValues(int k, double lambda, double expected)
    {
        double actual = Poissondist.StaticCdf(k, lambda);
        Assert.True(Math.Abs(actual - expected) < LooseTolerance,
            $"k={k}, λ={lambda}: expected {expected:G10}, got {actual:G10}");
    }

    // ─── Exact boundary values ────────────────────────────────────────────────

    [Fact]
    public void StaticCdf_K0_Lambda1_ExactEMinusOne()
    {
        // P(X=0; λ=1) = e^(-1) exactly
        double expected = Math.Exp(-1.0);
        double actual = Poissondist.StaticCdf(0, 1.0);
        Assert.Equal(expected, actual, Tolerance);
    }

    [Fact]
    public void StaticCdf_K1_Lambda1_Exact2EMinusOne()
    {
        // P(X<=1; λ=1) = e^(-1) + e^(-1) = 2e^(-1)
        double expected = 2.0 * Math.Exp(-1.0);
        double actual = Poissondist.StaticCdf(1, 1.0);
        Assert.Equal(expected, actual, Tolerance);
    }

    [Fact]
    public void StaticCdf_K2_Lambda1_Exact2Point5EMinusOne()
    {
        // P(X<=2; λ=1) = e^(-1)(1 + 1 + 1/2) = 2.5e^(-1)
        double expected = 2.5 * Math.Exp(-1.0);
        double actual = Poissondist.StaticCdf(2, 1.0);
        Assert.Equal(expected, actual, Tolerance);
    }

    [Fact]
    public void StaticCdf_KNegative_ReturnsZero()
    {
        Assert.Equal(0.0, Poissondist.StaticCdf(-1, 1.0), Tolerance);
        Assert.Equal(0.0, Poissondist.StaticCdf(-10, 5.0), Tolerance);
    }

    [Fact]
    public void StaticCdf_LambdaZero_ReturnsOne()
    {
        // λ=0: degenerate case, all mass at X=0, P(X<=k) = 1 for k>=0
        Assert.Equal(1.0, Poissondist.StaticCdf(0, 0.0), Tolerance);
        Assert.Equal(1.0, Poissondist.StaticCdf(5, 0.0), Tolerance);
    }

    [Fact]
    public void StaticCdf_VeryLargeK_NearOne()
    {
        // For large k >> lambda, CDF → 1
        double cdf = Poissondist.StaticCdf(100, 1.0);
        Assert.True(Math.Abs(cdf - 1.0) < 1e-12,
            $"CDF(100, 1.0) should be ≈ 1.0, got {cdf}");
    }

    // ─── Monotonicity ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(0.5)]
    public void StaticCdf_Monotonic_InK(double lambda)
    {
        // CDF must be non-decreasing in k
        double prev = 0.0;
        for (int k = 0; k <= 20; k++)
        {
            double cdf = Poissondist.StaticCdf(k, lambda);
            Assert.True(cdf >= prev - 1e-12,
                $"CDF not monotonic at k={k}, λ={lambda}: got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    // ─── Output bounds ─────────────────────────────────────────────────────────

    [Fact]
    public void StaticCdf_OutputAlwaysInZeroOne()
    {
        double[] lambdas = { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0 };
        int[] ks = { 0, 1, 2, 5, 10, 20, 50, 100 };

        foreach (double lambda in lambdas)
        {
            foreach (int k in ks)
            {
                double cdf = Poissondist.StaticCdf(k, lambda);
                Assert.True(cdf >= 0.0 && cdf <= 1.0,
                    $"CDF({k}, {lambda}) = {cdf} out of [0,1]");
            }
        }
    }

    [Fact]
    public void Streaming_OutputAlwaysBounded()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 72001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Poissondist(lambda: 5.0, period: 20, threshold: 5);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── MathNet.Numerics cross-validation ──────────────────────────────────

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.0)]
    [InlineData(3, 1.0)]
    [InlineData(0, 5.0)]
    [InlineData(3, 5.0)]
    [InlineData(5, 5.0)]
    [InlineData(10, 5.0)]
    [InlineData(0, 10.0)]
    [InlineData(8, 10.0)]
    [InlineData(10, 10.0)]
    [InlineData(15, 10.0)]
    public void StaticCdf_VsMathNet(int k, double lambda)
    {
        var dist = new MathNet.Numerics.Distributions.Poisson(lambda);
        double expected = dist.CumulativeDistribution(k);
        double actual = Poissondist.StaticCdf(k, lambda);
        Assert.True(Math.Abs(actual - expected) < Tolerance,
            $"k={k}, λ={lambda}: MathNet={expected:G12}, QuanTAlib={actual:G12}, diff={Math.Abs(actual - expected):G4}");
    }

    // ─── PMF derivation ──────────────────────────────────────────────────────

    [Fact]
    public void StaticCdf_PmfDerived_Lambda1()
    {
        // PMF(0; 1) = e^(-1) ≈ 0.36788
        // PMF(1; 1) = e^(-1) ≈ 0.36788
        // PMF(2; 1) = 0.5*e^(-1) ≈ 0.18394
        double pmf0 = Poissondist.StaticCdf(0, 1.0);
        double pmf1 = Poissondist.StaticCdf(1, 1.0) - Poissondist.StaticCdf(0, 1.0);
        double pmf2 = Poissondist.StaticCdf(2, 1.0) - Poissondist.StaticCdf(1, 1.0);

        Assert.Equal(Math.Exp(-1.0), pmf0, Tolerance);
        Assert.Equal(Math.Exp(-1.0), pmf1, Tolerance);
        Assert.Equal(0.5 * Math.Exp(-1.0), pmf2, Tolerance);
    }

    [Fact]
    public void StaticCdf_PmfSumsToOne_Lambda5()
    {
        // Sum of PMF(k; 5) for k=0..50 should be ≈ 1
        double sum = 0.0;
        double prev = 0.0;
        for (int k = 0; k <= 50; k++)
        {
            double cdf = Poissondist.StaticCdf(k, 5.0);
            sum += cdf - prev;
            prev = cdf;
        }
        // Highest PMF k values will be missing but should be negligible at k=50 for λ=5
        Assert.True(Math.Abs(sum - 1.0) < 1e-10,
            $"PMF sum {sum} deviates from 1.0 by {Math.Abs(sum - 1.0):G4}");
    }

    // ─── Flat range → neutral CDF ─────────────────────────────────────────────

    [Fact]
    public void Streaming_FlatRange_ReturnsNeutralCdf()
    {
        // Flat range → x=0.5, λ = lambdaScale*0.5 = 5.0*0.5 = 2.5
        double expectedLambda = 2.5;
        double expectedCdf = Poissondist.StaticCdf(5, expectedLambda);

        var ind = new Poissondist(lambda: 5.0, period: 10, threshold: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        Assert.Equal(expectedCdf, ind.Last.Value, LooseTolerance);
    }

    // ─── Span batch consistency ───────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int count = 150;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 72002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Poissondist.Batch(bars.Close, lambda: 5.0, period: 30, threshold: 5);
        double[] spanResult = new double[count];
        Poissondist.Batch(rawValues, spanResult, lambda: 5.0, period: 30, threshold: 5);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Large lambda stability ────────────────────────────────────────────────

    [Fact]
    public void StaticCdf_LargeLambda_Stable()
    {
        // λ=100, k=100: CDF should be ≈ 0.51 (slightly above median for Poisson)
        double cdf = Poissondist.StaticCdf(100, 100.0);
        Assert.True(double.IsFinite(cdf) && cdf >= 0.0 && cdf <= 1.0,
            $"Large λ CDF invalid: {cdf}");
        Assert.True(cdf > 0.4 && cdf < 0.65, $"CDF={cdf:G4} expected near 0.51 for k=λ=100");
    }

    [Fact]
    public void Streaming_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Poissondist(lambda: 10.0, period: 50, threshold: 10);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Different parameter combos all produce output in range ──────────────

    [Theory]
    [InlineData(0.5, 5, 2)]
    [InlineData(1.0, 14, 5)]
    [InlineData(5.0, 50, 5)]
    [InlineData(10.0, 100, 10)]
    [InlineData(0.1, 30, 0)]
    public void Streaming_ParameterCombos_OutputBounded(double lambda, int period, int threshold)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72004 + period);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Poissondist(lambda, period, threshold);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0,
                $"λ={lambda}, period={period}, k={threshold}: output {v} out of [0,1]");
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void Streaming_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Poissondist(lambda: 5.0, period: period, threshold: 5);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 72005);
        var bars = gbm.Fetch(period + 50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Non-finite output at bar {i}");
        }
    }

    // ─── Poisson-Gamma identity verification ─────────────────────────────────

    [Theory]
    [InlineData(3, 2.0)]
    [InlineData(5, 5.0)]
    [InlineData(0, 1.0)]
    [InlineData(10, 3.0)]
    public void PoissonCdf_MatchesGammaIdentity(int k, double lambda)
    {
        // P(X<=k; λ) = 1 - RegIncGamma(k+1, λ)
        double fromPoisson = Poissondist.StaticCdf(k, lambda);
        double lnG = Poissondist.LnGamma(k + 1.0);
        double fromGamma = 1.0 - Poissondist.RegularizedIncompleteGamma(k + 1.0, lambda, lnG);
        Assert.Equal(fromPoisson, fromGamma, Tolerance);
    }
}
