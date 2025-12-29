using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CMA (Cumulative Moving Average).
/// CMA is not commonly found in standard TA libraries (like TA-Lib, Skender, etc.)
/// as it's a fundamental statistical concept rather than a trading indicator.
/// These tests validate against known mathematical results.
/// </summary>
public sealed class CmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public CmaValidationTests(ITestOutputHelper output)
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

    [Fact]
    public void Validate_MathematicalCorrectness_Batch()
    {
        // Calculate QuanTAlib CMA (batch TSeries)
        var cma = new Cma();
        var qResult = cma.Update(_testData.Data);

        // Calculate expected CMA manually using running sum
        double runningSum = 0;
        int count = 0;

        foreach (var item in _testData.Data)
        {
            count++;
            runningSum += item.Value;
            double expectedCma = runningSum / count;

            // Get corresponding QuanTAlib result
            double qValue = qResult[count - 1].Value;

            Assert.True(
                Math.Abs(qValue - expectedCma) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {count - 1}: QuanTAlib={qValue:G17}, Expected={expectedCma:G17}");
        }

        _output.WriteLine("CMA Batch(TSeries) validated successfully against manual calculation");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_Streaming()
    {
        // Calculate QuanTAlib CMA (streaming)
        var cma = new Cma();
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(cma.Update(item).Value);
        }

        // Calculate expected CMA manually
        double runningSum = 0;

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            runningSum += _testData.Data[i].Value;
            double expectedCma = runningSum / (i + 1);

            Assert.True(
                Math.Abs(qResults[i] - expectedCma) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Expected={expectedCma:G17}");
        }

        _output.WriteLine("CMA Streaming validated successfully against manual calculation");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_Span()
    {
        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();
        double[] qOutput = new double[sourceData.Length];

        // Calculate QuanTAlib CMA (Span API)
        Cma.Batch(sourceData.AsSpan(), qOutput.AsSpan());

        // Calculate expected CMA manually
        double runningSum = 0;

        for (int i = 0; i < sourceData.Length; i++)
        {
            runningSum += sourceData[i];
            double expectedCma = runningSum / (i + 1);

            Assert.True(
                Math.Abs(qOutput[i] - expectedCma) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qOutput[i]:G17}, Expected={expectedCma:G17}");
        }

        _output.WriteLine("CMA Span validated successfully against manual calculation");
    }

    [Fact]
    public void Validate_WelfordAlgorithm_Stability()
    {
        // Test numerical stability with large values
        // Welford's algorithm should handle this without overflow
        var cma = new Cma();
        double[] largeValues = new double[1000];
        double baseValue = 1e10;

        for (int i = 0; i < largeValues.Length; i++)
        {
            largeValues[i] = baseValue + i;
        }

        // Calculate CMA
        foreach (var val in largeValues)
        {
            cma.Update(new TValue(DateTime.UtcNow, val));
        }

        // Expected: average of 1e10, 1e10+1, ..., 1e10+999
        // = 1e10 + average of 0,1,2,...,999
        // = 1e10 + 499.5
        double expectedMean = baseValue + 499.5;

        Assert.Equal(expectedMean, cma.Last.Value, 1e-6);
        _output.WriteLine($"CMA Welford stability test passed: {cma.Last.Value:G17}");
    }

    [Fact]
    public void Validate_WelfordAlgorithm_SmallDifferences()
    {
        // Test with values that have small differences (challenges precision)
        var cma = new Cma();
        double[] values = new double[10000];
        double baseValue = 1e8;

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = baseValue + (i % 2 == 0 ? 0.1 : -0.1);
        }

        foreach (var val in values)
        {
            cma.Update(new TValue(DateTime.UtcNow, val));
        }

        // With alternating +0.1 and -0.1, the average offset is 0
        Assert.Equal(baseValue, cma.Last.Value, 1e-7);
        _output.WriteLine($"CMA small differences test passed: {cma.Last.Value:G17}");
    }

    [Fact]
    public void Validate_AgainstNaiveSum_ShortSequence()
    {
        // For short sequences, compare against naive sum/count
        double[] values = [100, 200, 150, 175, 125, 180, 160, 140, 190, 170];
        var cma = new Cma();

        double sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
            cma.Update(new TValue(DateTime.UtcNow, values[i]));

            double naiveMean = sum / (i + 1);
            Assert.Equal(naiveMean, cma.Last.Value, 1e-10);
        }

        _output.WriteLine("CMA validated against naive sum for short sequence");
    }

    [Fact]
    public void Validate_AgainstNaiveSum_LongSequence()
    {
        // For longer sequences, verify the final value
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.1, seed: 42);
        int count = 50000;
        double sum = 0;
        var cma = new Cma();

        for (int i = 0; i < count; i++)
        {
            double value = gbm.Next().Close;
            sum += value;
            cma.Update(new TValue(DateTime.UtcNow, value));
        }

        double naiveMean = sum / count;
        double welfordMean = cma.Last.Value;

        // Both should be very close
        Assert.True(
            Math.Abs(naiveMean - welfordMean) < 1e-8,
            $"Naive={naiveMean:G17}, Welford={welfordMean:G17}, Diff={Math.Abs(naiveMean - welfordMean):G17}");

        _output.WriteLine($"CMA long sequence: Naive={naiveMean:G10}, Welford={welfordMean:G10}");
    }

    [Fact]
    public void Validate_KnownSequence_ArithmeticProgression()
    {
        // Arithmetic progression: 1, 2, 3, ..., n
        // CMA at each point: 1, 1.5, 2, 2.5, 3, ...
        // Formula: CMA_n = (n+1)/2

        var cma = new Cma();

        for (int n = 1; n <= 100; n++)
        {
            cma.Update(new TValue(DateTime.UtcNow, n));
            double expected = (n + 1.0) / 2.0;
            Assert.Equal(expected, cma.Last.Value, 1e-10);
        }

        _output.WriteLine("CMA validated for arithmetic progression");
    }

    [Fact]
    public void Validate_KnownSequence_GeometricProgression()
    {
        // Geometric progression: r, r^2, r^3, ..., r^n
        // Sum = r * (r^n - 1) / (r - 1)
        // CMA = Sum / n

        double r = 1.1;
        var cma = new Cma();

        for (int n = 1; n <= 50; n++)
        {
            double value = Math.Pow(r, n);
            cma.Update(new TValue(DateTime.UtcNow, value));

            // Sum of geometric series: a * (r^n - 1) / (r - 1) where a = r
            double sum = r * (Math.Pow(r, n) - 1) / (r - 1);
            double expected = sum / n;

            Assert.Equal(expected, cma.Last.Value, 1e-9);
        }

        _output.WriteLine("CMA validated for geometric progression");
    }

    [Fact]
    public void Validate_ConstantSequence()
    {
        // CMA of constant sequence should be the constant
        double constant = 42.5;
        var cma = new Cma();

        for (int i = 0; i < 10000; i++)
        {
            cma.Update(new TValue(DateTime.UtcNow, constant));
        }

        Assert.Equal(constant, cma.Last.Value, 1e-10);
        _output.WriteLine("CMA validated for constant sequence");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        // Verify all three calculation modes produce identical results
        var sourceData = _testData.RawData.ToArray();

        // Mode 1: TSeries Batch
        var cma1 = new Cma();
        var batchResult = cma1.Update(_testData.Data);

        // Mode 2: Streaming
        var cma2 = new Cma();
        var streamingResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamingResults.Add(cma2.Update(item).Value);
        }

        // Mode 3: Span
        var spanOutput = new double[sourceData.Length];
        Cma.Batch(sourceData.AsSpan(), spanOutput.AsSpan());

        // Compare all three
        for (int i = 0; i < sourceData.Length; i++)
        {
            double batchVal = batchResult[i].Value;
            double streamVal = streamingResults[i];
            double spanVal = spanOutput[i];

            Assert.Equal(batchVal, streamVal, 1e-10);
            Assert.Equal(batchVal, spanVal, 1e-10);
        }

        _output.WriteLine("All CMA calculation modes produce consistent results");
    }
}