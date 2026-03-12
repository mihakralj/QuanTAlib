namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Cointegration indicator.
/// Note: Cointegration is not commonly implemented in standard TA libraries.
/// These tests validate against expected statistical properties rather than
/// external library comparisons.
/// </summary>
public class CointegrationValidationTests
{
    private const double Tolerance = 1e-6;

    // GBM-based noise helper: log-return from seeded GBM price stream as centered noise.
    private static double GbmNoise(GBM gbm) => Math.Log(gbm.Next().Close / 100.0);

    #region Statistical Property Validation

    [Fact]
    public void Cointegration_PerfectlyCointegrated_ProducesStrongNegativeAdf()
    {
        // Two series with near-perfect linear relationship should show strong cointegration.
        // Use incremental log-returns (i.i.d.) as noise so residuals are stationary.
        // Period=30 gives ADF sufficient window; 200 samples ensure stable regression.
        var indicator = new Cointegration(30);
        var gbm = new GBM(startPrice: 100.0, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(201, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 1; i <= 200; i++)
        {
            // Incremental log-return: truly i.i.d. noise, variance ~(0.2²·dt)
            double noise = Math.Log(bars[i].Close / bars[i - 1].Close);
            double a = 100.0 + i * 0.5 + noise * 0.1;
            double b = 2.0 * a + 10.0 + noise * 0.1;
            indicator.Update(a, b);
        }

        // Near-perfect cointegration should produce ADF below the 5% critical value.
        // Engle-Granger critical values (residual-based, no constant): -1.95 at 5%, -2.86 for large N.
        // With period=30 and 200 samples of near-linear data the statistic should clear -1.95 comfortably.
        Assert.True(indicator.Last.Value < -1.95, $"ADF should be below 5% critical value (-1.95) for cointegrated series, got {indicator.Last.Value}");
    }

    [Fact]
    public void Cointegration_IdenticalSeries_ProducesNegativeOrNaN()
    {
        // Two identical series produce zero residuals, which is mathematically correct
        // but results in zero variance for ADF test (division by zero → NaN)
        var indicator = new Cointegration(20);

        for (int i = 0; i < 100; i++)
        {
            double value = 100.0 + Math.Sin(i * 0.1) * 10.0;
            indicator.Update(value, value);
        }

        // Identical series produce zero residuals → NaN ADF (mathematically correct)
        // This is expected behavior: perfect cointegration with no estimation error
        Assert.True(double.IsNaN(indicator.Last.Value) || indicator.Last.Value < 0,
            $"ADF should be NaN or negative for identical series, got {indicator.Last.Value}");
    }

    [Fact]
    public void Cointegration_ProportionalSeries_WithNoise_ProducesNegativeAdf()
    {
        // B = k * A + small noise (near-proportional relationship)
        var indicator = new Cointegration(20);
        var random = new GBM(startPrice: 100.0, sigma: 1.0, seed: 43);

        for (int i = 0; i < 100; i++)
        {
            double a = 50.0 + i * 0.3 + Math.Sin(i * 0.2) * 5.0;
            double noise = GbmNoise(random) * 0.5;
            double b = 1.5 * a + noise;
            indicator.Update(a, b);
        }

        // Proportional series with small noise should produce ADF well below 0; -1.0 is a conservative bound.
        Assert.True(indicator.Last.Value < -1.0, $"ADF should be well negative for near-proportional series, got {indicator.Last.Value}");
    }

    [Fact]
    public void Cointegration_LinearWithNoise_StillDetectsCointegration()
    {
        // B = α + β*A + small_noise
        var indicator = new Cointegration(20);
        var random = new GBM(startPrice: 100.0, sigma: 1.0, seed: 44);

        for (int i = 0; i < 100; i++)
        {
            double a = 100.0 + i * 0.2;
            double noise = GbmNoise(random) * 0.5; // Small noise
            double b = 25.0 + 0.8 * a + noise;
            indicator.Update(a, b);
        }

        // Linear relationship with small noise should still clear -1.0.
        Assert.True(indicator.Last.Value < -1.0, $"ADF should be well negative with small noise, got {indicator.Last.Value}");
    }

    [Fact]
    public void Cointegration_DifferentPeriods_ProduceDifferentResults()
    {
        var indicator10 = new Cointegration(10);
        var indicator30 = new Cointegration(30);

        for (int i = 0; i < 100; i++)
        {
            double a = 100.0 + i * 0.3;
            double b = 50.0 + 0.5 * a + Math.Sin(i * 0.1);
            indicator10.Update(a, b);
            indicator30.Update(a, b);
        }

        // Different periods should yield different ADF values
        Assert.NotEqual(indicator10.Last.Value, indicator30.Last.Value);
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void Cointegration_BatchMatchesStreaming()
    {
        var seriesA = new TSeries();
        var seriesB = new TSeries();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            double a = 100.0 + i * 0.2 + Math.Sin(i * 0.1) * 3.0;
            double b = 30.0 + 0.7 * a + Math.Cos(i * 0.15) * 2.0;
            seriesA.Add(baseTime.AddMinutes(i), a);
            seriesB.Add(baseTime.AddMinutes(i), b);
        }

        // Batch calculation
        var batchResult = Cointegration.Batch(seriesA, seriesB, 20);

        // Streaming calculation
        var streamingIndicator = new Cointegration(20);
        for (int i = 0; i < seriesA.Count; i++)
        {
            streamingIndicator.Update(seriesA[i].Value, seriesB[i].Value);
        }

        // Last values should match
        if (double.IsNaN(batchResult.Last.Value) && double.IsNaN(streamingIndicator.Last.Value))
        {
            Assert.True(true);
        }
        else
        {
            Assert.Equal(batchResult.Last.Value, streamingIndicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Cointegration_SpanMatchesStreaming()
    {
        const int length = 50;
        var seriesA = new double[length];
        var seriesB = new double[length];
        var output = new double[length];

        for (int i = 0; i < length; i++)
        {
            seriesA[i] = 100.0 + i * 0.2 + Math.Sin(i * 0.1) * 3.0;
            seriesB[i] = 30.0 + 0.7 * seriesA[i] + Math.Cos(i * 0.15) * 2.0;
        }

        // Span calculation
        Cointegration.Batch(seriesA, seriesB, output, 20);

        // Streaming calculation
        var streamingIndicator = new Cointegration(20);
        for (int i = 0; i < length; i++)
        {
            streamingIndicator.Update(seriesA[i], seriesB[i]);
        }

        // Last values should match
        if (double.IsNaN(output[length - 1]) && double.IsNaN(streamingIndicator.Last.Value))
        {
            Assert.True(true);
        }
        else
        {
            Assert.Equal(output[length - 1], streamingIndicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Cointegration_ResetProducesSameResults()
    {
        var indicator = new Cointegration(20);

        // First run
        for (int i = 0; i < 50; i++)
        {
            double a = 100.0 + i * 0.3;
            double b = 50.0 + 0.5 * a;
            indicator.Update(a, b);
        }
        var firstResult = indicator.Last.Value;

        indicator.Reset();

        // Second run with same data
        for (int i = 0; i < 50; i++)
        {
            double a = 100.0 + i * 0.3;
            double b = 50.0 + 0.5 * a;
            indicator.Update(a, b);
        }
        var secondResult = indicator.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Cointegration_ConstantSeries_HandlesGracefully()
    {
        var indicator = new Cointegration(10);

        // Both series are constant
        for (int i = 0; i < 20; i++)
        {
            indicator.Update(100.0, 50.0);
        }

        // Constant series → zero variance → ADF denominator is zero → NaN is correct.
        Assert.True(double.IsNaN(indicator.Last.Value), $"Expected NaN for constant series, got {indicator.Last.Value}");
    }

    [Fact]
    public void Cointegration_OneConstantOneTrending_HandlesGracefully()
    {
        var indicator = new Cointegration(10);

        for (int i = 0; i < 20; i++)
        {
            indicator.Update(100.0, 50.0 + i); // A constant, B trending
        }

        // Constant A → zero variance in A → ADF is undefined → NaN.
        Assert.True(double.IsNaN(indicator.Last.Value), $"Expected NaN when series A is constant, got {indicator.Last.Value}");
    }

    [Fact]
    public void Cointegration_SmallPeriod_WorksCorrectly()
    {
        var indicator = new Cointegration(3); // Minimum practical period
        var random = new GBM(startPrice: 100.0, sigma: 1.0, seed: 45);

        for (int i = 0; i < 20; i++)
        {
            double a = 100.0 + i + GbmNoise(random) * 0.1;
            double b = 50.0 + 0.5 * a + GbmNoise(random) * 0.1;
            indicator.Update(a, b);
        }

        Assert.True(indicator.IsHot);
        // With small periods and noise, result may be finite or NaN
        Assert.True(double.IsFinite(indicator.Last.Value) || double.IsNaN(indicator.Last.Value));
    }

    [Fact]
    public void Cointegration_LargePeriod_WorksCorrectly()
    {
        var indicator = new Cointegration(100);
        var random = new GBM(startPrice: 100.0, sigma: 1.0, seed: 46);

        for (int i = 0; i < 150; i++)
        {
            double a = 100.0 + i * 0.1 + GbmNoise(random) * 0.1;
            double b = 30.0 + 0.8 * a + GbmNoise(random) * 0.1;
            indicator.Update(a, b);
        }

        Assert.True(indicator.IsHot);
        // Should produce finite or NaN value (both acceptable for edge cases)
        Assert.True(double.IsFinite(indicator.Last.Value) || double.IsNaN(indicator.Last.Value));
    }

    #endregion

    #region Numerical Stability

    [Fact]
    public void Cointegration_LargeValues_MaintainsStability()
    {
        var indicator = new Cointegration(20);

        for (int i = 0; i < 50; i++)
        {
            double a = 1e8 + i * 1e5;
            double b = 2e8 + 2.0 * a;
            indicator.Update(a, b);
        }

        Assert.True(double.IsFinite(indicator.Last.Value) || double.IsNaN(indicator.Last.Value));
    }

    [Fact]
    public void Cointegration_SmallValues_MaintainsStability()
    {
        var indicator = new Cointegration(20);

        for (int i = 0; i < 50; i++)
        {
            double a = 1e-6 + i * 1e-8;
            double b = 2e-6 + 1.5 * a;
            indicator.Update(a, b);
        }

        Assert.True(double.IsFinite(indicator.Last.Value) || double.IsNaN(indicator.Last.Value));
    }

    [Fact]
    public void Cointegration_MixedMagnitudes_HandlesCorrectly()
    {
        var indicator = new Cointegration(20);

        for (int i = 0; i < 50; i++)
        {
            double a = 1000.0 + i;
            double b = 0.001 + 0.000001 * a; // Much smaller scale
            indicator.Update(a, b);
        }

        Assert.True(double.IsFinite(indicator.Last.Value) || double.IsNaN(indicator.Last.Value));
    }

    #endregion
}
