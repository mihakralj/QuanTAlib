using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// BetaDist validation tests — validates against known mathematical properties
/// of the regularized incomplete beta function. Known-value tests call
/// Betadist.IncompleteBeta directly (bypassing windowing) so results are exact.
/// Streaming/batch tests use GBM data and check invariants (bounds, finiteness,
/// monotonicity, symmetry) that hold regardless of window state.
/// </summary>
public class BetadistValidationTests
{
    private const double Tolerance = 1e-9;
    private const double LooseTolerance = 1e-6;

    // ─── Mathematical invariants (invariant to normalization) ────────────────

    [Fact]
    public void BetaCdf_FlatRange_ReturnsHalf()
    {
        // When all window values are equal → range=0 → x=0.5
        // For symmetric distributions (alpha=beta), CDF(0.5) = 0.5
        double[] shapes = { 0.5, 1.0, 2.0, 3.0, 5.0 };
        var time = DateTime.UtcNow;

        foreach (double shape in shapes)
        {
            var ind = new Betadist(20, shape, shape);
            for (int i = 0; i < 20; i++)
            {
                ind.Update(new TValue(time.AddSeconds(i), 100.0));
            }

            Assert.Equal(0.5, ind.Last.Value, LooseTolerance);
        }
    }

    [Fact]
    public void BetaCdf_OutputBounded_Zero_To_One()
    {
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 51001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Betadist(period: 20, alpha: 2.0, beta: 2.0);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Output {v} at bar {i} out of [0,1]");
        }
    }

    // ─── Period=1 trivial case ────────────────────────────────────────────────

    [Fact]
    public void BetaCdf_Period1_AlwaysReturnsCdfAtHalf()
    {
        // period=1: single-element window → range=0 → x=0.5 always
        // CDF(0.5, 1, 1) = 0.5 exactly (uniform)
        var ind = new Betadist(1, 1.0, 1.0);
        var time = DateTime.UtcNow;

        double[] prices = { 100.0, 50.0, 200.0, 1.0, 1000.0 };
        foreach (double p in prices)
        {
            ind.Update(new TValue(time, p));
            time = time.AddMinutes(1);
            Assert.Equal(0.5, ind.Last.Value, LooseTolerance);
        }
    }

    // ─── Known-value tests via IncompleteBeta static method ──────────────────
    // These bypass windowing entirely and test the CDF math directly.

    [Theory]
    [InlineData(0.25, 2.0, 2.0, 0.15625)]    // Beta(2,2): I(0.25) = 0.15625
    [InlineData(0.5, 2.0, 2.0, 0.5)]        // Beta(2,2): I(0.5)  = 0.5 (symmetry)
    [InlineData(0.75, 2.0, 2.0, 0.84375)]    // Beta(2,2): I(0.75) = 0.84375
    [InlineData(0.5, 2.0, 3.0, 0.6875)]     // Beta(2,3): I(0.5)  = 0.6875
    [InlineData(0.5, 3.0, 2.0, 0.3125)]     // Beta(3,2): I(0.5)  = 0.3125
    [InlineData(0.5, 1.0, 1.0, 0.5)]        // Uniform:   I(0.5)  = 0.5
    [InlineData(0.25, 1.0, 1.0, 0.25)]       // Uniform:   I(0.25) = 0.25
    [InlineData(0.75, 1.0, 1.0, 0.75)]       // Uniform:   I(0.75) = 0.75
    public void BetaCdf_IncompleteBeta_KnownValues(double x, double alpha, double beta, double expected)
    {
        double actual = Betadist.IncompleteBeta(x, alpha, beta);
        Assert.Equal(expected, actual, LooseTolerance);
    }

    // ─── Complementary symmetry: I_x(a,b) + I_{1-x}(b,a) = 1 ───────────────

    [Theory]
    [InlineData(0.3, 2.0, 3.0)]
    [InlineData(0.7, 2.0, 3.0)]
    [InlineData(0.5, 1.5, 4.0)]
    [InlineData(0.2, 3.0, 5.0)]
    public void BetaCdf_ComplementarySymmetry(double x, double alpha, double beta)
    {
        double iab = Betadist.IncompleteBeta(x, alpha, beta);
        double iba = Betadist.IncompleteBeta(1.0 - x, beta, alpha);
        Assert.Equal(1.0, iab + iba, LooseTolerance);
    }

    // ─── Monotonicity via direct CDF ─────────────────────────────────────────

    [Fact]
    public void BetaCdf_MonotonicIncreasing()
    {
        // CDF must be non-decreasing as x increases from 0 to 1
        double alpha = 2.0, beta = 2.0;
        double prevCdf = -1.0;

        for (int i = 0; i <= 10; i++)
        {
            double x = i / 10.0 + 1e-10; // avoid exact 0
            x = Math.Min(x, 1.0 - 1e-10);
            double cdf = Betadist.IncompleteBeta(x, alpha, beta);

            Assert.True(cdf >= prevCdf - LooseTolerance,
                $"CDF not monotonic at x={x}: got {cdf}, prev={prevCdf}");
            prevCdf = cdf;
        }
    }

    // ─── Span batch consistency ───────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int count = 150;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 51002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Betadist.Batch(bars.Close, period: 30);
        double[] spanResult = new double[count];
        Betadist.Batch(rawValues, spanResult, period: 30);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Streaming convergence ────────────────────────────────────────────────

    [Fact]
    public void BetaCdf_HighPeriod_StillConverges()
    {
        int period = 200;
        var indicator = new Betadist(period, 2.0, 5.0);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 51003);
        var bars = gbm.Fetch(period + 50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < bars.Close.Count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(double.IsFinite(indicator.Last.Value),
                $"Non-finite output at bar {i}");
        }
    }

    [Fact]
    public void BetaCdf_ExtremePrices_StillInRange()
    {
        var indicator = new Betadist(period: 20, alpha: 2.0, beta: 2.0);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            double price = (i % 2 == 0) ? 1e10 : 1e-10;
            indicator.Update(new TValue(time.AddMinutes(i), price));
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"Out of range at {i}: {v}");
        }
    }

    // ─── Different parameter combos all produce output in range ──────────────

    [Theory]
    [InlineData(5, 0.5, 0.5)]
    [InlineData(14, 1.0, 1.0)]
    [InlineData(50, 2.0, 2.0)]
    [InlineData(100, 3.0, 5.0)]
    [InlineData(30, 0.5, 2.0)]
    public void BetaCdf_ParameterCombos_OutputBounded(int period, double alpha, double beta)
    {
        int count = period + 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 51004 + period);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Betadist(period, alpha, beta);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(v >= 0.0 && v <= 1.0);
        }
    }

    // ─── Large dataset: stable ────────────────────────────────────────────────

    [Fact]
    public void BetaCdf_LargeDataset_Stable()
    {
        int count = 2000;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 51005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Betadist(period: 50);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0 && v <= 1.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Alpha != Beta produces asymmetric CDF ───────────────────────────────

    [Fact]
    public void BetaCdf_AsymmetricParams_SkewsOutput()
    {
        // Beta(0.5, 5): mode near 0, most mass below 0.5 → CDF(0.5) > 0.5
        // Beta(5, 0.5): mode near 1, most mass above 0.5 → CDF(0.5) < 0.5
        double cdfLow = Betadist.IncompleteBeta(0.5, 0.5, 5.0);
        double cdfHigh = Betadist.IncompleteBeta(0.5, 5.0, 0.5);

        Assert.True(cdfLow > cdfHigh,
            $"Beta(0.5,5) CDF at 0.5 ({cdfLow:F6}) should be > Beta(5,0.5) ({cdfHigh:F6})");
        Assert.True(cdfLow > 0.5, $"Beta(0.5,5) CDF(0.5)={cdfLow} should be > 0.5");
        Assert.True(cdfHigh < 0.5, $"Beta(5,0.5) CDF(0.5)={cdfHigh} should be < 0.5");
    }
}
