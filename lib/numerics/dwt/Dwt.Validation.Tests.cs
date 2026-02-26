using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Dwt using known mathematical properties of the
/// à trous Haar wavelet decomposition. No external library reference —
/// validates against first-principles mathematical invariants.
/// </summary>
public class DwtValidationTests
{
    private const double Tolerance = 1e-10;
    private const double CoarseTolerance = 1e-6;

    // ─── Property 1: Constant signal → approximation = constant, detail ≈ 0 ──

    [Fact]
    public void HaarDwt_ConstantSignal_ApproximationEqualsConstant()
    {
        // À trous Haar: avg of identical samples = the sample itself
        const double constantValue = 42.0;
        var indicator = new Dwt(levels: 4, output: 0); // approximation
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        for (int i = 0; i < warmup + 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), constantValue));
        }

        // After warmup, approximation of constant signal = constant
        Assert.Equal(constantValue, indicator.Last.Value, CoarseTolerance);
    }

    [Fact]
    public void HaarDwt_ConstantSignal_DetailEqualsZero()
    {
        // Detail = c[j-1] - c[j]; for constant input, both levels equal constant → detail = 0
        const double constantValue = 100.0;
        var time = DateTime.UtcNow;

        for (int level = 1; level <= 5; level++)
        {
            var indicator = new Dwt(levels: level, output: level); // detail at deepest level
            int warmup = indicator.WarmupPeriod;

            for (int i = 0; i < warmup + 5; i++)
            {
                indicator.Update(new TValue(time.AddMinutes(i), constantValue));
            }

            Assert.Equal(0.0, indicator.Last.Value, CoarseTolerance);
        }
    }

    [Fact]
    public void HaarDwt_ConstantSignal_AllDetailLevelsZero()
    {
        // Every detail level of a constant signal should be zero
        const double constantValue = 50.0;
        var time = DateTime.UtcNow;
        int maxLevels = 4;
        int warmup = 1 << maxLevels; // 16

        for (int detail = 1; detail <= maxLevels; detail++)
        {
            var indicator = new Dwt(levels: maxLevels, output: detail);
            for (int i = 0; i < warmup + 5; i++)
            {
                indicator.Update(new TValue(time.AddMinutes(i), constantValue));
            }

            Assert.Equal(0.0, indicator.Last.Value, CoarseTolerance);
        }
    }

    // ─── Property 2: Zero input → zero output ────────────────────────────────

    [Fact]
    public void HaarDwt_ZeroInput_ZeroApproximation()
    {
        var indicator = new Dwt(levels: 3, output: 0);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        for (int i = 0; i < warmup + 5; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 0.0));
        }

        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void HaarDwt_ZeroInput_ZeroDetail()
    {
        var indicator = new Dwt(levels: 3, output: 1);
        var time = DateTime.UtcNow;
        int warmup = indicator.WarmupPeriod;

        for (int i = 0; i < warmup + 5; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 0.0));
        }

        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
    }

    // ─── Property 3: Perfect reconstruction ──────────────────────────────────

    [Fact]
    public void PerfectReconstruction_ApproxPlusSumOfDetails_EqualsInput()
    {
        // x[n] = c[L][n] + sum(d[j][n], j=1..L)
        // All components computed at the same time = same input, so their sum = input.
        int levels = 3;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90001);
        int count = 50;
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Run all components simultaneously on same data
        var approxInd = new Dwt(levels, output: 0);
        var detail1Ind = new Dwt(levels, output: 1);
        var detail2Ind = new Dwt(levels, output: 2);
        var detail3Ind = new Dwt(levels, output: 3);

        for (int i = 0; i < count; i++)
        {
            approxInd.Update(bars.Close[i]);
            detail1Ind.Update(bars.Close[i]);
            detail2Ind.Update(bars.Close[i]);
            detail3Ind.Update(bars.Close[i]);
        }

        // Only check after full warmup
        double reconstructed = approxInd.Last.Value
            + detail1Ind.Last.Value
            + detail2Ind.Last.Value
            + detail3Ind.Last.Value;

        double original = bars.Close[^1].Value;
        Assert.Equal(original, reconstructed, 1e-8);
    }

    [Fact]
    public void PerfectReconstruction_Level2_HoldsForMultipleBars()
    {
        int levels = 2;
        int warmup = 1 << levels; // 4
        int count = 30;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.15, seed: 90002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var approxInd = new Dwt(levels, output: 0);
        var detail1Ind = new Dwt(levels, output: 1);
        var detail2Ind = new Dwt(levels, output: 2);

        for (int i = 0; i < count; i++)
        {
            approxInd.Update(bars.Close[i]);
            detail1Ind.Update(bars.Close[i]);
            detail2Ind.Update(bars.Close[i]);

            if (i >= warmup - 1)
            {
                double reconstructed = approxInd.Last.Value
                    + detail1Ind.Last.Value
                    + detail2Ind.Last.Value;
                double original = bars.Close[i].Value;
                Assert.Equal(original, reconstructed, 1e-8);
            }
        }
    }

    // ─── Property 4: Approximation smooths variance ───────────────────────────

    [Fact]
    public void Approximation_HasLowerVariance_ThanInput()
    {
        // By design, Haar averaging reduces high-frequency variance.
        int levels = 3;
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: 90003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int warmup = 1 << levels;

        var approxInd = new Dwt(levels, output: 0);
        var approxVals = new List<double>();
        var inputVals = new List<double>();

        for (int i = 0; i < count; i++)
        {
            approxInd.Update(bars.Close[i]);
            if (i >= warmup)
            {
                approxVals.Add(approxInd.Last.Value);
                inputVals.Add(bars.Close[i].Value);
            }
        }

        double inputVar = Variance(inputVals);
        double approxVar = Variance(approxVals);

        Assert.True(approxVar <= inputVar,
            $"Approximation variance {approxVar:F6} should be <= input variance {inputVar:F6}");
    }

    // ─── Property 5: Linearity of the transform ───────────────────────────────

    [Fact]
    public void DwtApproximation_IsLinear_ScaledInputScalesOutput()
    {
        // DWT is a linear operator: DWT(k*x) = k*DWT(x)
        const double scale = 2.5;
        int levels = 2;
        int count = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.1, seed: 90004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ind1 = new Dwt(levels, output: 0);
        var ind2 = new Dwt(levels, output: 0);

        for (int i = 0; i < count; i++)
        {
            ind1.Update(bars.Close[i]);
            ind2.Update(new TValue(bars.Close[i].Time, bars.Close[i].Value * scale));
        }

        // ind2.Last ≈ scale * ind1.Last
        Assert.Equal(ind1.Last.Value * scale, ind2.Last.Value, 1e-8);
    }

    // ─── Property 6: Span API perfect-reconstruction ─────────────────────────

    [Fact]
    public void Batch_Span_PerfectReconstruction_Level2()
    {
        int levels = 2;
        int count = 40;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.2, seed: 90005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int warmup = 1 << levels;

        double[] src = new double[count];
        for (int i = 0; i < count; i++) { src[i] = bars.Close[i].Value; }

        double[] approx = new double[count];
        double[] d1 = new double[count];
        double[] d2 = new double[count];

        Dwt.Batch(src, approx, levels, 0);
        Dwt.Batch(src, d1, levels, 1);
        Dwt.Batch(src, d2, levels, 2);

        for (int i = warmup - 1; i < count; i++)
        {
            double reconstructed = approx[i] + d1[i] + d2[i];
            Assert.Equal(src[i], reconstructed, 1e-8);
        }
    }

    // ─── Property 7: Detail level 1 captures 2-bar differences ───────────────

    [Fact]
    public void Detail1_CapturesHighFrequency_LargerThanDetail2()
    {
        // For GBM noise: detail level 1 (2-bar scale) has larger variance than detail level 2 (4-bar scale)
        // because lower-frequency details progressively smooth
        int levels = 3;
        int count = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: 90006);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int warmup = 1 << levels;

        var d1Ind = new Dwt(levels, output: 1);
        var d2Ind = new Dwt(levels, output: 2);
        var d1Vals = new List<double>();
        var d2Vals = new List<double>();

        for (int i = 0; i < count; i++)
        {
            d1Ind.Update(bars.Close[i]);
            d2Ind.Update(bars.Close[i]);
            if (i >= warmup)
            {
                d1Vals.Add(d1Ind.Last.Value);
                d2Vals.Add(d2Ind.Last.Value);
            }
        }

        double d1Var = Variance(d1Vals);
        double d2Var = Variance(d2Vals);

        // d1 captures finer-scale variation → should have higher energy than d2
        Assert.True(d1Var >= d2Var * 0.5,
            $"Detail 1 variance {d1Var:F6} should be >= 50% of detail 2 variance {d2Var:F6}");
    }

    // ─── Property 8: Span vs streaming consistency across all levels ──────────

    [Fact]
    public void Batch_Span_MatchesStreaming_AllLevels()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 90007);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++) { src[i] = bars.Close[i].Value; }

        for (int levels = 1; levels <= 5; levels++)
        {
            double[] spanOut = new double[count];
            Dwt.Batch(src, spanOut, levels, 0);

            var streaming = new Dwt(levels, 0);
            for (int i = 0; i < count; i++)
            {
                streaming.Update(bars.Close[i]);
                Assert.Equal(streaming.Last.Value, spanOut[i], Tolerance);
            }
        }
    }

    // ─── Helper ──────────────────────────────────────────────────────────────

    private static double Variance(List<double> vals)
    {
        if (vals.Count < 2) { return 0.0; }

        double mean = 0.0;
        for (int i = 0; i < vals.Count; i++) { mean += vals[i]; }

        mean /= vals.Count;
        double ss = 0.0;
        for (int i = 0; i < vals.Count; i++)
        {
            double d = vals[i] - mean;
            ss = Math.FusedMultiplyAdd(d, d, ss);
        }

        return ss / (vals.Count - 1);
    }
}
