using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// ExpdistValidationTests — validates against known mathematical properties
/// of the exponential CDF. Known-value tests call Expdist.ExpCdf directly
/// (bypassing windowing) so results are exact closed-form comparisons.
/// Streaming/batch tests check invariants (bounds, monotonicity, finiteness)
/// that hold regardless of window state.
/// </summary>
public class ExpdistValidationTests
{
    private const double Tolerance = 1e-9;
    private const double LooseTolerance = 1e-6;

    // ─── Known-value tests via ExpCdf static method ──────────────────────────
    // F(x; λ) = 1 - exp(-λx), closed-form, no special functions.

    [Theory]
    [InlineData(0.0, 1.0, 0.0)]                          // F(0; 1) = 0
    [InlineData(1.0, 1.0, 0.6321205588285578)]           // F(1; 1) = 1 - 1/e
    [InlineData(2.0, 1.0, 0.8646647167633873)]           // F(2; 1) = 1 - exp(-2)
    [InlineData(0.5, 1.0, 0.3934693402873666)]           // F(0.5; 1) = 1 - exp(-0.5)
    [InlineData(1.0, 2.0, 0.8646647167633873)]           // F(1; 2) = 1 - exp(-2)
    [InlineData(0.5, 2.0, 0.6321205588285578)]           // F(0.5; 2) = 1 - 1/e
    [InlineData(1.0, 3.0, 0.9502129316321360)]           // F(1; 3) = 1 - exp(-3)
    [InlineData(0.5, 3.0, 0.7768698398515702)]           // F(0.5; 3) = 1 - exp(-1.5)
    [InlineData(0.0, 5.0, 0.0)]                          // F(0; 5) = 0 always
    public void ExpCdf_KnownValues(double x, double lambda, double expected)
    {
        double actual = Expdist.ExpCdf(x, lambda);
        Assert.Equal(expected, actual, LooseTolerance);
    }

    // ─── PDF known values ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, 2.0, 2.0)]      // f(0; 2) = 2
    [InlineData(0.0, 1.0, 1.0)]      // f(0; 1) = 1
    [InlineData(1.0, 1.0, 0.36787944117144233)]  // f(1; 1) = exp(-1)
    [InlineData(0.0, 0.5, 0.5)]      // f(0; 0.5) = 0.5
    public void ExpPdf_KnownValues(double x, double lambda, double expected)
    {
        double actual = Expdist.ExpPdf(x, lambda);
        Assert.Equal(expected, actual, LooseTolerance);
    }

