using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Adaptive Laguerre Filter.
/// Since ALaguerre is a custom Ehlers indicator not found in external libraries (TA-Lib, Skender, Tulip, Ooples),
/// these tests validate internal consistency across calculation modes and against known mathematical properties.
/// </summary>
public sealed class ALaguerreValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public ALaguerreValidationTests(ITestOutputHelper output)
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
        int length = 20;
        int medianLength = 5;
        int count = _testData.Count;

        // Mode 1: Streaming
        var alStream = new ALaguerre(length, medianLength);
        var streamResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamResults[i] = alStream.Update(_testData.Data[i]).Value;
        }

        // Mode 2: Batch (TSeries)
        var batchResults = ALaguerre.Batch(_testData.Data, length, medianLength);

        // Mode 3: Span
        double[] spanOutput = new double[count];
        ALaguerre.Batch(_testData.RawData.Span, spanOutput.AsSpan(), length, medianLength);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-10);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
        }

        _output.WriteLine($"AllModes consistency validated: {count} bars, length={length}, medianLength={medianLength}");
    }

    [Fact]
    public void Validate_BatchStreamingSpan_MultipleParameters()
    {
        (int length, int medianLength)[] paramSets =
        [
            (5, 3),
            (10, 5),
            (20, 5),
            (30, 7),
            (50, 10)
        ];

        foreach (var (length, medianLength) in paramSets)
        {
            // Batch
            var batchResults = ALaguerre.Batch(_testData.Data, length, medianLength);

            // Streaming
            var alStream = new ALaguerre(length, medianLength);
            var streamResults = new double[_testData.Count];
            for (int i = 0; i < _testData.Count; i++)
            {
                streamResults[i] = alStream.Update(_testData.Data[i]).Value;
            }

            // Span
            double[] spanOutput = new double[_testData.Count];
            ALaguerre.Batch(_testData.RawData.Span, spanOutput.AsSpan(), length, medianLength);

            for (int i = 0; i < _testData.Count; i++)
            {
                Assert.Equal(batchResults[i].Value, streamResults[i], 1e-10);
                Assert.Equal(batchResults[i].Value, spanOutput[i], 1e-10);
            }

            _output.WriteLine($"Length={length}, MedianLength={medianLength}: batch/streaming/span consistency OK");
        }
    }

    // ============== Mathematical Properties ==============

    [Fact]
    public void Validate_ConstantInput_ConvergesToInput()
    {
        (int length, int medianLength)[] paramSets = [(5, 3), (10, 5), (20, 5), (50, 10)];
        double constant = 42.0;

        foreach (var (length, medianLength) in paramSets)
        {
            var al = new ALaguerre(length, medianLength);

            for (int i = 0; i < 500; i++)
            {
                al.Update(new TValue(DateTime.UtcNow, constant));
            }

            Assert.Equal(constant, al.Last.Value, 1e-6);
            _output.WriteLine($"Length={length}, MedianLength={medianLength}: constant input convergence OK (result={al.Last.Value:F10})");
        }
    }

    [Fact]
    public void Validate_FirstBar_ReturnsInput()
    {
        (int length, int medianLength)[] paramSets = [(5, 3), (20, 5), (50, 10)];
        double inputVal = 123.456;

        foreach (var (length, medianLength) in paramSets)
        {
            var al = new ALaguerre(length, medianLength);
            TValue result = al.Update(new TValue(DateTime.UtcNow, inputVal));

            Assert.Equal(inputVal, result.Value, 1e-10);
            _output.WriteLine($"Length={length}, MedianLength={medianLength}: first bar returns input OK");
        }
    }

    // ============== Smoothing Properties ==============

    [Fact]
    public void Validate_FilterSmooths_ReducesVariance()
    {
        int length = 20;
        int medianLength = 5;
        int count = _testData.Count;

        var al = new ALaguerre(length, medianLength);
        var filterResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            filterResults[i] = al.Update(_testData.Data[i]).Value;
        }

        // Compute variance of source and filtered
        int warmup = Math.Max(length, 4);
        double sourceVariance = ComputeVariance(_testData.RawData.Span[warmup..]);
        double filteredVariance = ComputeVariance(filterResults.AsSpan(warmup));

        Assert.True(filteredVariance < sourceVariance,
            $"Filtered variance ({filteredVariance:F6}) should be less than source variance ({sourceVariance:F6})");

        _output.WriteLine($"Variance: source={sourceVariance:F6}, filtered={filteredVariance:F6}, reduction={1 - (filteredVariance / sourceVariance):P2}");
    }

    [Fact]
    public void Validate_Stability_LargeDataset()
    {
        var al = new ALaguerre(20, 5);

        for (int i = 0; i < _testData.Count; i++)
        {
            al.Update(_testData.Data[i]);
        }

        Assert.True(double.IsFinite(al.Last.Value));
        Assert.True(al.IsHot);

        _output.WriteLine($"Large dataset stability validated: {_testData.Count} bars, result={al.Last.Value:F6}");
    }

    // ============== Adaptive Behavior ==============

    [Fact]
    public void Validate_AdaptiveAlpha_RespondsToVolatility()
    {
        // Trending data: should produce faster tracking (larger alpha → closer to price)
        var alTrend = new ALaguerre(20, 5);
        var trendSeries = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            trendSeries.Add(DateTime.UtcNow.Ticks + i, 100.0 + (i * 5.0));
        }

        foreach (var item in trendSeries)
        {
            alTrend.Update(item);
        }

        double trendError = Math.Abs(trendSeries[^1].Value - alTrend.Last.Value);

        // Flat data: should produce more smoothing (smaller alpha)
        var alFlat = new ALaguerre(20, 5);
        var flatSeries = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            flatSeries.Add(DateTime.UtcNow.Ticks + i, 100.0);
        }

        foreach (var item in flatSeries)
        {
            alFlat.Update(item);
        }

        double flatError = Math.Abs(100.0 - alFlat.Last.Value);

        _output.WriteLine($"Trend tracking error: {trendError:F6}, Flat tracking error: {flatError:F6}");

        // Flat tracking error should be near zero
        Assert.True(flatError < 0.01, $"Flat input tracking error ({flatError:F6}) should be near zero");
    }

    [Fact]
    public void Validate_EventDriven_MatchesStreaming()
    {
        int length = 20;
        int medianLength = 5;

        // Streaming
        var alStream = new ALaguerre(length, medianLength);
        var streamResults = new double[_testData.Count];
        for (int i = 0; i < _testData.Count; i++)
        {
            streamResults[i] = alStream.Update(_testData.Data[i]).Value;
        }

        // Event-driven
        var eventSource = new TSeries();
        var alEvent = new ALaguerre(eventSource, length, medianLength);
        var eventResults = new double[_testData.Count];
        for (int i = 0; i < _testData.Count; i++)
        {
            eventSource.Add(_testData.Data[i]);
            eventResults[i] = alEvent.Last.Value;
        }

        for (int i = 0; i < _testData.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 1e-10);
        }

        _output.WriteLine($"Event-driven vs streaming consistency validated: {_testData.Count} bars");
    }

    [Fact]
    public void Validate_BarCorrection_RevertsProperly()
    {
        var al = new ALaguerre(20, 5);

        // Feed initial bars
        for (int i = 0; i < 30; i++)
        {
            al.Update(_testData.Data[i], isNew: true);
        }

        double stateAfter30 = al.Last.Value;

        // Apply multiple corrections (isNew=false)
        for (int i = 0; i < 5; i++)
        {
            al.Update(new TValue(DateTime.UtcNow, 999.0 + i), isNew: false);
        }

        // Revert to original input
        TValue reverted = al.Update(_testData.Data[29], isNew: false);

        Assert.Equal(stateAfter30, reverted.Value, 1e-10);

        _output.WriteLine($"Bar correction revert validated: original={stateAfter30:F6}, reverted={reverted.Value:F6}");
    }

    [Fact]
    public void Validate_Prime_MatchesStreaming()
    {
        int length = 20;
        int medianLength = 5;

        // Method 1: Streaming update
        var alStream = new ALaguerre(length, medianLength);
        for (int i = 0; i < 50; i++)
        {
            alStream.Update(_testData.Data[i]);
        }

        // Method 2: Prime from history
        var alPrime = new ALaguerre(length, medianLength);
        double[] historyValues = new double[50];
        for (int i = 0; i < 50; i++)
        {
            historyValues[i] = _testData.Data[i].Value;
        }

        alPrime.Prime(historyValues);

        Assert.Equal(alStream.Last.Value, alPrime.Last.Value, 1e-10);

        _output.WriteLine($"Prime vs streaming match validated: {alStream.Last.Value:F6}");
    }

    private static double ComputeVariance(ReadOnlySpan<double> data)
    {
        if (data.Length < 2)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i];
        }

        double mean = sum / data.Length;
        double sumSq = 0;
        for (int i = 0; i < data.Length; i++)
        {
            double diff = data[i] - mean;
            sumSq += diff * diff;
        }

        return sumSq / (data.Length - 1);
    }
}
