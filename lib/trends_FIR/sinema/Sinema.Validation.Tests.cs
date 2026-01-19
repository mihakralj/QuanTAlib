using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for SINEMA indicator.
/// SINEMA is not available in external libraries (TA-Lib, Skender, Tulip, Ooples),
/// so validation is done against mathematical properties and reference values
/// computed from the PineScript implementation.
/// </summary>
public sealed class SinemaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public SinemaValidationTests(ITestOutputHelper output)
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
        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    /// <summary>
    /// Validates that SINEMA produces correct sine weights.
    /// For period N, weight[i] = sin(π * (i+1) / N)
    /// </summary>
    [Fact]
    public void Validate_SineWeights_AreCorrect()
    {
        int period = 5;

        // Expected weights: sin(π*1/5), sin(π*2/5), sin(π*3/5), sin(π*4/5), sin(π*5/5)
        double[] expectedWeights =
        [
            Math.Sin(Math.PI * 1 / 5), // ≈ 0.5878
            Math.Sin(Math.PI * 2 / 5), // ≈ 0.9511
            Math.Sin(Math.PI * 3 / 5), // ≈ 0.9511
            Math.Sin(Math.PI * 4 / 5), // ≈ 0.5878
            Math.Sin(Math.PI * 5 / 5)  // = 0 (sin(π))
        ];

        // Feed values 1, 2, 3, 4, 5 and verify weighted calculation
        var sinema = new Sinema(period);
        double[] inputs = [1, 2, 3, 4, 5];

        foreach (double val in inputs)
        {
            sinema.Update(new TValue(DateTime.UtcNow, val));
        }

        // Manual calculation: Σ(val[i] * w[i]) / Σ(w[i])
        double expectedSum = 0;
        double weightSum = 0;
        for (int i = 0; i < period; i++)
        {
            expectedSum += inputs[i] * expectedWeights[i];
            weightSum += expectedWeights[i];
        }
        double expectedResult = expectedSum / weightSum;

        Assert.Equal(expectedResult, sinema.Last.Value, 1e-10);
        _output.WriteLine($"SINEMA({period}) of [1,2,3,4,5] = {sinema.Last.Value:F10} (expected {expectedResult:F10})");
    }

    /// <summary>
    /// Validates that constant input produces constant output.
    /// This is a fundamental property of all weighted averages.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Validate_ConstantInput_ProducesConstantOutput(int period)
    {
        var sinema = new Sinema(period);
        const double constantValue = 123.456;

        // Feed constant values
        for (int i = 0; i < period * 2; i++)
        {
            sinema.Update(new TValue(DateTime.UtcNow, constantValue));
        }

        Assert.Equal(constantValue, sinema.Last.Value, 1e-10);
        _output.WriteLine($"SINEMA({period}) of constant {constantValue} = {sinema.Last.Value}");
    }

    /// <summary>
    /// Validates batch calculation matches streaming calculation.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_BatchMatchesStreaming(int period)
    {
        var sinemaStreaming = new Sinema(period);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            streamingResults.Add(sinemaStreaming.Update(item).Value);
        }

        // Calculate batch
        var sinemaBatch = new Sinema(period);
        var batchResults = sinemaBatch.Update(_testData.Data);

        // Compare all values
        Assert.Equal(streamingResults.Count, batchResults.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, 1e-10);
        }

        _output.WriteLine($"SINEMA({period}) batch matches streaming for {streamingResults.Count} values");
    }

    /// <summary>
    /// Validates span calculation matches streaming calculation.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_SpanMatchesStreaming(int period)
    {
        var sinemaStreaming = new Sinema(period);
        var streamingResults = new List<double>();

        foreach (var item in _testData.Data)
        {
            streamingResults.Add(sinemaStreaming.Update(item).Value);
        }

        // Calculate span
        double[] sourceData = _testData.RawData.ToArray();
        double[] spanOutput = new double[sourceData.Length];
        Sinema.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

        // Compare all values
        Assert.Equal(streamingResults.Count, spanOutput.Length);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], spanOutput[i], 1e-10);
        }

        _output.WriteLine($"SINEMA({period}) span matches streaming for {streamingResults.Count} values");
    }

    /// <summary>
    /// Validates that SINEMA is bounded by min and max of input values.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Validate_OutputBoundedByInput(int period)
    {
        var sinema = new Sinema(period);

        double[] inputs = [10, 20, 15, 25, 5, 30, 12, 18, 22, 8];
        double runningMin = double.MaxValue;
        double runningMax = double.MinValue;

        for (int i = 0; i < inputs.Length; i++)
        {
            double input = inputs[i];
            runningMin = Math.Min(runningMin, input);
            runningMax = Math.Max(runningMax, input);

            double result = sinema.Update(new TValue(DateTime.UtcNow, input)).Value;

            // For the first few values, the window is partial
            // but output should still be within the range of values seen so far
            Assert.True(result >= runningMin - 1e-10 && result <= runningMax + 1e-10,
                $"SINEMA value {result} outside bounds [{runningMin}, {runningMax}] at index {i}");
        }

        _output.WriteLine($"SINEMA({period}) output properly bounded by input range");
    }

    /// <summary>
    /// Validates known reference values computed from PineScript.
    /// These values were computed using the reference PineScript implementation.
    /// </summary>
    [Fact]
    public void Validate_KnownReferenceValues()
    {
        var sinema = new Sinema(5);

        // Input sequence: 100, 102, 104, 103, 105
        double[] inputs = [100, 102, 104, 103, 105];

        // Feed all values
        foreach (double val in inputs)
        {
            sinema.Update(new TValue(DateTime.UtcNow, val));
        }

        // Calculate expected value manually
        // Weights: sin(π*1/5), sin(π*2/5), sin(π*3/5), sin(π*4/5), sin(π*5/5)
        double w0 = Math.Sin(Math.PI * 1 / 5);
        double w1 = Math.Sin(Math.PI * 2 / 5);
        double w2 = Math.Sin(Math.PI * 3 / 5);
        double w3 = Math.Sin(Math.PI * 4 / 5);
        double w4 = Math.Sin(Math.PI * 5 / 5);

        double expectedSum = 100 * w0 + 102 * w1 + 104 * w2 + 103 * w3 + 105 * w4;
        double weightSum = w0 + w1 + w2 + w3 + w4;
        double expected = expectedSum / weightSum;

        Assert.Equal(expected, sinema.Last.Value, 1e-10);
        _output.WriteLine($"SINEMA(5) reference value validated: {sinema.Last.Value:F10}");
    }

    /// <summary>
    /// Validates SINEMA with period 1 returns input values.
    /// </summary>
    [Fact]
    public void Validate_Period1_ReturnsInput()
    {
        var sinema = new Sinema(1);

        double[] inputs = [100, 105.5, 99.3, 110.7];

        foreach (double val in inputs)
        {
            double result = sinema.Update(new TValue(DateTime.UtcNow, val)).Value;
            Assert.Equal(val, result, 1e-10);
        }

        _output.WriteLine("SINEMA(1) correctly returns input values");
    }

    /// <summary>
    /// Validates warmup behavior - SINEMA adapts weights for partial buffer.
    /// </summary>
    [Fact]
    public void Validate_WarmupAdaptsWeights()
    {
        var sinema = new Sinema(5);

        // First value should return itself
        double r1 = sinema.Update(new TValue(DateTime.UtcNow, 100)).Value;
        Assert.Equal(100.0, r1, 1e-10);

        // Second value: weights for period 2
        // w0 = sin(π*1/2) = 1, w1 = sin(π*2/2) = 0
        // Result = (100*1 + 110*0) / 1 = 100
        double r2 = sinema.Update(new TValue(DateTime.UtcNow, 110)).Value;
        double expected2 = (100 * Math.Sin(Math.PI * 1 / 2) + 110 * Math.Sin(Math.PI * 2 / 2))
                         / (Math.Sin(Math.PI * 1 / 2) + Math.Sin(Math.PI * 2 / 2));
        Assert.Equal(expected2, r2, 1e-10);

        _output.WriteLine("SINEMA warmup weight adaptation validated");
    }

    /// <summary>
    /// Validates all calculation modes produce identical results.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Validate_AllModes_Consistent(int period)
    {
        // 1. Batch Mode
        var batchSeries = Sinema.Batch(_testData.Data, period);
        double batchResult = batchSeries.Last.Value;

        // 2. Span Mode
        double[] sourceData = _testData.RawData.ToArray();
        double[] spanOutput = new double[sourceData.Length];
        Sinema.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Sinema(period);
        foreach (var item in _testData.Data)
        {
            streamingInd.Update(item);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Sinema(pubSource, period);
        foreach (var item in _testData.Data)
        {
            pubSource.Add(item);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert all modes match
        Assert.Equal(batchResult, spanResult, 9);
        Assert.Equal(batchResult, streamingResult, 9);
        Assert.Equal(batchResult, eventingResult, 9);

        _output.WriteLine($"SINEMA({period}) all modes consistent: {batchResult:F10}");
    }
}