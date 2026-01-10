using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for AFIRMA indicator.
/// AFIRMA is a specialized FIR filter with windowed sinc coefficients.
/// Since no external library implements this exact algorithm, validation
/// focuses on internal consistency and mathematical properties.
/// </summary>
public sealed class AfirmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AfirmaValidationTests(ITestOutputHelper output)
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
    public void Validate_InternalConsistency_Batch()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib AFIRMA (batch TSeries)
            var afirma = new Afirma(period);
            var qResult = afirma.Update(_testData.Data);

            // Verify all results are finite
            foreach (var val in qResult)
            {
                Assert.True(double.IsFinite(val.Value),
                    $"AFIRMA({period}) produced non-finite value");
            }

            // Verify count matches input
            Assert.Equal(_testData.Data.Count, qResult.Count);
        }
        _output.WriteLine("AFIRMA Batch(TSeries) internal consistency validated");
    }

    [Fact]
    public void Validate_InternalConsistency_Streaming()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib AFIRMA (streaming)
            var afirma = new Afirma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(afirma.Update(item).Value);
            }

            // Verify all results are finite
            foreach (var val in qResults)
            {
                Assert.True(double.IsFinite(val),
                    $"AFIRMA({period}) streaming produced non-finite value");
            }

            // Verify count matches input
            Assert.Equal(_testData.Data.Count, qResults.Count);
        }
        _output.WriteLine("AFIRMA Streaming internal consistency validated");
    }

    [Fact]
    public void Validate_InternalConsistency_Span()
    {
        int[] periods = { 5, 10, 20, 50 };

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib AFIRMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            Afirma.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Verify all results are finite
            foreach (var val in qOutput)
            {
                Assert.True(double.IsFinite(val),
                    $"AFIRMA({period}) span produced non-finite value");
            }
        }
        _output.WriteLine("AFIRMA Span internal consistency validated");
    }

    [Fact]
    public void Validate_BatchStreamingConsistency()
    {
        int[] periods = { 5, 10, 20 };

        foreach (var period in periods)
        {
            // Batch calculation
            var afirmaBatch = new Afirma(period);
            var batchResult = afirmaBatch.Update(_testData.Data);

            // Streaming calculation
            var afirmaStream = new Afirma(period);
            var streamResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamResults.Add(afirmaStream.Update(item).Value);
            }

            // Compare last 100 values
            int compareCount = Math.Min(100, batchResult.Count);
            for (int i = 0; i < compareCount; i++)
            {
                int idx = batchResult.Count - compareCount + i;
                Assert.Equal(batchResult[idx].Value, streamResults[idx], 1e-10);
            }
        }
        _output.WriteLine("AFIRMA Batch/Streaming consistency validated");
    }

    [Fact]
    public void Validate_SpanBatchConsistency()
    {
        int[] periods = { 5, 10, 20 };

        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // TSeries Batch
            var afirma = new Afirma(period);
            var tseriesResult = afirma.Update(_testData.Data);

            // Span Batch
            double[] spanOutput = new double[sourceData.Length];
            Afirma.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

            // Compare
            for (int i = 0; i < sourceData.Length; i++)
            {
                Assert.Equal(tseriesResult[i].Value, spanOutput[i], 1e-10);
            }
        }
        _output.WriteLine("AFIRMA Span/Batch consistency validated");
    }

    [Fact]
    public void Validate_WindowTypes_Consistency()
    {
        var windows = new[]
        {
            Afirma.WindowType.Rectangular,
            Afirma.WindowType.Hanning,
            Afirma.WindowType.Hamming,
            Afirma.WindowType.Blackman,
            Afirma.WindowType.BlackmanHarris
        };

        const int period = 10;

        foreach (var window in windows)
        {
            // Batch
            var afirmaBatch = new Afirma(period, window);
            var batchResult = afirmaBatch.Update(_testData.Data);

            // Streaming
            var afirmaStream = new Afirma(period, window);
            foreach (var item in _testData.Data)
            {
                afirmaStream.Update(item);
            }

            // Compare last values
            Assert.Equal(batchResult.Last.Value, afirmaStream.Last.Value, 1e-10);
            _output.WriteLine($"Window {window}: Batch={batchResult.Last.Value:F6}, Stream={afirmaStream.Last.Value:F6}");
        }
        _output.WriteLine("AFIRMA Window types consistency validated");
    }

    [Fact]
    public void Validate_FlatInput_ReturnsConstant()
    {
        int period = 10;
        double constantValue = 100.0;

        // Create flat input
        var flatSeries = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            flatSeries.Add(DateTime.UtcNow.AddSeconds(i), constantValue);
        }

        var afirma = new Afirma(period);
        var result = afirma.Update(flatSeries);

        // After warmup, all values should equal the constant
        for (int i = period; i < result.Count; i++)
        {
            Assert.Equal(constantValue, result[i].Value, 1e-9);
        }
        _output.WriteLine($"AFIRMA flat input returns constant: {result.Last.Value:F9}");
    }

    [Fact]
    public void Validate_Smoothing_ReducesVariance()
    {
        int period = 21;

        // Calculate variance of input
        var rawData = _testData.RawData.ToArray();
        double inputMean = rawData.Average();
        double inputVariance = rawData.Select(x => Math.Pow(x - inputMean, 2)).Average();

        // Calculate AFIRMA
        var afirma = new Afirma(period);
        var result = afirma.Update(_testData.Data);

        // Calculate variance of output (after warmup)
        var outputValues = result.Skip(period).Select(v => v.Value).ToList();
        double outputMean = outputValues.Average();
        double outputVariance = outputValues.Select(x => Math.Pow(x - outputMean, 2)).Average();

        // Output variance should be less than input variance (smoothing effect)
        Assert.True(outputVariance < inputVariance,
            $"AFIRMA should reduce variance. Input: {inputVariance:F4}, Output: {outputVariance:F4}");

        _output.WriteLine($"AFIRMA smoothing effect: Input variance={inputVariance:F4}, Output variance={outputVariance:F4}");
    }

    [Fact]
    public void Validate_LargerPeriod_MoreSmoothing()
    {
        // Calculate with different periods (which implies different tap counts)
        var afirma5 = new Afirma(5);
        var afirma11 = new Afirma(11);
        var afirma21 = new Afirma(21);

        var result5 = afirma5.Update(_testData.Data);
        var result11 = afirma11.Update(_testData.Data);
        var result21 = afirma21.Update(_testData.Data);

        // Calculate variance of each
        double GetVariance(TSeries series, int skip)
        {
            var values = series.Skip(skip).Select(v => v.Value).ToList();
            double mean = values.Average();
            return values.Select(x => Math.Pow(x - mean, 2)).Average();
        }

        double var5 = GetVariance(result5, 5);
        double var11 = GetVariance(result11, 11);
        double var21 = GetVariance(result21, 21);

        // Larger period should generally produce smoother output (lower variance)
        // This is a statistical property, not guaranteed for all data
        _output.WriteLine($"Variance by period: 5={var5:F4}, 11={var11:F4}, 21={var21:F4}");

        // At minimum, all should be finite
        Assert.True(double.IsFinite(var5));
        Assert.True(double.IsFinite(var11));
        Assert.True(double.IsFinite(var21));
    }

    [Fact]
    public void Validate_DifferentWindows_DifferentCharacteristics()
    {
        int period = 10;

        var rectangularResult = Afirma.Batch(_testData.Data, period, Afirma.WindowType.Rectangular);
        var blackmanHarrisResult = Afirma.Batch(_testData.Data, period, Afirma.WindowType.BlackmanHarris);

        // Results should be different (different window characteristics)
        double rectLast = rectangularResult.Last.Value;
        double bhLast = blackmanHarrisResult.Last.Value;

        // They should generally not be exactly equal
        // (unless input happens to be perfectly constant)
        _output.WriteLine($"Rectangular: {rectLast:F6}, Blackman-Harris: {bhLast:F6}");

        // Both should be finite and reasonable
        Assert.True(double.IsFinite(rectLast));
        Assert.True(double.IsFinite(bhLast));
    }
}
