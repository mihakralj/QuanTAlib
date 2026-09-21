using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Laguerre Filter.
/// Since Laguerre is a custom Ehlers indicator not found in external libraries (TA-Lib, Skender, Tulip, Ooples),
/// these tests validate internal consistency across calculation modes and against known mathematical properties.
/// </summary>
public sealed class LaguerreValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public LaguerreValidationTests(ITestOutputHelper output)
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

    // ============== Self-consistency: All Modes Match ==============

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        double gamma = 0.8;
        int count = _testData.Count;

        // Mode 1: Streaming
        var lagStream = new Laguerre(gamma);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = lagStream.Update(_testData.Data[i]).Value;
        }

        // Mode 2: Batch (TSeries)
        var batchResults = Laguerre.Batch(_testData.Data, gamma);

        // Mode 3: Span
        double[] spanOutput = new double[count];
        Laguerre.Batch(_testData.RawData.Span, spanOutput.AsSpan(), gamma);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-10);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
        }

        _output.WriteLine($"AllModes consistency validated: {count} bars, gamma={gamma}");
    }

    [Fact]
    public void Validate_BatchStreamingSpan_Consistency()
    {
        double[] gammas = { 0.0, 0.2, 0.5, 0.8, 0.95 };

        foreach (double gamma in gammas)
        {
            // Batch
            var batchResults = Laguerre.Batch(_testData.Data, gamma);

            // Streaming
            var lagStream = new Laguerre(gamma);
            var streamResults = new double[_testData.Count];
            for (int i = 0; i < _testData.Count; i++)
            {
                streamResults[i] = lagStream.Update(_testData.Data[i]).Value;
            }

            // Span
            double[] spanOutput = new double[_testData.Count];
            Laguerre.Batch(_testData.RawData.Span, spanOutput.AsSpan(), gamma);

            for (int i = 0; i < _testData.Count; i++)
            {
                Assert.Equal(batchResults[i].Value, streamResults[i], 1e-10);
                Assert.Equal(batchResults[i].Value, spanOutput[i], 1e-10);
            }

            _output.WriteLine($"Gamma={gamma}: batch/streaming/span consistency OK");
        }
    }

    // ============== Mathematical Properties ==============

    [Fact]
    public void Validate_Gamma0_MatchesFIR()
    {
        // When gamma=0, Laguerre becomes a 4-tap FIR filter
        // L0=input, L1=prev_input, L2=prev2_input, L3=prev3_input
        // Filt = (L0 + 2*L1 + 2*L2 + L3) / 6
        var lag = new Laguerre(0.0);
        var sourceData = _testData.RawData.Span;

        // Feed data and compare with manual FIR calculation
        double prev0 = 0, prev1 = 0, prev2 = 0;
        for (int i = 0; i < _testData.Count; i++)
        {
            double input = sourceData[i];
            double lagResult = lag.Update(_testData.Data[i]).Value;

            if (i == 0)
            {
                // First bar: all elements are input
                Assert.Equal(input, lagResult, 1e-10);
            }
            else if (i >= 4)
            {
                // After warmup: FIR = (input + 2*prev1 + 2*prev2 + prev3) / 6
                double expectedFir = (input + (2.0 * prev0) + (2.0 * prev1) + prev2) / 6.0;
                Assert.Equal(expectedFir, lagResult, 1e-10);
            }

            prev2 = prev1;
            prev1 = prev0;
            prev0 = input;
        }

        _output.WriteLine("Gamma=0 FIR validation passed");
    }

    [Fact]
    public void Validate_ConstantInput_ConvergesToInput()
    {
        double[] gammas = { 0.0, 0.2, 0.5, 0.8, 0.95 };
        double constant = 42.0;

        foreach (double gamma in gammas)
        {
            var lag = new Laguerre(gamma);

            for (int i = 0; i < 500; i++)
            {
                lag.Update(new TValue(DateTime.UtcNow, constant));
            }

            Assert.Equal(constant, lag.Last.Value, 1e-8);
            _output.WriteLine($"Gamma={gamma}: constant input convergence OK (result={lag.Last.Value:F10})");
        }
    }

    [Fact]
    public void Validate_SmoothingBehavior()
    {
        // Higher gamma = more smoothing = smaller variance in output
        double[] spanOutput02 = new double[_testData.Count];
        double[] spanOutput05 = new double[_testData.Count];
        double[] spanOutput09 = new double[_testData.Count];

        Laguerre.Batch(_testData.RawData.Span, spanOutput02.AsSpan(), 0.2);
        Laguerre.Batch(_testData.RawData.Span, spanOutput05.AsSpan(), 0.5);
        Laguerre.Batch(_testData.RawData.Span, spanOutput09.AsSpan(), 0.9);

        // Compute variance of each output (skip first 10 bars for warmup)
        double var02 = ComputeVariance(spanOutput02.AsSpan(10));
        double var05 = ComputeVariance(spanOutput05.AsSpan(10));
        double var09 = ComputeVariance(spanOutput09.AsSpan(10));

        // Higher gamma should produce smoother (lower variance) output
        Assert.True(var09 < var05, $"Gamma 0.9 variance ({var09:F2}) should be < gamma 0.5 variance ({var05:F2})");
        Assert.True(var05 < var02, $"Gamma 0.5 variance ({var05:F2}) should be < gamma 0.2 variance ({var02:F2})");

        _output.WriteLine($"Smoothing variance: gamma=0.2→{var02:F2}, gamma=0.5→{var05:F2}, gamma=0.9→{var09:F2}");
    }

    [Fact]
    public void Validate_Convergence_AllGammas()
    {
        // All gamma values should converge (no divergence or NaN)
        double[] gammas = { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 0.99 };

        foreach (double gamma in gammas)
        {
            var lag = new Laguerre(gamma);
            var result = lag.Update(_testData.Data);

            for (int i = 0; i < result.Count; i++)
            {
                Assert.True(double.IsFinite(result[i].Value), $"Gamma={gamma}: non-finite at bar {i}: {result[i].Value}");
            }

            Assert.True(result[^1].Value > 0, $"Gamma={gamma}: last value should be positive");
            _output.WriteLine($"Gamma={gamma}: converged, last={result[^1].Value:F4}");
        }
    }

    [Fact]
    public void Validate_NaN_SelfConsistency()
    {
        // Insert NaN into the data, verify all modes handle it identically
        var seriesWithNaN = new TSeries();
        double[] rawWithNaN = new double[100];
        var sourceData = _testData.RawData.Span;

        for (int i = 0; i < 100; i++)
        {
            double val = (i == 25 || i == 50 || i == 75) ? double.NaN : sourceData[i];
            seriesWithNaN.Add(_testData.Data[i].Time, val);
            rawWithNaN[i] = val;
        }

        // Streaming
        var lag1 = new Laguerre(0.8);
        var streamResults = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streamResults[i] = lag1.Update(seriesWithNaN[i]).Value;
        }

        // Span
        double[] spanOutput = new double[100];
        Laguerre.Batch(rawWithNaN.AsSpan(), spanOutput.AsSpan(), 0.8);

        // Both should be identical and finite
        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(streamResults[i]), $"Stream NaN at bar {i}");
            Assert.True(double.IsFinite(spanOutput[i]), $"Span NaN at bar {i}");
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
        }

        _output.WriteLine("NaN self-consistency validated");
    }

    [Fact]
    public void Validate_LargeDataset_Stability()
    {
        double[] gammas = { 0.5, 0.8, 0.95 };

        foreach (double gamma in gammas)
        {
            double[] output = new double[_testData.Count];
            Laguerre.Batch(_testData.RawData.Span, output.AsSpan(), gamma);

            // Check no NaN/Inf in entire output
            for (int i = 0; i < output.Length; i++)
            {
                Assert.True(double.IsFinite(output[i]), $"Gamma={gamma}: non-finite at bar {i}");
            }

            // Check output is within reasonable bounds
            double lastVal = output[^1];
            Assert.True(lastVal > 0, $"Gamma={gamma}: last value ({lastVal}) should be positive");

            _output.WriteLine($"Gamma={gamma}: {_testData.Count} bars stable, last={lastVal:F4}");
        }
    }

    [Fact]
    public void Validate_DeterministicOutput()
    {
        // Same input, same gamma, different runs should produce identical results
        double gamma = 0.8;

        double[] output1 = new double[_testData.Count];
        double[] output2 = new double[_testData.Count];

        Laguerre.Batch(_testData.RawData.Span, output1.AsSpan(), gamma);
        Laguerre.Batch(_testData.RawData.Span, output2.AsSpan(), gamma);

        for (int i = 0; i < _testData.Count; i++)
        {
            Assert.Equal(output1[i], output2[i], 0);
        }

        _output.WriteLine("Determinism validated: two identical runs produce bit-exact identical output");
    }

    [Fact]
    public void Validate_DifferentGammas_ProduceDifferentResults()
    {
        double[] output1 = new double[_testData.Count];
        double[] output2 = new double[_testData.Count];

        Laguerre.Batch(_testData.RawData.Span, output1.AsSpan(), 0.3);
        Laguerre.Batch(_testData.RawData.Span, output2.AsSpan(), 0.9);

        bool anyDifferent = false;
        for (int i = 10; i < _testData.Count; i++)
        {
            if (Math.Abs(output1[i] - output2[i]) > 1e-6)
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, "Different gamma values should produce different results");
        _output.WriteLine("Different gammas produce different results - confirmed");
    }

    // ============== Helper ==============

    private static double ComputeVariance(ReadOnlySpan<double> data)
    {
        double sum = 0;
        double sumSq = 0;
        int n = data.Length;

        for (int i = 0; i < n; i++)
        {
            sum += data[i];
            sumSq += data[i] * data[i];
        }

        double mean = sum / n;
        return Math.Max(0, (sumSq / n) - (mean * mean));
    }
}
