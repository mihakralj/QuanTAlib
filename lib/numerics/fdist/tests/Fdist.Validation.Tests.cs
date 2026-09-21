using Xunit;
using MathNet.Numerics.Distributions;

namespace QuanTAlib.Tests;

/// <summary>
/// FdistValidationTests — validates against known mathematical properties
/// of the F-Distribution CDF and against MathNet.Numerics FisherSnedecor.
/// Known-value tests call Fdist.FCdf directly (bypassing windowing) so results
/// are exact closed-form comparisons with tolerance 1e-9.
/// </summary>
public class FdistValidationTests
{
    private const double Tolerance = 1e-9;
    private const double LooseTolerance = 1e-6;

    // ─── Known-value tests via FCdf static method vs MathNet ─────────────────
    // F(x; d1, d2) = I(d1*x/(d1*x+d2), d1/2, d2/2)

    [Theory]
    [InlineData(0.0, 1, 1)]           // F(0; d1, d2) = 0 always
    [InlineData(0.0, 5, 5)]
    [InlineData(0.0, 10, 2)]
    public void FCdf_AtZero_IsAlwaysZero(double x, int d1, int d2)
    {
        Assert.Equal(0.0, Fdist.FCdf(x, d1, d2), Tolerance);
    }

    [Theory]
    [InlineData(-0.1, 1, 1)]
    [InlineData(-1.0, 5, 5)]
    [InlineData(-100.0, 2, 3)]
    public void FCdf_Negative_IsAlwaysZero(double x, int d1, int d2)
    {
        Assert.Equal(0.0, Fdist.FCdf(x, d1, d2), Tolerance);
    }

    [Theory]
    [InlineData(100.0, 1, 1, 0.90)]    // F(1,1) is heavy-tailed; F(100) ≈ 0.936
    [InlineData(100.0, 5, 5, 0.99)]
    [InlineData(100.0, 10, 2, 0.99)]
    public void FCdf_AtLargeX_ApproachesOne(double x, int d1, int d2, double minExpected)
    {
        double cdf = Fdist.FCdf(x, d1, d2);
        Assert.True(cdf > minExpected, $"F({x}; {d1},{d2}) = {cdf} should be > {minExpected}");
    }

    // ─── MathNet.Numerics cross-validation ───────────────────────────────────

    [Theory]
    [InlineData(1.0, 1, 1)]
    [InlineData(2.0, 1, 1)]
    [InlineData(0.5, 2, 3)]
    [InlineData(1.5, 5, 5)]
    [InlineData(0.8, 10, 2)]
    [InlineData(3.0, 3, 7)]
    [InlineData(0.25, 2, 10)]
    [InlineData(5.0, 5, 10)]
    [InlineData(0.1, 1, 5)]
    [InlineData(2.5, 8, 4)]
    public void FCdf_VsMathNet_KnownValues(double x, int d1, int d2)
    {
        var dist = new FisherSnedecor(d1, d2);
        double expected = dist.CumulativeDistribution(x);
        double actual = Fdist.FCdf(x, d1, d2);
        Assert.Equal(expected, actual, Tolerance);
    }

    [Theory]
    [InlineData(1.0, 1, 1)]
    [InlineData(2.0, 5, 5)]
    [InlineData(0.5, 2, 3)]
    [InlineData(1.5, 10, 10)]
    [InlineData(0.8, 3, 7)]
    public void StaticCdf_VsMathNet_KnownValues(double x, int d1, int d2)
    {
        var dist = new FisherSnedecor(d1, d2);
        double expected = dist.CumulativeDistribution(x);
        double actual = Fdist.StaticCdf(x, d1, d2);
        Assert.Equal(expected, actual, Tolerance);
    }