    // ─── Boundary conditions ─────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void ExpCdf_AtZero_IsAlwaysZero(double lambda)
    {
        Assert.Equal(0.0, Expdist.ExpCdf(0.0, lambda), Tolerance);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void ExpCdf_AtNegative_IsAlwaysZero(double lambda)
    {
        Assert.Equal(0.0, Expdist.ExpCdf(-1.0, lambda), Tolerance);
        Assert.Equal(0.0, Expdist.ExpCdf(-100.0, lambda), Tolerance);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void ExpCdf_AtLargeX_ApproachesOne(double lambda)
    {
        double cdf = Expdist.ExpCdf(100.0, lambda);
        Assert.Equal(1.0, cdf, LooseTolerance);
    }

    // ─── Monotonicity ────────────────────────────────────────────────────────

    [Fact]
    public void ExpCdf_MonotonicIncreasing_Lambda1()
    {
        double lambda = 1.0;
        double prev = -1.0;

        for (int i = 0; i <= 20; i++)
        {
            double x = i * 0.1;
            double cdf = Expdist.ExpCdf(x, lambda);
            Assert.True(cdf >= prev - LooseTolerance,
                $"CDF not monotonic at x={x}: got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    [Fact]
    public void ExpCdf_MonotonicIncreasing_Lambda3()
    {
        double lambda = 3.0;
        double prev = -1.0;

        for (int i = 0; i <= 20; i++)
        {
            double x = i * 0.05;
            double cdf = Expdist.ExpCdf(x, lambda);
            Assert.True(cdf >= prev - LooseTolerance,
                $"CDF not monotonic at x={x}: got {cdf}, prev={prev}");
            prev = cdf;
        }
    }

    // ─── Higher λ -> faster rise ─────────────────────────────────────────────

    [Theory]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    public void ExpCdf_HigherLambda_HigherCdfForSamePositiveX(double x)
    {
        double cdf1 = Expdist.ExpCdf(x, 1.0);
        double cdf3 = Expdist.ExpCdf(x, 3.0);
        double cdf10 = Expdist.ExpCdf(x, 10.0);

        Assert.True(cdf3 > cdf1, $"λ=3 CDF({x})={cdf3} should exceed λ=1 CDF({x})={cdf1}");
        Assert.True(cdf10 > cdf3, $"λ=10 CDF({x})={cdf10} should exceed λ=3 CDF({x})={cdf3}");
    }

    // ─── Flat range → F(0.5; λ) ──────────────────────────────────────────────

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    public void ExpdistCdf_FlatRange_ReturnsCdfAtHalf(double lambda)
    {
        var ind = new Expdist(20, lambda);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        double expected = Expdist.ExpCdf(0.5, lambda);
        Assert.Equal(expected, ind.Last.Value, LooseTolerance);
    }

    // ─── Output bounded [0, 1] ────────────────────────────────────────────────

    [Fact]
    public void ExpdistCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 63001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Expdist(period: 20, lambda: 3.0);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── Period=1 trivial case ────────────────────────────────────────────────

    [Fact]
    public void ExpdistCdf_Period1_AlwaysReturnsCdfAtHalf()
    {
        // period=1: single-element window → range=0 → x=0.5 always
        var ind = new Expdist(1, 2.0);
        var time = DateTime.UtcNow;
        double expected = Expdist.ExpCdf(0.5, 2.0); // 1 - exp(-1) ≈ 0.6321

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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 63002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Expdist.Batch(bars.Close, period: 30);
        double[] spanResult = new double[count];
        Expdist.Batch(rawValues, spanResult, period: 30);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void ExpdistCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Expdist(period, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 63003);
        var bars = gbm.Fetch(period + 50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Non-finite output at bar {i}");
        }
    }

    [Fact]
    public void ExpdistCdf_ExtremePrices_StillInRange()
    {
        var indicator = new Expdist(period: 20, lambda: 3.0);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double price = (i % 2 == 0) ? 1e10 : 1e-10;
            indicator.Update(new TValue(time.AddMinutes(i), price));
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Out of range at {i}: {v}");
        }
    }

    // ─── CDF integrates to complement of survival function ───────────────────

    [Fact]
    public void ExpCdf_PlusSurvival_IsOne()
    {
        // F(x) + (1 - F(x)) = 1; survival = exp(-λx)
        double[] lambdas = { 0.5, 1.0, 2.0, 5.0 };
        double[] xs = { 0.1, 0.5, 1.0, 2.0 };

        foreach (double lambda in lambdas)
        {
            foreach (double x in xs)
            {
                double cdf = Expdist.ExpCdf(x, lambda);
                double survival = Math.Exp(-lambda * x);
                Assert.Equal(1.0, cdf + survival, LooseTolerance);
            }
        }
    }

    // ─── Different parameter combos all produce output in range ──────────────

    [Theory]
    [InlineData(5, 0.5)]
    [InlineData(14, 1.0)]
    [InlineData(50, 3.0)]
    [InlineData(100, 5.0)]
    [InlineData(30, 10.0)]
    public void ExpdistCdf_ParameterCombos_OutputBounded(int period, double lambda)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 63004 + period);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Expdist(period, lambda);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0,
                $"Out of [0,1] at bar {i}: {v} (period={period}, lambda={lambda})");
        }
    }

    // ─── Large dataset: stable ────────────────────────────────────────────────

    [Fact]
    public void ExpdistCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 63005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Expdist(period: 50);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Memoryless property: F(x+t) - F(x) / (1-F(x)) = F(t) ─────────────

    [Fact]
    public void ExpCdf_MemorylessProperty()
    {
        // P(X > s + t | X > s) = P(X > t) = exp(-λt)
        // Equivalently: (1 - F(s+t)) / (1 - F(s)) ≈ 1 - F(t)
        double lambda = 2.0;
        double s = 0.5;
        double t = 0.3;

        double fst = Expdist.ExpCdf(s + t, lambda);
        double fs = Expdist.ExpCdf(s, lambda);
        double ft = Expdist.ExpCdf(t, lambda);

        // (1 - F(s+t)) / (1 - F(s)) should equal (1 - F(t))
        double conditionalSurvival = (1.0 - fst) / (1.0 - fs);
        double expectedSurvival = 1.0 - ft;

        Assert.Equal(expectedSurvival, conditionalSurvival, LooseTolerance);
    }
}
