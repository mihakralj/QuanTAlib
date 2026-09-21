using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class QrmaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public QrmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Batch_Vs_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib QRMA (batch TSeries)
            var qrma = new global::QuanTAlib.Qrma(period);
            var batchResult = qrma.Update(_testData.Data);

            // Calculate QuanTAlib QRMA (streaming)
            var qrmaStreaming = new global::QuanTAlib.Qrma(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(qrmaStreaming.Update(item).Value);
            }

            // Compare all records
            Assert.Equal(batchResult.Count, streamingResults.Count);
            for (int i = 0; i < batchResult.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-9);
            }
        }
        _output.WriteLine("QRMA Batch(TSeries) vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_Span_Vs_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib QRMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Qrma.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate QuanTAlib QRMA (streaming)
            var qrmaStreaming = new global::QuanTAlib.Qrma(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(qrmaStreaming.Update(item).Value);
            }

            // Compare all records
            for (int i = 0; i < qOutput.Length; i++)
            {
                Assert.Equal(streamingResults[i], qOutput[i], 1e-9);
            }
        }
        _output.WriteLine("QRMA Span vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var (results, indicator) = global::QuanTAlib.Qrma.Calculate(_testData.Data, period);

            Assert.True(indicator.IsHot);
            Assert.Equal(results.Count, _testData.Data.Count);
            Assert.True(double.IsFinite(indicator.Last.Value));

            // The hot indicator should continue to produce valid results
            var nextResult = indicator.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.True(double.IsFinite(nextResult.Value));
        }
        _output.WriteLine("QRMA Calculate returns hot indicator validated successfully");
    }

    [Fact]
    public void Validate_LinearData_ExactFit()
    {
        // For linear data y = 2x + 5, quadratic regression should fit exactly
        const int period = 14;
        const int count = 100;
        var values = new double[count];
        var output = new double[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = (2.0 * i) + 5.0;
        }

        global::QuanTAlib.Qrma.Batch(values, output, period);

        // After warmup, should match perfectly (linear is subset of quadratic)
        for (int i = period; i < count; i++)
        {
            Assert.Equal(values[i], output[i], 1e-6);
        }
        _output.WriteLine("QRMA linear data exact fit validated successfully");
    }

    [Fact]
    public void Validate_QuadraticData_ExactFit()
    {
        // For quadratic data y = 0.5x² + x + 3, quadratic regression should fit exactly
        const int period = 14;
        const int count = 100;
        var values = new double[count];
        var output = new double[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = (0.5 * i * i) + i + 3.0;
        }

        global::QuanTAlib.Qrma.Batch(values, output, period);

        // After warmup, should match well (quadratic model fits quadratic data exactly)
        for (int i = period; i < count; i++)
        {
            Assert.Equal(values[i], output[i], 1e-3);
        }
        _output.WriteLine("QRMA quadratic data exact fit validated successfully");
    }
}
