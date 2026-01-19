using Xunit.Abstractions;
using TALib;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Sum (Summation with Kahan-Babuška algorithm).
/// Validates against TA-Lib SUM function and mathematical calculations.
/// </summary>
public sealed class SumValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public SumValidationTests(ITestOutputHelper output)
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
        if (_disposed) return;
        _disposed = true;
        if (disposing) _testData?.Dispose();
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = [5, 10, 20, 50, 100];
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            var sum = new Sum(period);
            var qResult = sum.Update(_testData.Data);

            var retCode = Functions.Sum<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = Functions.SumLookback(period);

            ValidationHelper.VerifyData(qResult, output, outRange, lookback, ValidationHelper.DefaultVerificationCount, ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("Sum Batch(TSeries) validated against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = [5, 10, 20, 50, 100];
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            var sum = new Sum(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(sum.Update(item).Value);
            }

            var retCode = Functions.Sum<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = Functions.SumLookback(period);

            ValidationHelper.VerifyData(qResults, output, outRange, lookback, ValidationHelper.DefaultVerificationCount, ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("Sum Streaming validated against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = [5, 10, 20, 50, 100];
        double[] sourceData = _testData.RawData.ToArray();
        double[] tOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            double[] qOutput = new double[sourceData.Length];
            Sum.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            var retCode = Functions.Sum<double>(sourceData, 0..^0, tOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = Functions.SumLookback(period);

            ValidationHelper.VerifyData(qOutput, tOutput, outRange, lookback, ValidationHelper.DefaultVerificationCount, ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("Sum Span validated against TA-Lib");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_Batch()
    {
        const int period = 10;
        var sum = new Sum(period);
        var qResult = sum.Update(_testData.Data);

        // Calculate expected sum manually using naive approach
        var rawData = _testData.RawData.ToArray();

        for (int i = 0; i < rawData.Length; i++)
        {
            double expectedSum = 0;
            int startIdx = Math.Max(0, i - period + 1);
            for (int j = startIdx; j <= i; j++)
            {
                expectedSum += rawData[j];
            }

            double qValue = qResult[i].Value;

            Assert.True(
                Math.Abs(qValue - expectedSum) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qValue:G17}, Expected={expectedSum:G17}");
        }

        _output.WriteLine("Sum Batch validated against manual calculation");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_Streaming()
    {
        int period = 10;
        var sum = new Sum(period);
        var qResults = new List<double>();
        var rawData = _testData.RawData.ToArray();

        foreach (var item in _testData.Data)
        {
            qResults.Add(sum.Update(item).Value);
        }

        for (int i = 0; i < rawData.Length; i++)
        {
            double expectedSum = 0;
            int startIdx = Math.Max(0, i - period + 1);
            for (int j = startIdx; j <= i; j++)
            {
                expectedSum += rawData[j];
            }

            Assert.True(
                Math.Abs(qResults[i] - expectedSum) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Expected={expectedSum:G17}");
        }

        _output.WriteLine("Sum Streaming validated against manual calculation");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_Span()
    {
        int period = 10;
        var sourceData = _testData.RawData.ToArray();
        var qOutput = new double[sourceData.Length];

        Sum.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

        for (int i = 0; i < sourceData.Length; i++)
        {
            double expectedSum = 0;
            int startIdx = Math.Max(0, i - period + 1);
            for (int j = startIdx; j <= i; j++)
            {
                expectedSum += sourceData[j];
            }

            Assert.True(
                Math.Abs(qOutput[i] - expectedSum) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qOutput[i]:G17}, Expected={expectedSum:G17}");
        }

        _output.WriteLine("Sum Span validated against manual calculation");
    }

    [Fact]
    public void Validate_KahanBabuska_Stability_LargeValues()
    {
        // Test numerical stability with large values
        var sum = new Sum(1000);
        double[] largeValues = new double[1000];
        double baseValue = 1e10;

        for (int i = 0; i < largeValues.Length; i++)
        {
            largeValues[i] = baseValue + i;
        }

        // Calculate sum
        foreach (var val in largeValues)
        {
            sum.Update(new TValue(DateTime.UtcNow, val));
        }

        // Expected: sum of 1e10, 1e10+1, ..., 1e10+999
        // = 1000 * 1e10 + sum of 0,1,2,...,999
        // = 1e13 + 999*1000/2 = 1e13 + 499500
        double expectedSum = 1000 * baseValue + 499500.0;

        Assert.Equal(expectedSum, sum.Last.Value, 1e-4);
        _output.WriteLine($"Sum Kahan-Babuška stability test passed: {sum.Last.Value:G17}");
    }

    [Fact]
    public void Validate_KahanBabuska_Stability_SmallDifferences()
    {
        // Test with values that have small differences (challenges precision)
        var sum = new Sum(10000);
        double[] values = new double[10000];
        double baseValue = 1e8;

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = baseValue + (i % 2 == 0 ? 0.1 : -0.1);
        }

        foreach (var val in values)
        {
            sum.Update(new TValue(DateTime.UtcNow, val));
        }

        // With alternating +0.1 and -0.1, the sum is 10000 * baseValue
        // Use tolerance scaled to magnitude (relative error ~1e-12 is excellent for 1e12 scale)
        double expectedSum = 10000 * baseValue;
        Assert.Equal(expectedSum, sum.Last.Value, 1.0);
        _output.WriteLine($"Sum small differences test passed: {sum.Last.Value:G17}");
    }

    [Fact]
    public void Validate_AgainstNaiveSum_ShortSequence()
    {
        double[] values = [100, 200, 150, 175, 125, 180, 160, 140, 190, 170];
        var sum = new Sum(5);

        for (int i = 0; i < values.Length; i++)
        {
            sum.Update(new TValue(DateTime.UtcNow, values[i]));

            // Calculate naive sum for the window
            double naiveSum = 0;
            int startIdx = Math.Max(0, i - 4); // Period = 5, so window starts 4 back
            for (int j = startIdx; j <= i; j++)
            {
                naiveSum += values[j];
            }

            Assert.Equal(naiveSum, sum.Last.Value, 1e-10);
        }

        _output.WriteLine("Sum validated against naive sum for short sequence");
    }

    [Fact]
    public void Validate_KnownSequence_ArithmeticProgression()
    {
        // Arithmetic progression: 1, 2, 3, ..., n with period 5
        // Sum at index i = sum of values from max(0, i-4) to i

        var sum = new Sum(5);

        for (int n = 1; n <= 100; n++)
        {
            sum.Update(new TValue(DateTime.UtcNow, n));

            // Calculate expected sum for window [n-4, n] (or [1, n] if n < 5)
            int windowStart = Math.Max(1, n - 4);
            // Sum of windowStart to n = (n - windowStart + 1) * (windowStart + n) / 2
            double expected = (n - windowStart + 1) * (double)(windowStart + n) / 2;

            Assert.Equal(expected, sum.Last.Value, 1e-10);
        }

        _output.WriteLine("Sum validated for arithmetic progression");
    }

    [Fact]
    public void Validate_ConstantSequence()
    {
        // Sum of constant sequence with period n should be n * constant
        double constant = 42.5;
        int period = 100;
        var sum = new Sum(period);

        for (int i = 0; i < 10000; i++)
        {
            sum.Update(new TValue(DateTime.UtcNow, constant));

            int windowSize = Math.Min(i + 1, period);
            double expected = windowSize * constant;
            Assert.Equal(expected, sum.Last.Value, 1e-9);
        }

        _output.WriteLine("Sum validated for constant sequence");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int period = 20;
        var sourceData = _testData.RawData.ToArray();

        // Mode 1: TSeries Batch
        var sum1 = new Sum(period);
        var batchResult = sum1.Update(_testData.Data);

        // Mode 2: Streaming
        var sum2 = new Sum(period);
        var streamingResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamingResults.Add(sum2.Update(item).Value);
        }

        // Mode 3: Span
        var spanOutput = new double[sourceData.Length];
        Sum.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

        // Compare all three
        for (int i = 0; i < sourceData.Length; i++)
        {
            double batchVal = batchResult[i].Value;
            double streamVal = streamingResults[i];
            double spanVal = spanOutput[i];

            Assert.Equal(batchVal, streamVal, 1e-8);
            Assert.Equal(batchVal, spanVal, 1e-8);
        }

        _output.WriteLine("All Sum calculation modes produce consistent results");
    }

    [Fact]
    public void Validate_KahanBabuska_AdversarialInput()
    {
        // This is the classic adversarial case for naive summation
        // Large positive followed by many small negatives that should cancel
        var sum = new Sum(1001);

        sum.Update(new TValue(DateTime.UtcNow, 1e16));

        for (int i = 0; i < 1000; i++)
        {
            sum.Update(new TValue(DateTime.UtcNow, -1e13));
        }

        // Expected: 1e16 - 1000 * 1e13 = 1e16 - 1e16 = 0
        double expected = 1e16 - 1000 * 1e13;

        // With Kahan-Babuška, this should be accurate
        // Naive sum would have significant error
        Assert.Equal(expected, sum.Last.Value, 1e2);
        _output.WriteLine($"Adversarial input test: Expected={expected:G17}, Actual={sum.Last.Value:G17}");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = [5, 10, 20, 50, 100];
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var sum = new Sum(period);
            var qResult = sum.Update(_testData.Data);

            var sumIndicator = Tulip.Indicators.sum;
            double[][] inputs = [tData];
            double[] options = [period];
            int lookback = period - 1;
            double[][] outputs = [new double[tData.Length - lookback]];

            sumIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            ValidationHelper.VerifyData(qResult, tResult, lookback, ValidationHelper.DefaultVerificationCount, ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("Sum Batch validated against Tulip");
    }
}
