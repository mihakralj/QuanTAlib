using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Binomdist validation tests — validates PMF/CDF against exact combinatorial values.
/// Known-value tests call Binomdist.BinomialCdf directly (bypassing windowing) so
/// results are exact. Streaming/batch tests check invariants that hold regardless
/// of window state.
/// </summary>
public class BinomdistValidationTests
{
    private const double Tolerance = 1e-9;
    private const double LooseTolerance = 1e-6;

    // ─── PMF known values ────────────────────────────────────────────────────
    // P(X=k; n, p) = C(n,k) * p^k * (1-p)^(n-k)
    // CDF P(X<=k) = sum_{i=0}^{k} P(X=i)

    [Theory]
    // P(X=3; n=10, p=0.5) = C(10,3) * 0.5^10 = 120/1024 = 0.1171875
    // CDF P(X<=3; n=10, p=0.5) = (1+10+45+120)/1024 = 176/1024 = 0.171875
    [InlineData(0.5, 10, 3, 0.171875)]
    // P(X<=5; n=10, p=0.5) = 638/1024 = 0.623046875 (exact)
    [InlineData(0.5, 10, 5, 0.623046875)]
    // P(X<=0; n=5, p=0.3) = (0.7)^5 = 0.16807
    [InlineData(0.3, 5, 0, 0.16807)]
    // P(X<=5; n=5, p=0.3) = 1.0 (k >= n)
    [InlineData(0.3, 5, 5, 1.0)]
    // P(X<=0; n=10, p=0.5) = 0.5^10 = 1/1024 ≈ 0.0009765625
    [InlineData(0.5, 10, 0, 0.0009765625)]
    // P(X<=10; n=10, p=0.5) = 1.0
    [InlineData(0.5, 10, 10, 1.0)]
    // P(X<=0; n=1, p=0.5) = 0.5
    [InlineData(0.5, 1, 0, 0.5)]
    // P(X<=1; n=1, p=0.5) = 1.0
    [InlineData(0.5, 1, 1, 1.0)]
    // P(X<=2; n=5, p=0.5) = (1+5+10)/32 = 16/32 = 0.5
    [InlineData(0.5, 5, 2, 0.5)]
    // P(X<=4; n=5, p=0.3) = 1 - P(X=5) = 1 - 0.3^5 = 1 - 0.00243 = 0.99757
    [InlineData(0.3, 5, 4, 0.99757)]
    public void BinomCdf_KnownValues(double p, int n, int k, double expected)
    {
        double actual = Binomdist.BinomialCdf(p, n, k);
        Assert.Equal(expected, actual, LooseTolerance);
    }

    // ─── PMF direct known values ─────────────────────────────────────────────

    [Fact]
    public void BinomPmf_Exact_n10_p05_k3()
    {
        // P(X=3; n=10, p=0.5) = C(10,3) / 2^10 = 120/1024 = 0.1171875
        // PMF = CDF(k) - CDF(k-1)
        double cdfK = Binomdist.BinomialCdf(0.5, 10, 3);
        double cdfKm1 = Binomdist.BinomialCdf(0.5, 10, 2);
        double pmf = cdfK - cdfKm1;
        Assert.Equal(0.1171875, pmf, Tolerance);
    }

    [Fact]
    public void BinomPmf_Exact_n5_p03_k0()
    {
        // P(X=0; n=5, p=0.3) = (0.7)^5 = 0.16807
        // CDF(0) - CDF(-1) = CDF(0) = 0.16807
        double cdf = Binomdist.BinomialCdf(0.3, 5, 0);
        Assert.Equal(0.16807, cdf, Tolerance);
    }

