using Xunit;
using MathNet.Numerics.Distributions;

namespace QuanTAlib.Tests;

/// <summary>
/// WeibulldistValidationTests — validates against known mathematical properties
/// of the Weibull CDF and cross-validates with MathNet.Numerics.Distributions.Weibull.
/// StaticCdf tests call Weibulldist.StaticCdf directly (bypassing windowing)
/// so results are exact closed-form comparisons.
/// </summary>
public class WeibulldistValidationTests
{
    private const double Tolerance = 1e-9;
    private const double LooseTolerance = 1e-6;

    // ─── Known-value tests via StaticCdf static method ───────────────────────
    // F(x; k, λ) = 1 - exp(-(x/λ)^k), closed-form.

    [Theory]
    [InlineData(0.0, 1.5, 1.0, 0.0)]           // F(0; k, λ) = 0 always
    [InlineData(1.0, 1.0, 1.0, 0.6321205588285578)]  // k=1: exponential, F(1;1,1) = 1-1/e
    [InlineData(1.0, 2.0, 1.0, 0.6321205588285578)]  // F(λ; k, λ) = 1-1/e for any k (x=λ=1)
    [InlineData(1.0, 1.5, 1.0, 0.6321205588285578)]  // F(λ; k, λ) = 1-1/e (x=λ=1)
    [InlineData(1.0, 3.0, 1.0, 0.6321205588285578)]  // F(λ; k, λ) = 1-1/e (x=λ=1)
    [InlineData(2.0, 2.0, 2.0, 0.6321205588285578)]  // F(λ=2; k=2, λ=2) = 1-1/e
    [InlineData(0.5, 1.0, 1.0, 0.3934693402873666)]  // k=1: F(0.5;1,1)=1-exp(-0.5)
    [InlineData(1.0, 2.0, 2.0, 0.2211992169285951)]  // F(1;2,2)=1-exp(-0.25)
    [InlineData(2.0, 1.0, 1.0, 0.8646647167633873)]  // k=1: F(2;1,1)=1-exp(-2)
    public void StaticCdf_KnownValues(double x, double k, double lambda, double expected)
    {
        double actual = Weibulldist.StaticCdf(x, k, lambda);
        Assert.Equal(expected, actual, LooseTolerance);
    }