    // ─── Monotonicity ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(2, 10)]
    [InlineData(10, 3)]
    public void FCdf_MonotonicIncreasing(int d1, int d2)
    {
        double prev = -1.0;

        for (int i = 0; i <= 30; i++)
        {
            double x = i * 0.2;
            double cdf = Fdist.FCdf(x, d1, d2);
            Assert.True(cdf >= prev - LooseTolerance,
                $"CDF not monotonic at x={x} (d1={d1}, d2={d2}): got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    // ─── Output bounded [0, 1] ────────────────────────────────────────────────

    [Fact]
    public void FdistCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 66001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Fdist(d1: 5, d2: 5, period: 20);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── Flat range → F-CDF at 5.0 (xNorm=0.5, xF=5) ────────────────────────

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(2, 3)]
    [InlineData(10, 5)]
    public void FdistCdf_FlatRange_ReturnsCdfAtFive(int d1, int d2)
    {
        var ind = new Fdist(d1, d2, period: 20);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        double expected = Fdist.FCdf(5.0, d1, d2);
        Assert.Equal(expected, ind.Last.Value, LooseTolerance);
    }

    // ─── Streaming vs MathNet on raw (unnormalized) values ───────────────────

    [Fact]
    public void FCdf_MultiplePoints_AllMatchMathNet()
    {
        int d1 = 5, d2 = 5;
        var dist = new FisherSnedecor(d1, d2);

        double[] testX = { 0.0, 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 };

        foreach (double x in testX)
        {
            double expected = dist.CumulativeDistribution(x);
            double actual = Fdist.FCdf(x, d1, d2);
            Assert.Equal(expected, actual, Tolerance);
        }
    }

    // ─── Span batch consistency ───────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int count = 150;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 66002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Fdist.Batch(bars.Close, d1: 5, d2: 5, period: 30);
        double[] spanResult = new double[count];
        Fdist.Batch(rawValues, spanResult, d1: 5, d2: 5, period: 30);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void FdistCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Fdist(d1: 5, d2: 5, period: period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 66003);
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
    [InlineData(1, 1, 5)]
    [InlineData(2, 3, 14)]
    [InlineData(5, 5, 20)]
    [InlineData(10, 2, 30)]
    [InlineData(1, 10, 10)]
    public void FdistCdf_ParameterCombos_OutputBounded(int d1, int d2, int period)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 66004 + (d1 * 100) + d2);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Fdist(d1, d2, period);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0,
                $"Out of [0,1] at bar {i}: {v} (d1={d1}, d2={d2}, period={period})");
        }
    }

    // ─── Large dataset stable ─────────────────────────────────────────────────

    [Fact]
    public void FdistCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 66005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Fdist(d1: 5, d2: 5, period: 50);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Complementary property F(x;d1,d2) = 1 - G(1/x;d2,d1) ──────────────

    [Theory]
    [InlineData(0.5, 5, 5)]
    [InlineData(1.0, 3, 7)]
    [InlineData(2.0, 2, 4)]
    [InlineData(0.25, 4, 8)]
    public void FCdf_ComplementaryProperty(double x, int d1, int d2)
    {
        // F(x; d1, d2) = 1 - F(1/x; d2, d1) — the reciprocal (swapped-DoF) relation
        double direct = Fdist.FCdf(x, d1, d2);
        // Use local variables to avoid S2234 name-order false positive when intentionally swapping d1/d2
        double xRecip = 1.0 / x;
        int swappedD1 = d2;
        int swappedD2 = d1;
        double reciprocal = Fdist.FCdf(xRecip, swappedD1, swappedD2);
        Assert.Equal(1.0, direct + reciprocal, LooseTolerance);
    }

    // ─── Symmetric case (d1=d2=n) median near 1 ──────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void FCdf_SymmetricDoF_MedianIsOne(int n)
    {
        // When d1==d2, the F distribution median is 1.0 (approx) → CDF(1) ≈ 0.5
        double cdf = Fdist.FCdf(1.0, n, n);
        Assert.Equal(0.5, cdf, 1e-6);
    }

    // ─── Extreme prices don't blow up ─────────────────────────────────────────

    [Fact]
    public void FdistCdf_ExtremePrices_StillInRange()
    {
        var indicator = new Fdist(d1: 5, d2: 5, period: 20);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double price = (i % 2 == 0) ? 1e10 : 1e-10;
            indicator.Update(new TValue(time.AddMinutes(i), price));
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Out of range at {i}: {v}");
        }
    }
}
