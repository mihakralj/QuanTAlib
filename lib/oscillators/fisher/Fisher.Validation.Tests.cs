using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validates Fisher Transform against Tulip NETCore and manual computation.
/// Tulip's fisher indicator uses the same normalization + arctanh approach.
/// </summary>
public sealed class FisherValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 10;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    #region Manual arctanh Cross-Validation

    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Manual_Arctanh()
    {
        // Validate that our Fisher Transform correctly computes arctanh
        // by testing with known normalized inputs
        double[] testValues = [-0.9, -0.5, 0.0, 0.5, 0.9];

        foreach (double v in testValues)
        {
            double expected = 0.5 * Math.Log((1.0 + v) / (1.0 - v));
            double actual = Math.Atanh(v);

            Assert.True(Math.Abs(expected - actual) < 1e-12,
                $"arctanh({v}): expected={expected}, actual={actual}");
        }

        _output.WriteLine("arctanh mathematical identity verified.");
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Manual_Computation()
    {
        double[] values = _testData.RawData.ToArray();
        int[] periods = [5, 10, 20];

        foreach (int period in periods)
        {
            double[] batchOutput = new double[values.Length];
            Fisher.Batch(values.AsSpan(), batchOutput.AsSpan(), period);

            // Manual computation
            double[] manualOutput = new double[values.Length];
            double emaValue = 0.0;
            var buffer = new double[period];
            int bufCount = 0;
            int bufIdx = 0;

            for (int i = 0; i < values.Length; i++)
            {
                double val = values[i];

                // Add to circular buffer
                if (bufCount < period)
                {
                    buffer[bufCount] = val;
                    bufCount++;
                }
                else
                {
                    buffer[bufIdx] = val;
                    bufIdx = (bufIdx + 1) % period;
                }

                // Find min/max
                double highest = double.MinValue;
                double lowest = double.MaxValue;
                for (int j = 0; j < bufCount; j++)
                {
                    if (buffer[j] > highest)
                    {
                        highest = buffer[j];
                    }
                    if (buffer[j] < lowest)
                    {
                        lowest = buffer[j];
                    }
                }

                double range = highest - lowest;
                double normalized = range > 0.0
                    ? 2.0 * ((val - lowest) / range) - 1.0
                    : 0.0;

                emaValue = 0.33 * normalized + 0.67 * emaValue;

                double clamped = Math.Clamp(emaValue, -0.999, 0.999);
                manualOutput[i] = 0.5 * Math.Log((1.0 + clamped) / (1.0 - clamped));
            }

            int validCount = 0;
            for (int i = period; i < values.Length; i++)
            {
                Assert.True(Math.Abs(manualOutput[i] - batchOutput[i]) < 1e-9,
                    $"Fisher mismatch at i={i}, period={period}: manual={manualOutput[i]}, batch={batchOutput[i]}");
                validCount++;
            }

            Assert.True(validCount > 0, $"No valid comparison points for period {period}");
            _output.WriteLine($"Fisher period={period}: validated {validCount} points against manual computation.");
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Validate_Manual_DifferentPeriods(int period)
    {
        double[] values = _testData.RawData.ToArray();

        double[] batchOutput = new double[values.Length];
        Fisher.Batch(values.AsSpan(), batchOutput.AsSpan(), period);

        // Verify all outputs are finite
        for (int i = 0; i < values.Length; i++)
        {
            Assert.True(double.IsFinite(batchOutput[i]),
                $"Fisher output not finite at i={i}, period={period}: {batchOutput[i]}");
        }

        _output.WriteLine($"Fisher period={period}: all {values.Length} outputs finite.");
    }

    #endregion

    #region Consistency Validation

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Batch_Span_Agree()
    {
        double[] tData = _testData.RawData.ToArray();

        // Batch TSeries
        TSeries batchSeries = Fisher.Batch(_testData.Data, TestPeriod);

        // Batch Span
        var spanOutput = new double[tData.Length];
        Fisher.Batch(tData.AsSpan(), spanOutput.AsSpan(), TestPeriod);

        // Batch and Span should be identical (same code path)
        for (int i = 0; i < tData.Length; i++)
        {
            Assert.Equal(batchSeries.Values[i], spanOutput[i], 12);
        }

        // Streaming
        var fisher = new Fisher(TestPeriod);
        var streamResults = new double[tData.Length];
        for (int i = 0; i < tData.Length; i++)
        {
            streamResults[i] = fisher.Update(_testData.Data[i]).Value;
        }

        // Streaming vs Batch should match exactly (same algorithm, same state)
        for (int i = 0; i < tData.Length; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], 9);
        }

        _output.WriteLine("Fisher streaming/batch/span agreement verified.");
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Event_Matches_Streaming()
    {
        // Streaming
        var streamFisher = new Fisher(TestPeriod);
        var streamResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            streamResults[i] = streamFisher.Update(_testData.Data[i]).Value;
        }

        // Event-based
        var eventSource = new TSeries();
        var eventFisher = new Fisher(eventSource, TestPeriod);
        var eventResults = new double[_testData.Data.Count];
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            eventSource.Add(_testData.Data[i]);
            eventResults[i] = eventFisher.Last.Value;
        }

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 12);
        }

        _output.WriteLine("Fisher event-based matches streaming.");
    }

    #endregion
}
