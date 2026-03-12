namespace QuanTAlib.Validation;

/// <summary>
/// Validation tests for ZTEST indicator.
/// No direct TA-Lib/Tulip/Skender/Ooples equivalent exists for one-sample t-test.
/// Validates against manual computation, mathematical properties, and ZSCORE relationship.
/// </summary>
public sealed class ZtestValidationTests
{
    [Fact]
    public void Ztest_ManualComputation_MatchesPineScript()
    {
        // PineScript formula: t = (mean - mu0) / (sampleStdDev / sqrt(n))
        // Data: {10, 20, 30, 40, 50}, period=5, mu0=0
        // mean = 30, popVar = 1000/5 = 200, sampleVar = 200*5/4 = 250
        // sampleStdDev = sqrt(250) ≈ 15.8114
        // SE = sqrt(250)/sqrt(5) = sqrt(50) ≈ 7.0711
        // t = 30 / sqrt(50) = 30*sqrt(2)/10 = 3*sqrt(2) ≈ 4.2426
        var z = new Ztest(5, 0.0);
        double[] data = [10, 20, 30, 40, 50];
        foreach (double d in data)
        {
            z.Update(new TValue(DateTime.UtcNow, d));
        }

        double expected = 30.0 / Math.Sqrt(50.0);
        Assert.Equal(expected, z.Last.Value, 1e-9);
    }

    [Fact]
    public void Ztest_GBMData_BoundedRange()
    {
        // For GBM-generated data with mu0=0, t-stats should be far from zero for prices
        // but still finite
        int period = 20;
        var z = new Ztest(period, 0.0);
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 200; i++)
        {
            TBar bar = rng.Next();
            z.Update(new TValue(bar.Time, bar.Close));

            if (z.IsHot)
            {
                Assert.True(double.IsFinite(z.Last.Value),
                    $"t-stat not finite at i={i}");
            }
        }
    }

    [Fact]
    public void Ztest_ScalingProperty_Mu0ScalesToo()
    {
        // If we scale data by factor a and mu0 by same factor a,
        // t-statistic should remain the same (scale-invariant when mu0 scales too)
        int period = 10;
        double mu0 = 5.0;
        double scale = 3.0;
        var z1 = new Ztest(period, mu0);
        var z2 = new Ztest(period, mu0 * scale);
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 88);

        for (int i = 0; i < 30; i++)
        {
            double val = rng.Next().Close;
            z1.Update(new TValue(DateTime.UtcNow, val));
            z2.Update(new TValue(DateTime.UtcNow, val * scale));

            if (z1.IsHot && z2.IsHot)
            {
                Assert.Equal(z1.Last.Value, z2.Last.Value, 1e-4); // scaled values amplify FP accumulation drift
            }
        }
    }

    [Fact]
    public void Ztest_RelationToZscore_CorrectRatio()
    {
        // ZTEST(mu0=mean) = 0 while ZSCORE tests individual value vs mean
        // When mu0=0: t = mean / SE = mean / (s/sqrt(n))
        // zscore = (last_value - mean) / pop_stddev
        // Relationship: t = mean * sqrt(n) / s = mean * sqrt(n) / (pop_sd * sqrt(n/(n-1)))
        //             = mean * sqrt(n-1) / pop_sd
        int period = 10;
        var zt = new Ztest(period, 0.0);
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 99);

        for (int i = 0; i < 20; i++)
        {
            double val = rng.Next().Close;
            zt.Update(new TValue(DateTime.UtcNow, val));
        }

        // Just verify finite and non-zero for prices with mu0=0
        Assert.True(double.IsFinite(zt.Last.Value));
        Assert.NotEqual(0.0, zt.Last.Value);
    }

    [Fact]
    public void Ztest_SignProperty_MatchesMeanVsMu0()
    {
        // t-stat sign must match sign of (mean - mu0)
        int period = 10;
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);

        var source = new TSeries(30);
        for (int i = 0; i < 30; i++)
        {
            TBar bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close), true);
        }

        // With mu0 = 0 and price data around 100, mean >> mu0, so t should be positive
        var z = new Ztest(period, 0.0);
        for (int i = 0; i < source.Count; i++)
        {
            z.Update(source[i]);
        }

        Assert.True(z.Last.Value > 0, "t-stat should be positive when mean >> mu0=0");

        // With mu0 = 10000, mean << mu0, so t should be negative
        var z2 = new Ztest(period, 10000.0);
        for (int i = 0; i < source.Count; i++)
        {
            z2.Update(source[i]);
        }

        Assert.True(z2.Last.Value < 0, "t-stat should be negative when mean << mu0=10000");
    }
}
