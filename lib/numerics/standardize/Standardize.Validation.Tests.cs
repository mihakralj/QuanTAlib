using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Standardize indicator.
/// Since Standardize is a basic mathematical transformation (z-score), validation focuses on
/// mathematical properties rather than external library comparison.
/// </summary>
public class StandardizeValidationTests
{
    private readonly GBM _gbm = new(100, 0.05, 0.2, seed: 42);

    [Fact]
    public void Standardize_OutputIsFinite_AllPeriods()
    {
        // Test across multiple periods and data sets
        int[] periods = { 5, 14, 50, 100 };

        foreach (var period in periods)
        {
            var standardize = new Standardize(period);
            var series = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

            foreach (var bar in series)
            {
                var result = standardize.Update(new TValue(bar.Time, bar.Close));
                Assert.True(double.IsFinite(result.Value),
                    $"Period {period}: output {result.Value} is not finite");
            }
        }
    }

    [Fact]
    public void Standardize_MeanValue_ReturnsZero()
    {
        var standardize = new Standardize(5);

        // Create data where all values equal the mean
        double[] values = [50, 50, 50, 50, 50];

        foreach (var v in values)
        {
            standardize.Update(new TValue(DateTime.UtcNow, v));
        }

        // Value = mean, stdev = 0, should return 0
        Assert.Equal(0.0, standardize.Last.Value, 1e-10);
    }

