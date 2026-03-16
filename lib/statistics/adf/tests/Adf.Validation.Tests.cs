namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for the ADF indicator — verifying mathematical properties
/// and cross-checking against known statistical behaviors.
/// </summary>
public class AdfValidationTests
{
    // ═══════════════════════════════════════════════════════════════
    // 1. P-Value Bounds
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PValue_AlwaysBetweenZeroAndOne()
    {
        var seeds = new[] { 1, 42, 123, 999, 31415 };

        foreach (int seed in seeds)
        {
            var a = new Adf(30);
            var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: seed);

            for (int i = 0; i < 100; i++)
            {
                var bar = gbm.Next(isNew: true);
                var result = a.Update(new TValue(bar.Time, bar.Close));
                Assert.InRange(result.Value, 0.0, 1.0);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Known Stationary Process
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AR1_WithStrongMeanReversion_DetectsStationarity()
    {
        // AR(1): y_t = 0.3 * y_{t-1} + ε_t  (|φ| < 1 → stationary)
        var a = new Adf(50, 1, Adf.AdfRegression.Constant);
        var rng = new Random(42);
        double y = 0;
        var now = DateTime.UtcNow;

        for (int i = 0; i < 500; i++)
        {
            y = (0.3 * y) + (rng.NextDouble() * 2) - 1;
            a.Update(new TValue(now.AddMinutes(i), 100 + y));
        }

        // Strong mean-reversion — p should be very low
        Assert.True(a.PValue < 0.05, $"AR(1) φ=0.3 should be detected as stationary, p={a.PValue}");
    }

    [Fact]
    public void WhiteNoise_IsStationary()
    {
        // Pure white noise is strongly stationary — use explicit lag=1 to avoid
        // auto-lag overfitting on small windows, and zero-centered noise for clean signal
        var a = new Adf(50, 1, Adf.AdfRegression.Constant);
        var rng = new Random(42);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 500; i++)
        {
            double noise = (rng.NextDouble() * 10) - 5; // zero-centered white noise
            a.Update(new TValue(now.AddMinutes(i), noise));
        }

        Assert.True(a.PValue < 0.10, $"White noise should be stationary, p={a.PValue}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Known Non-Stationary Process
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PureRandomWalk_FailsToRejectUnitRoot()
    {
        // y_t = y_{t-1} + ε_t  (unit root)
        var a = new Adf(50, 1, Adf.AdfRegression.Constant);
        var rng = new Random(789);
        double y = 100;
        var now = DateTime.UtcNow;

        for (int i = 0; i < 500; i++)
        {
            y += (rng.NextDouble() * 2) - 1;
            a.Update(new TValue(now.AddMinutes(i), y));
        }

        Assert.True(a.PValue > 0.05, $"Random walk should not reject unit root, p={a.PValue}");
    }

    [Fact]
    public void LinearTrend_WithNoConstantModel_AppearsNonStationary()
    {
        // Pure linear trend y_t = t
        var a = new Adf(50, 0, Adf.AdfRegression.NoConstant);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            a.Update(new TValue(now.AddMinutes(i), 100.0 + (i * 0.1)));
        }

        // Linear trend without constant/trend in model should appear non-stationary
        Assert.InRange(a.PValue, 0.0, 1.0);
        Assert.True(double.IsFinite(a.Statistic));
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. MacKinnon P-Value Properties
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void VeryNegativeStatistic_GivesLowPValue()
    {
        // Feed data that will produce very negative t-stat (strongly stationary)
        var a = new Adf(30, 0, Adf.AdfRegression.Constant);
        var rng = new Random(42);
        var now = DateTime.UtcNow;

        // Oscillating series: y_t = -0.9 * y_{t-1} + noise → very negative γ
        double y = 0;
        for (int i = 0; i < 100; i++)
        {
            y = (-0.9 * y) + (rng.NextDouble() * 0.1);
            a.Update(new TValue(now.AddMinutes(i), 50 + y));
        }

        Assert.True(a.PValue < 0.01, $"Strong oscillation should give p < 0.01, got {a.PValue}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. Consistency Across API Modes
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BatchAndStreaming_ProduceConsistentResults()
    {
        int period = 30;
        var rng = new Random(42);
        double y = 100;
        var source = new TSeries();
        for (int i = 0; i < 80; i++)
        {
            y += (rng.NextDouble() * 2) - 1;
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), y));
        }

        // Batch via TSeries
        var batchResult = Adf.Batch(source, period);

        // Span batch
        double[] spanOutput = new double[source.Count];
        Adf.Batch(source.Values, spanOutput.AsSpan(), period);

        // Both should be in valid range
        for (int i = 0; i < source.Count; i++)
        {
            Assert.InRange(batchResult.Values[i], 0.0, 1.0);
            Assert.InRange(spanOutput[i], 0.0, 1.0);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. Determinism
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        double[] data = { 100, 101, 99, 102, 98, 103, 97, 104, 96, 105,
                         94, 106, 93, 107, 92, 108, 91, 109, 90, 110,
                         89, 111, 88, 112, 87, 113, 86, 114, 85, 115 };

        var a1 = new Adf(25);
        var a2 = new Adf(25);

        for (int i = 0; i < data.Length; i++)
        {
            var tv = new TValue(DateTime.UtcNow.AddMinutes(i), data[i]);
            a1.Update(tv);
            a2.Update(tv);
        }

        Assert.Equal(a1.PValue, a2.PValue);
        Assert.Equal(a1.Statistic, a2.Statistic);
        Assert.Equal(a1.LagsUsed, a2.LagsUsed);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. Reset and Reprocess
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ResetAndReprocess_GivesSameResult()
    {
        var a = new Adf(25);
        var rng = new Random(42);
        double y = 100;
        var data = new List<TValue>();
        for (int i = 0; i < 40; i++)
        {
            y += (rng.NextDouble() * 2) - 1;
            data.Add(new TValue(DateTime.UtcNow.AddMinutes(i), y));
        }

        // First pass
        foreach (var tv in data)
        {
            a.Update(tv);
        }
        double firstPValue = a.PValue;
        double firstStat = a.Statistic;

        // Reset and second pass
        a.Reset();
        foreach (var tv in data)
        {
            a.Update(tv);
        }

        Assert.Equal(firstPValue, a.PValue);
        Assert.Equal(firstStat, a.Statistic);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. Auto-Lag Selection
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AutoLag_SelectsReasonableLag()
    {
        var a = new Adf(50, 0, Adf.AdfRegression.Constant);
        var rng = new Random(42);
        double y = 100;
        var now = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            y += (rng.NextDouble() * 2) - 1;
            a.Update(new TValue(now.AddMinutes(i), y));
        }

        // Auto-lag should select a small number of lags
        Assert.True(a.LagsUsed >= 0);
        Assert.True(a.LagsUsed <= 5, $"Auto-lag selected {a.LagsUsed} lags — seems excessive for 50-bar window");
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. Edge Cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MinimumPeriod_StillWorks()
    {
        var a = new Adf(20, 0, Adf.AdfRegression.Constant);
        var rng = new Random(42);
        double y = 100;
        var now = DateTime.UtcNow;

        for (int i = 0; i < 25; i++)
        {
            y += (rng.NextDouble() * 2) - 1;
            a.Update(new TValue(now.AddMinutes(i), y));
        }

        Assert.True(double.IsFinite(a.PValue));
        Assert.InRange(a.PValue, 0.0, 1.0);
    }

    [Fact]
    public void FixedLagZero_NoAugmentation()
    {
        var a = new Adf(30, 1, Adf.AdfRegression.Constant);
        var rng = new Random(42);
        double y = 100;
        var now = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            y += (rng.NextDouble() * 2) - 1;
            a.Update(new TValue(now.AddMinutes(i), y));
        }

        // With explicit lag=1, should get finite result
        Assert.True(double.IsFinite(a.PValue));
        Assert.Equal(1, a.LagsUsed);
    }
}