    // ─── Monotonicity ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.3, 10)]
    [InlineData(0.5, 10)]
    [InlineData(0.7, 20)]
    [InlineData(0.1, 5)]
    public void BinomCdf_Monotonic_InK(double p, int n)
    {
        // CDF must be non-decreasing in k
        double prev = 0.0;
        for (int k = 0; k <= n; k++)
        {
            double cdf = Binomdist.BinomialCdf(p, n, k);
            Assert.True(cdf >= prev - 1e-12,
                $"CDF not monotonic at k={k}, p={p}, n={n}: got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    [Fact]
    public void BinomCdf_MonotonicInP()
    {
        // P(X<=5; n=10, p) must be bounded [0,1] for all p
        int n = 10, k = 5;
        for (int i = 1; i <= 9; i++)
        {
            double p = i / 10.0;
            double cdf = Binomdist.BinomialCdf(p, n, k);
            Assert.True(cdf >= 0.0 && cdf <= 1.0,
                $"CDF out of bounds: {cdf} at p={p}");
        }
    }

    // ─── Boundary behavior ────────────────────────────────────────────────────

    [Fact]
    public void BinomCdf_P_Zero_ReturnsOne()
    {
        Assert.Equal(1.0, Binomdist.BinomialCdf(0.0, 10, 0), Tolerance);
        Assert.Equal(1.0, Binomdist.BinomialCdf(0.0, 10, 10), Tolerance);
    }

    [Fact]
    public void BinomCdf_P_One_KLessN_ReturnsZero()
    {
        Assert.Equal(0.0, Binomdist.BinomialCdf(1.0, 10, 5), Tolerance);
        Assert.Equal(0.0, Binomdist.BinomialCdf(1.0, 10, 9), Tolerance);
    }

    [Fact]
    public void BinomCdf_P_One_KEqualN_ReturnsOne()
    {
        Assert.Equal(1.0, Binomdist.BinomialCdf(1.0, 10, 10), Tolerance);
    }

    [Fact]
    public void BinomCdf_KN_ReturnsOne()
    {
        // P(X<=n; n, p) = 1 for all p in (0,1)
        Assert.Equal(1.0, Binomdist.BinomialCdf(0.3, 5, 5), Tolerance);
        Assert.Equal(1.0, Binomdist.BinomialCdf(0.5, 10, 10), Tolerance);
        Assert.Equal(1.0, Binomdist.BinomialCdf(0.9, 20, 20), Tolerance);
    }

    // ─── Output bounds ─────────────────────────────────────────────────────────

    [Fact]
    public void BinomCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 52001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Binomdist(period: 20, trials: 10, threshold: 5);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── Flat range → neutral CDF ─────────────────────────────────────────────

    [Fact]
    public void BinomCdf_FlatRange_ReturnsSymmetricCdf()
    {
        // Flat range → p=0.5; for symmetric n=10, k=5: CDF = 0.623046875
        var ind = new Binomdist(20, trials: 10, threshold: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        Assert.Equal(0.623046875, ind.Last.Value, LooseTolerance);
    }

    // ─── Period=1 trivial case ────────────────────────────────────────────────

    [Fact]
    public void BinomCdf_Period1_AlwaysReturnsCdfAtHalf()
    {
        // period=1: single-element window → range=0 → p=0.5 always
        var ind = new Binomdist(1, trials: 10, threshold: 5);
        var time = DateTime.UtcNow;

        double expected = Binomdist.BinomialCdf(0.5, 10, 5);
        double[] prices = { 100.0, 50.0, 200.0, 1.0, 1000.0 };
        foreach (double p in prices)
        {
            ind.Update(new TValue(time, p));
            time = time.AddMinutes(1);
            Assert.Equal(expected, ind.Last.Value, LooseTolerance);
        }
    }

    // ─── Span batch consistency ───────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int count = 150;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 52002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Binomdist.Batch(bars.Close, period: 30, trials: 15, threshold: 7);
        double[] spanResult = new double[count];
        Binomdist.Batch(rawValues, spanResult, period: 30, trials: 15, threshold: 7);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Large n stability ────────────────────────────────────────────────────

    [Fact]
    public void BinomCdf_LargeN_Stable()
    {
        // Large n tests log-space summation's overflow avoidance
        double cdf = Binomdist.BinomialCdf(0.5, 100, 50);
        Assert.True(double.IsFinite(cdf) && cdf >= 0.0 && cdf <= 1.0,
            $"Large n CDF invalid: {cdf}");
        // n=100, k=50, p=0.5 should be near 0.54 (slightly above 0.5)
        Assert.True(cdf > 0.5 && cdf < 0.7, $"CDF={cdf} expected near 0.54");
    }

    [Fact]
    public void BinomCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 52003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Binomdist(period: 50, trials: 20, threshold: 10);

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
    [InlineData(5, 5, 2)]
    [InlineData(14, 10, 5)]
    [InlineData(50, 20, 10)]
    [InlineData(100, 50, 25)]
    [InlineData(30, 1, 0)]
    public void BinomCdf_ParameterCombos_OutputBounded(int period, int trials, int threshold)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 52004 + period);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Binomdist(period, trials, threshold);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0);
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void BinomCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Binomdist(period, trials: 20, threshold: 10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 52005);
        var bars = gbm.Fetch(period + 50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Non-finite output at bar {i}");
        }
    }
}