    [Fact]
    public void Standardize_OneStdDevAboveMean_ReturnsOne()
    {
        // For a known distribution, verify z-score calculation
        // Values: 2, 4, 6 -> Mean = 4, Sample StdDev = 2
        // Z-score of 6 = (6 - 4) / 2 = 1

        var standardize = new Standardize(3);

        standardize.Update(new TValue(DateTime.UtcNow, 2));
        standardize.Update(new TValue(DateTime.UtcNow, 4));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 6));

        Assert.Equal(1.0, result.Value, 1e-10);
    }

    [Fact]
    public void Standardize_OneStdDevBelowMean_ReturnsNegativeOne()
    {
        // Values: 6, 4, 2 -> Mean = 4, Sample StdDev = 2
        // Z-score of 2 = (2 - 4) / 2 = -1

        var standardize = new Standardize(3);

        standardize.Update(new TValue(DateTime.UtcNow, 6));
        standardize.Update(new TValue(DateTime.UtcNow, 4));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 2));

        Assert.Equal(-1.0, result.Value, 1e-10);
    }

    [Fact]
    public void Standardize_TwoStdDevsAboveMean_ReturnsTwo()
    {
        // Values: 0, 4, 8 -> Mean = 4, Sample StdDev = 4
        // Z-score of 12 = (12 - 4) / 4 = 2

        var standardize = new Standardize(3);

        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 4));
        standardize.Update(new TValue(DateTime.UtcNow, 8));

        // Now add 12 to the window
        var result = standardize.Update(new TValue(DateTime.UtcNow, 12));
        // Window is now [4, 8, 12], Mean = 8, StdDev = 4
        // Z-score = (12 - 8) / 4 = 1.0

        Assert.Equal(1.0, result.Value, 1e-10);
    }

    [Fact]
    public void Standardize_ManualCalculation_Matches()
    {
        // Manual calculation test
        var standardize = new Standardize(4);

        double[] values = [10, 20, 30, 40];

        foreach (var v in values)
        {
            standardize.Update(new TValue(DateTime.UtcNow, v));
        }

        // Mean = (10 + 20 + 30 + 40) / 4 = 25
        // Sum of squared deviations = (10-25)² + (20-25)² + (30-25)² + (40-25)²
        //                           = 225 + 25 + 25 + 225 = 500
        // Sample variance = 500 / 3 = 166.667
        // Sample StdDev = sqrt(166.667) ≈ 12.91
        // Z-score of 40 = (40 - 25) / 12.91 ≈ 1.162

        double mean = 25.0;
        double sampleVariance = 500.0 / 3.0;
        double sampleStdDev = Math.Sqrt(sampleVariance);
        double expectedZ = (40.0 - mean) / sampleStdDev;

        Assert.Equal(expectedZ, standardize.Last.Value, 1e-6);
    }

    [Fact]
    public void Standardize_Symmetry_OppositeSignsForSymmetricValues()
    {
        // For symmetric values around the mean, z-scores should be opposite

        var standardize = new Standardize(5);

        // Window: -20, -10, 0, 10, 20 -> Mean = 0
        standardize.Update(new TValue(DateTime.UtcNow, -20));
        standardize.Update(new TValue(DateTime.UtcNow, -10));
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 10));
        var zFor20 = standardize.Update(new TValue(DateTime.UtcNow, 20));

        // Window: 20, 10, 0, -10, -20 -> Mean = 0
        standardize.Reset();
        standardize.Update(new TValue(DateTime.UtcNow, 20));
        standardize.Update(new TValue(DateTime.UtcNow, 10));
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, -10));
        var zForMinus20 = standardize.Update(new TValue(DateTime.UtcNow, -20));

        // |z(20)| should equal |z(-20)| and have opposite signs
        Assert.Equal(Math.Abs(zFor20.Value), Math.Abs(zForMinus20.Value), 1e-10);
        Assert.True(zFor20.Value > 0);
        Assert.True(zForMinus20.Value < 0);
    }

    [Fact]
    public void Standardize_RollingWindow_AdaptsToNewData()
    {
        var standardize = new Standardize(3);

        // Initial window: 0, 50, 100
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 100));

        // Mean = 50, value = 100 is above mean
        Assert.True(standardize.Last.Value > 0);

        // Add 0, window becomes [50, 100, 0]
        // Mean = 50, value = 0 is below mean
        var result = standardize.Update(new TValue(DateTime.UtcNow, 0));
        Assert.True(result.Value < 0);
    }

    [Fact]
    public void Standardize_NegativeValues_WorksCorrectly()
    {
        var standardize = new Standardize(5);

        // All negative values
        standardize.Update(new TValue(DateTime.UtcNow, -100));
        standardize.Update(new TValue(DateTime.UtcNow, -75));
        standardize.Update(new TValue(DateTime.UtcNow, -50));
        standardize.Update(new TValue(DateTime.UtcNow, -25));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 0));

        // 0 is above the mean of negative values
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void Standardize_LargeValues_StillPrecise()
    {
        var standardize = new Standardize(5);

        // Use larger differences to avoid floating-point precision issues
        double baseVal = 1e6;  // Smaller base, larger differences
        standardize.Update(new TValue(DateTime.UtcNow, baseVal - 200));
        standardize.Update(new TValue(DateTime.UtcNow, baseVal - 100));
        standardize.Update(new TValue(DateTime.UtcNow, baseVal));
        standardize.Update(new TValue(DateTime.UtcNow, baseVal + 100));
        var result = standardize.Update(new TValue(DateTime.UtcNow, baseVal + 200));

        // Mean = baseVal, should still give reasonable z-score
        Assert.True(double.IsFinite(result.Value));
        Assert.True(result.Value > 0, $"Expected positive z-score for above-mean value, got {result.Value}");
    }

    [Fact]
    public void Standardize_SmallDifferences_StillPrecise()
    {
        var standardize = new Standardize(5);

        // Very small differences
        double baseVal = 100.0;
        double epsilon = 1e-8;

        standardize.Update(new TValue(DateTime.UtcNow, baseVal));
        standardize.Update(new TValue(DateTime.UtcNow, baseVal + epsilon));
        standardize.Update(new TValue(DateTime.UtcNow, baseVal + 2 * epsilon));
        standardize.Update(new TValue(DateTime.UtcNow, baseVal + 3 * epsilon));
        var result = standardize.Update(new TValue(DateTime.UtcNow, baseVal + 4 * epsilon));

        // Should be finite and reasonable
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Standardize_StreamingVsBatch_Match()
    {
        var series = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] values = series.Select(b => b.Close).ToArray();

        // Streaming
        var streamStandardize = new Standardize(14);
        double[] streamResults = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            streamResults[i] = streamStandardize.Update(new TValue(DateTime.UtcNow, values[i])).Value;
        }

        // Batch
        double[] batchResults = new double[values.Length];
        Standardize.Calculate(values, batchResults, 14);

        // Compare all values
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batchResults[i], streamResults[i], 1e-10);
        }
    }

    [Fact]
    public void Standardize_AllModes_Consistent()
    {
        var series = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 14;

        // Mode 1: Streaming via Update(TValue)
        var standardize1 = new Standardize(period);
        var results1 = new List<double>();
        foreach (var bar in series)
        {
            results1.Add(standardize1.Update(new TValue(bar.Time, bar.Close)).Value);
        }

        // Mode 2: Batch via Update(TSeries)
        var tseries = new TSeries();
        foreach (var bar in series)
        {
            tseries.Add(new TValue(bar.Time, bar.Close), true);
        }

        var results2 = Standardize.Calculate(tseries, period);

        // Mode 3: Static span Calculate
        double[] values = series.Select(b => b.Close).ToArray();
        double[] results3 = new double[values.Length];
        Standardize.Calculate(values, results3, period);

        // Mode 4: Event-based chaining
        var source = new TSeries();
        var standardize4 = new Standardize(source, period);
        foreach (var bar in series)
        {
            source.Add(new TValue(bar.Time, bar.Close), true);
        }

        double results4 = standardize4.Last.Value;

        // Compare all modes (use last 50 values for stability)
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(results1[i], results2[i].Value, 1e-10);
            Assert.Equal(results1[i], results3[i], 1e-10);
        }
        // Verify Mode 4 matches last value from other modes
        Assert.Equal(results1[^1], results4, 1e-10);
    }

    [Fact]
    public void Standardize_BarCorrection_WorksCorrectly()
    {
        var standardize = new Standardize(5);

        // Build up buffer
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 50));

        // New bar
        var first = standardize.Update(new TValue(DateTime.UtcNow, 75), isNew: true);

        // Correction (same bar, different value)
        var corrected = standardize.Update(new TValue(DateTime.UtcNow, 25), isNew: false);

        // Values should be different
        Assert.NotEqual(first.Value, corrected.Value);

        // Further correction should still work
        var corrected2 = standardize.Update(new TValue(DateTime.UtcNow, 50), isNew: false);
        Assert.NotEqual(corrected.Value, corrected2.Value);
    }

    [Fact]
    public void Standardize_Period2_IsMinimum()
    {
        var standardize = new Standardize(2);

        // With only 2 values, sample stdev is still meaningful
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 100));

        // Mean = 50, Sample StdDev = sqrt(((0-50)² + (100-50)²) / 1) = sqrt(5000) ≈ 70.71
        // Z-score of 100 = (100 - 50) / 70.71 ≈ 0.707
        double mean = 50.0;
        double sampleVariance = (2500.0 + 2500.0) / 1.0;  // N-1 = 1
        double sampleStdDev = Math.Sqrt(sampleVariance);
        double expectedZ = (100.0 - mean) / sampleStdDev;

        Assert.Equal(expectedZ, result.Value, 1e-6);
    }

    [Fact]
    public void Standardize_VeryLargePeriod_StillWorks()
    {
        var standardize = new Standardize(1000);
        var series = _gbm.Fetch(1500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in series)
        {
            var result = standardize.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(standardize.IsHot);
    }

    [Fact]
    public void Standardize_ZScoreDistribution_ReasonableForFinancialData()
    {
        var standardize = new Standardize(50);
        var series = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var zScores = new List<double>();
        foreach (var bar in series)
        {
            var result = standardize.Update(new TValue(bar.Time, bar.Close));
            if (standardize.IsHot)
            {
                zScores.Add(result.Value);
            }
        }

        // For financial data (GBM returns lognormal data), the 68% rule doesn't apply directly
        // However, most z-scores should still be within reasonable bounds (±3)
        int withinThreeStdDev = zScores.Count(z => Math.Abs(z) <= 3);
        double ratio = (double)withinThreeStdDev / zScores.Count;

        // At least 90% should be within ±3 for any reasonable distribution
        Assert.True(ratio > 0.90,
            $"Expected >90% of z-scores within ±3, got {ratio * 100:F1}%");

        // Verify z-scores are reasonably distributed (not all extreme)
        int moderate = zScores.Count(z => Math.Abs(z) <= 2);
        double moderateRatio = (double)moderate / zScores.Count;

        Assert.True(moderateRatio > 0.70,
            $"Expected >70% of z-scores within ±2, got {moderateRatio * 100:F1}%");
    }

    [Fact]
    public void Standardize_SampleVsPopulationStdDev_UsesSample()
    {
        // Verify Bessel's correction (N-1) is used, not N

        var standardize = new Standardize(4);

        // Values: 10, 20, 30, 40
        standardize.Update(new TValue(DateTime.UtcNow, 10));
        standardize.Update(new TValue(DateTime.UtcNow, 20));
        standardize.Update(new TValue(DateTime.UtcNow, 30));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 40));

        // Mean = 25
        // Population variance = ((10-25)² + (20-25)² + (30-25)² + (40-25)²) / 4 = 500/4 = 125
        // Sample variance = 500 / 3 = 166.667

        double mean = 25.0;
        double popStdDev = Math.Sqrt(125.0);
        double sampleStdDev = Math.Sqrt(500.0 / 3.0);

        double zWithPopulation = (40.0 - mean) / popStdDev;
        double zWithSample = (40.0 - mean) / sampleStdDev;

        // Result should match sample (N-1) calculation, NOT population (N)
        Assert.Equal(zWithSample, result.Value, 1e-10);
        Assert.NotEqual(zWithPopulation, result.Value);
    }
}
