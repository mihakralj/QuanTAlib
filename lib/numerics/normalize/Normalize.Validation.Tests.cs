using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Normalize indicator.
/// Since Normalize is a basic mathematical transformation, validation focuses on
/// mathematical properties rather than external library comparison.
/// </summary>
public class NormalizeValidationTests
{
    private readonly GBM _gbm = new(100, 0.05, 0.2, seed: 42);

    [Fact]
    public void Normalize_OutputBounds_AlwaysZeroToOne()
    {
        // Test across multiple periods and data sets
        int[] periods = { 5, 14, 50, 100 };

        foreach (var period in periods)
        {
            var norm = new Normalize(period);
            var series = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

            foreach (var bar in series)
            {
                var result = norm.Update(new TValue(bar.Time, bar.Close));
                Assert.True(result.Value >= 0.0 && result.Value <= 1.0,
                    $"Period {period}: output {result.Value} not in [0,1]");
            }
        }
    }

    [Fact]
    public void Normalize_MaxInWindow_ReturnsOne()
    {
        var norm = new Normalize(5);

        // Create ascending sequence
        double[] values = { 10, 20, 30, 40, 50 };

        foreach (var v in values)
            norm.Update(new TValue(DateTime.UtcNow, v));

        // Max value (50) should normalize to 1.0
        Assert.Equal(1.0, norm.Last.Value, 1e-10);
    }

    [Fact]
    public void Normalize_MinInWindow_ReturnsZero()
    {
        var norm = new Normalize(5);

        // Create descending sequence ending at min
        double[] values = { 50, 40, 30, 20, 10 };

        foreach (var v in values)
            norm.Update(new TValue(DateTime.UtcNow, v));

        // Min value (10) should normalize to 0.0
        Assert.Equal(0.0, norm.Last.Value, 1e-10);
    }

