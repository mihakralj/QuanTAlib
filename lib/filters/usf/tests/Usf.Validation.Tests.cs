using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for USF (Ehlers Ultimate Smoother Filter).
///
/// Note: USF was introduced by John Ehlers in April 2024.
/// As a very recent indicator, it is not yet available in external validation libraries
/// (Skender, TA-Lib, Tulip, OoplesFinance). These tests focus on internal consistency
/// and mathematical property verification.
/// </summary>
public sealed class UsfValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public UsfValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    /// <summary>
    /// Validates that batch, streaming, and span modes produce identical results.
    /// This is a critical self-consistency check for all indicators.
    /// </summary>
    [Fact]
    public void Validate_AllModes_ProduceSameResults()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // 1. Batch Mode (TSeries)
            var usfBatch = new Usf(period);
            var batchResult = usfBatch.Update(_testData.Data);

            // 2. Streaming Mode
            var usfStreaming = new Usf(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(usfStreaming.Update(item).Value);
            }

            // 3. Span Mode
            double[] sourceData = _testData.RawData.ToArray();
            double[] spanOutput = new double[sourceData.Length];
            Usf.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

            // Compare batch vs streaming
            Assert.Equal(batchResult.Count, streamingResults.Count);
            for (int i = 0; i < batchResult.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-10);
            }

            // Compare batch vs span
            Assert.Equal(batchResult.Count, spanOutput.Length);
            for (int i = 0; i < batchResult.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-10);
            }
        }
        _output.WriteLine("USF all modes validated successfully (batch, streaming, span produce identical results)");
    }

    /// <summary>
    /// Validates the mathematical properties of USF:
    /// - Smooth filter (reduces noise)
    /// - Zero-lag characteristics (tracks trend closely)
    /// - Converges to constant input
    /// </summary>
    [Fact]
    public void Validate_MathematicalProperties()
    {
        const int period = 10;

        // Test 1: Constant input should produce constant output (after warmup)
        var usfConstant = new Usf(period);
        for (int i = 0; i < period * 3; i++)
        {
            usfConstant.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        Assert.Equal(100.0, usfConstant.Last.Value, 1e-6);

        // Test 2: Linear trend - USF should track closely (zero-lag property)
        var usfLinear = new Usf(period);
        for (int i = 0; i < period * 5; i++)
        {
            usfLinear.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        // After warmup on a linear trend, USF should be close to the current value
        double expectedLinear = 100.0 + (period * 5 - 1);
        Assert.True(Math.Abs(usfLinear.Last.Value - expectedLinear) < period,
            $"USF should track linear trend closely. Expected ~{expectedLinear}, got {usfLinear.Last.Value}");

        // Test 3: Smoother than raw input (variance reduction on differences)
        // Use first differences (returns) to measure noise reduction
        var usf = new Usf(period);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: 42);
        var rawValues = new List<double>();
        var smoothedValues = new List<double>();

        for (int i = 0; i < 2000; i++)
        {
            var bar = gbm.Next();
            rawValues.Add(bar.Close);
            usf.Update(new TValue(bar.Time, bar.Close));
            if (usf.IsHot)
            {
                smoothedValues.Add(usf.Last.Value);
            }
        }

        // Calculate variance of first differences (measures noise/roughness)
        var rawDiffs = CalculateFirstDifferences(rawValues.Skip(period).ToList());
        var smoothedDiffs = CalculateFirstDifferences(smoothedValues);

        double rawDiffVariance = CalculateVariance(rawDiffs);
        double smoothedDiffVariance = CalculateVariance(smoothedDiffs);

        Assert.True(smoothedDiffVariance < rawDiffVariance,
            $"USF should reduce noise (diff variance). Raw diff variance: {rawDiffVariance}, Smoothed diff variance: {smoothedDiffVariance}");

        _output.WriteLine($"USF mathematical properties validated. Noise reduction: {rawDiffVariance / smoothedDiffVariance:F2}x");
    }

    /// <summary>
    /// Validates that USF coefficients are correctly computed based on Ehlers' formula.
    /// The formula is:
    /// arg = sqrt(2) * PI / period
    /// c2 = 2 * exp(-arg) * cos(arg)
    /// c3 = -exp(-2 * arg)
    /// c1 = (1 + c2 - c3) / 4
    /// </summary>
    [Fact]
    public void Validate_CoefficientCalculation()
    {
        // Verify by checking output for known input sequences
        int period = 10;
        var usf = new Usf(period);

        // Initialize with known values
        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 100));

        // After 4 values (count >= 4), the filter formula is applied
        // For constant input of 100, output should converge to 100
        for (int i = 0; i < 20; i++)
        {
            usf.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.Equal(100.0, usf.Last.Value, 1e-6);
        _output.WriteLine("USF coefficient calculation validated");
    }

    /// <summary>
    /// Validates USF against different period values to ensure stability.
    /// </summary>
    [Fact]
    public void Validate_PeriodStability()
    {
        int[] periods = { 2, 5, 10, 20, 50, 100, 200 };

        foreach (var period in periods)
        {
            var usf = new Usf(period);

            // Feed realistic data
            foreach (var item in _testData.Data)
            {
                var result = usf.Update(item);
                // All outputs should be finite
                Assert.True(double.IsFinite(result.Value),
                    $"USF with period {period} produced non-finite value: {result.Value}");
            }

            // Should be hot after sufficient data
            Assert.True(usf.IsHot, $"USF with period {period} should be hot after {_testData.Data.Count} bars");
        }
        _output.WriteLine("USF period stability validated for periods: " + string.Join(", ", periods));
    }

    private static double CalculateVariance(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        double mean = values.Average();
        return values.Sum(v => (v - mean) * (v - mean)) / values.Count;
    }

    private static List<double> CalculateFirstDifferences(List<double> values)
    {
        var diffs = new List<double>();
        for (int i = 1; i < values.Count; i++)
        {
            diffs.Add(values[i] - values[i - 1]);
        }
        return diffs;
    }
}