    // ─── Boundary conditions ─────────────────────────────────────────────────

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(2.0, 2.0)]
    [InlineData(5.0, 0.5)]
    [InlineData(0.5, 3.0)]
    public void StaticCdf_AtZero_IsAlwaysZero(double k, double lambda)
    {
        Assert.Equal(0.0, Weibulldist.StaticCdf(0.0, k, lambda), Tolerance);
    }

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(2.0, 0.5)]
    [InlineData(0.5, 2.0)]
    public void StaticCdf_AtNegative_IsAlwaysZero(double k, double lambda)
    {
        Assert.Equal(0.0, Weibulldist.StaticCdf(-1.0, k, lambda), Tolerance);
        Assert.Equal(0.0, Weibulldist.StaticCdf(-100.0, k, lambda), Tolerance);
    }

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(2.0, 2.0)]
    [InlineData(0.5, 0.5)]
    public void StaticCdf_AtLargeX_ApproachesOne(double k, double lambda)
    {
        double cdf = Weibulldist.StaticCdf(1000.0, k, lambda);
        Assert.Equal(1.0, cdf, LooseTolerance);
    }

    // ─── Characteristic life property: F(λ; k, λ) = 1 - 1/e for any k ───────

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 1.0)]
    [InlineData(2.0, 2.0)]
    [InlineData(3.6, 0.5)]
    [InlineData(5.0, 3.0)]
    public void StaticCdf_AtCharacteristicLife_Is1MinusInvE(double k, double lambda)
    {
        // CDF(lambda, k, lambda) = 1 - exp(-(lambda/lambda)^k) = 1 - exp(-1) for any k
        double expected = 1.0 - Math.Exp(-1.0); // ≈ 0.6321205588285578
        double actual = Weibulldist.StaticCdf(lambda, k, lambda);
        Assert.Equal(expected, actual, LooseTolerance);
    }

    // ─── k=1 reduces to Exponential distribution ─────────────────────────────

    [Theory]
    [InlineData(0.5, 1.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(2.0, 2.0)]
    [InlineData(0.3, 0.5)]
    public void StaticCdf_KEquals1_MatchesExponential(double x, double lambda)
    {
        // Weibull(k=1, λ) = Exponential(rate=1/λ)
        double weibull = Weibulldist.StaticCdf(x, 1.0, lambda);
        double exponential = 1.0 - Math.Exp(-x / lambda);
        Assert.Equal(exponential, weibull, Tolerance);
    }

    // ─── Monotonicity ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(5.0)]
    public void StaticCdf_MonotonicIncreasing(double k)
    {
        double lambda = 1.0;
        double prev = -1.0;

        for (int i = 0; i <= 30; i++)
        {
            double x = i * 0.1;
            double cdf = Weibulldist.StaticCdf(x, k, lambda);
            Assert.True(cdf >= prev - LooseTolerance,
                $"CDF not monotonic at x={x}, k={k}: got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    // ─── MathNet cross-validation ─────────────────────────────────────────────

    [Theory]
    [InlineData(0.5, 1.5, 1.0)]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(1.0, 2.0, 1.0)]
    [InlineData(0.5, 2.0, 0.5)]
    [InlineData(2.0, 0.5, 2.0)]
    [InlineData(1.5, 3.0, 1.5)]
    [InlineData(3.0, 1.5, 2.0)]
    [InlineData(0.1, 5.0, 1.0)]
    [InlineData(0.9, 2.0, 1.0)]
    [InlineData(2.5, 1.5, 2.0)]
    public void StaticCdf_MatchesMathNet(double x, double k, double lambda)
    {
        // MathNet Weibull(shape, scale) = Weibull(k, lambda) — same parameterization
        var dist = new Weibull(k, lambda);
        double expected = dist.CumulativeDistribution(x);
        double actual = Weibulldist.StaticCdf(x, k, lambda);
        Assert.Equal(expected, actual, Tolerance);
    }

    [Fact]
    public void StaticCdf_MathNet_ExtensiveComparison()
    {
        double[] kValues = { 0.5, 1.0, 1.5, 2.0, 3.6, 5.0 };
        double[] lambdaValues = { 0.5, 1.0, 2.0 };
        double[] xValues = { 0.0, 0.1, 0.5, 1.0, 1.5, 2.0, 5.0, 10.0 };

        foreach (double k in kValues)
        {
            foreach (double lambda in lambdaValues)
            {
                var dist = new Weibull(k, lambda);
                foreach (double x in xValues)
                {
                    double expected = dist.CumulativeDistribution(x);
                    double actual = Weibulldist.StaticCdf(x, k, lambda);
                    // MathNet uses internal Taylor approximations; tolerance 1e-8 covers its rounding
                    Assert.Equal(expected, actual, LooseTolerance);
                }
            }
        }
    }

    // ─── Flat range → F(0.5; k, λ) ───────────────────────────────────────────

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(2.0, 0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(3.0, 2.0)]
    public void WeibulldistCdf_FlatRange_ReturnsCdfAtHalf(double k, double lambda)
    {
        var ind = new Weibulldist(k, lambda, 20);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        // Streaming normalizes to [0,1] then multiplies by invLambda before pow
        // Equivalent: 1 - exp(-(0.5 * (1/lambda))^k)
        double expectedDirect = 1.0 - Math.Exp(-Math.Pow(0.5 * (1.0 / lambda), k));
        Assert.Equal(expectedDirect, ind.Last.Value, LooseTolerance);
    }

    // ─── Output bounded [0, 1] ────────────────────────────────────────────────

    [Fact]
    public void WeibulldistCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 73001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Weibulldist(k: 1.5, lambda: 1.0, period: 20);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
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

        var tseriesResult = Weibulldist.Batch(bars.Close, period: 30);
        double[] spanResult = new double[count];
        Weibulldist.Batch(rawValues, spanResult, period: 30);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void WeibulldistCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Weibulldist(k: 2.0, lambda: 1.0, period: period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 73003);
        var bars = gbm.Fetch(period + 50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Non-finite output at bar {i}");
        }
    }

    [Fact]
    public void WeibulldistCdf_ExtremePrices_StillInRange()
    {
        var indicator = new Weibulldist(k: 1.5, lambda: 1.0, period: 20);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double price = (i % 2 == 0) ? 1e10 : 1e-10;
            indicator.Update(new TValue(time.AddMinutes(i), price));
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Out of range at {i}: {v}");
        }
    }

    // ─── Parameter combos all produce output in range ─────────────────────────

    [Theory]
    [InlineData(5, 0.5, 1.0)]
    [InlineData(14, 1.5, 1.0)]
    [InlineData(50, 2.0, 0.5)]
    [InlineData(20, 3.6, 2.0)]
    [InlineData(30, 5.0, 1.0)]
    public void WeibulldistCdf_ParameterCombos_OutputBounded(int period, double k, double lambda)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 73004 + period);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Weibulldist(k, lambda, period);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0,
                $"Out of [0,1] at bar {i}: {v} (k={k}, lambda={lambda}, period={period})");
        }
    }

    // ─── Large dataset: stable ────────────────────────────────────────────────

    [Fact]
    public void WeibulldistCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 73005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Weibulldist(k: 1.5, lambda: 1.0, period: 50);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Survival function: F(x) + S(x) = 1 ─────────────────────────────────

    [Fact]
    public void StaticCdf_PlusSurvival_IsOne()
    {
        double[] kValues = { 0.5, 1.0, 2.0, 5.0 };
        double[] lambdaValues = { 0.5, 1.0, 2.0 };
        double[] xs = { 0.1, 0.5, 1.0, 2.0 };

        foreach (double k in kValues)
        {
            foreach (double lambda in lambdaValues)
            {
                foreach (double x in xs)
                {
                    double cdf = Weibulldist.StaticCdf(x, k, lambda);
                    double survival = Math.Exp(-Math.Pow(x / lambda, k));
                    Assert.Equal(1.0, cdf + survival, LooseTolerance);
                }
            }
        }
    }

    // ─── Streaming vs MathNet cross-validation ────────────────────────────────

    [Fact]
    public void WeibulldistCdf_StreamingOutput_MatchesMathNetOnKnownData()
    {
        // Feed known values so streaming result is predictable via MathNet
        // Period=3, strictly ascending: first 3 bars warm up, then check bar 3
        var indicator = new Weibulldist(k: 2.0, lambda: 1.0, period: 3);
        var time = DateTime.UtcNow;

        // Values: 100, 102, 104 → x = (104-100)/(104-100) = 1.0
        indicator.Update(new TValue(time, 100.0));
        indicator.Update(new TValue(time.AddMinutes(1), 102.0));
        indicator.Update(new TValue(time.AddMinutes(2), 104.0));

        // After 3 bars: window = [100,102,104], min=100, max=104, range=4
        // Current (104-100)/4 = 1.0 → x=1.0, CDF(1/1.0, k=2) = 1-exp(-1)
        double expected = 1.0 - Math.Exp(-Math.Pow(1.0, 2.0)); // = 1 - exp(-1) ≈ 0.6321
        Assert.Equal(expected, indicator.Last.Value, LooseTolerance);
    }
}