    [Fact]
    public void Normalize_LinearMapping_Correct()
    {
        var norm = new Normalize(5);

        // Set up window with known range [0, 100]
        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 50));  // Placeholder
        norm.Update(new TValue(DateTime.UtcNow, 50));  // Placeholder
        norm.Update(new TValue(DateTime.UtcNow, 50));  // Placeholder

        // Test various values - (value - 0) / (100 - 0) = value / 100
        double[] testValues = { 0, 25, 50, 75, 100 };
        double[] expected = { 0.0, 0.25, 0.5, 0.75, 1.0 };

        for (int i = 0; i < testValues.Length; i++)
        {
            // Reset and refill to maintain window [0, 100, test, test, test]
            norm.Reset();
            norm.Update(new TValue(DateTime.UtcNow, 0));
            norm.Update(new TValue(DateTime.UtcNow, 100));
            norm.Update(new TValue(DateTime.UtcNow, testValues[i]));
            norm.Update(new TValue(DateTime.UtcNow, testValues[i]));
            var result = norm.Update(new TValue(DateTime.UtcNow, testValues[i]));

            Assert.Equal(expected[i], result.Value, 1e-10);
        }
    }

    [Fact]
    public void Normalize_ConstantInput_ReturnsHalf()
    {
        var norm = new Normalize(10);

        // All same values
        for (int i = 0; i < 20; i++)
            norm.Update(new TValue(DateTime.UtcNow, 42.0));

        // Flat range: should return 0.5
        Assert.Equal(0.5, norm.Last.Value, 1e-10);
    }

    [Fact]
    public void Normalize_RollingWindow_AdaptsToNewRange()
    {
        var norm = new Normalize(3);

        // Initial window [10, 20, 30] - range 20
        norm.Update(new TValue(DateTime.UtcNow, 10));
        norm.Update(new TValue(DateTime.UtcNow, 20));
        norm.Update(new TValue(DateTime.UtcNow, 30));

        // Value 25 in range [10, 30]: (25-10)/(30-10) = 0.75
        var result1 = norm.Update(new TValue(DateTime.UtcNow, 25));
        // Window is now [20, 30, 25], range [20, 30]
        // (25-20)/(30-20) = 0.5
        Assert.Equal(0.5, result1.Value, 1e-10);
    }

    [Fact]
    public void Normalize_NegativeValues_WorksCorrectly()
    {
        var norm = new Normalize(5);

        // Range from -50 to +50
        norm.Update(new TValue(DateTime.UtcNow, -50));
        norm.Update(new TValue(DateTime.UtcNow, -25));
        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 25));
        norm.Update(new TValue(DateTime.UtcNow, 50));

        // max=50, value=50: (50-(-50))/(50-(-50)) = 100/100 = 1.0
        Assert.Equal(1.0, norm.Last.Value, 1e-10);

        // Test zero: (0-(-50))/(50-(-50)) = 50/100 = 0.5
        norm.Reset();
        norm.Update(new TValue(DateTime.UtcNow, -50));
        norm.Update(new TValue(DateTime.UtcNow, 50));
        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 0));
        var zeroResult = norm.Update(new TValue(DateTime.UtcNow, 0));
        Assert.Equal(0.5, zeroResult.Value, 1e-10);
    }

    [Fact]
    public void Normalize_SmallRange_HighPrecision()
    {
        var norm = new Normalize(5);

        // Very small range
        double baseVal = 100.0;
        double epsilon = 1e-8;

        norm.Update(new TValue(DateTime.UtcNow, baseVal));
        norm.Update(new TValue(DateTime.UtcNow, baseVal + epsilon));
        norm.Update(new TValue(DateTime.UtcNow, baseVal + epsilon / 2));
        norm.Update(new TValue(DateTime.UtcNow, baseVal + epsilon / 4));
        var result = norm.Update(new TValue(DateTime.UtcNow, baseVal + epsilon * 0.75));

        // Should be in valid range
        Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
    }

    [Fact]
    public void Normalize_LargeRange_StillPrecise()
    {
        var norm = new Normalize(5);

        // Very large range
        norm.Update(new TValue(DateTime.UtcNow, -1e10));
        norm.Update(new TValue(DateTime.UtcNow, 1e10));
        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 0));
        var result = norm.Update(new TValue(DateTime.UtcNow, 0));

        // 0 in range [-1e10, 1e10]: (0 - (-1e10)) / (2e10) = 0.5
        Assert.Equal(0.5, result.Value, 1e-6);
    }

    [Fact]
    public void Normalize_StreamingVsBatch_Match()
    {
        var series = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] values = series.Select(b => b.Close).ToArray();

        // Streaming
        var streamNorm = new Normalize(14);
        var streamResults = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            streamResults[i] = streamNorm.Update(new TValue(DateTime.UtcNow, values[i])).Value;
        }

        // Batch
        double[] batchResults = new double[values.Length];
        Normalize.Calculate(values, batchResults, 14);

        // Compare all values
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batchResults[i], streamResults[i], 1e-10);
        }
    }

    [Fact]
    public void Normalize_AllModes_Consistent()
    {
        var series = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        int period = 14;

        // Mode 1: Streaming via Update(TValue)
        var norm1 = new Normalize(period);
        var results1 = new List<double>();
        foreach (var bar in series)
        {
            results1.Add(norm1.Update(new TValue(bar.Time, bar.Close)).Value);
        }

        // Mode 2: Batch via Update(TSeries)
        var tseries = new TSeries();
        foreach (var bar in series)
            tseries.Add(new TValue(bar.Time, bar.Close), true);
        var results2 = Normalize.Calculate(tseries, period);

        // Mode 3: Static span Calculate
        double[] values = series.Select(b => b.Close).ToArray();
        double[] results3 = new double[values.Length];
        Normalize.Calculate(values, results3, period);

        // Mode 4: Event-based chaining
        var source = new TSeries();
        var norm4 = new Normalize(source, period);
        foreach (var bar in series)
            source.Add(new TValue(bar.Time, bar.Close), true);
        var results4 = norm4.Last.Value;

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
    public void Normalize_BarCorrection_WorksCorrectly()
    {
        var norm = new Normalize(5);

        // Build up buffer
        norm.Update(new TValue(DateTime.UtcNow, 0));
        norm.Update(new TValue(DateTime.UtcNow, 100));
        norm.Update(new TValue(DateTime.UtcNow, 50));
        norm.Update(new TValue(DateTime.UtcNow, 50));

        // New bar
        var first = norm.Update(new TValue(DateTime.UtcNow, 75), isNew: true);

        // Correction (same bar, different value)
        var corrected = norm.Update(new TValue(DateTime.UtcNow, 25), isNew: false);

        // Values should be different
        Assert.NotEqual(first.Value, corrected.Value);

        // Further correction should still work
        var corrected2 = norm.Update(new TValue(DateTime.UtcNow, 50), isNew: false);
        Assert.NotEqual(corrected.Value, corrected2.Value);
    }

    [Fact]
    public void Normalize_Period1_ReturnsHalf()
    {
        var norm = new Normalize(1);

        // With period 1, min = max = current value, so range = 0
        var result = norm.Update(new TValue(DateTime.UtcNow, 42));

        // Flat range returns 0.5
        Assert.Equal(0.5, result.Value, 1e-10);
    }

    [Fact]
    public void Normalize_VeryLargePeriod_StillWorks()
    {
        var norm = new Normalize(1000);
        var series = _gbm.Fetch(1500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in series)
        {
            var result = norm.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }

        Assert.True(norm.IsHot);
    }
}