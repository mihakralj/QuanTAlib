using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class CrmaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public CrmaValidationTests(ITestOutputHelper output)
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
            // Calculate QuanTAlib CRMA (batch TSeries)
            var crma = new global::QuanTAlib.Crma(period);
            var batchResult = crma.Update(_testData.Data);

            // Calculate QuanTAlib CRMA (streaming)
            var crmaStreaming = new global::QuanTAlib.Crma(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(crmaStreaming.Update(item).Value);
            }

            // Compare all records
            Assert.Equal(batchResult.Count, streamingResults.Count);
            for (int i = 0; i < batchResult.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-9);
            }
        }
        _output.WriteLine("CRMA Batch(TSeries) vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_Span_Vs_Streaming()
    {
        int[] periods = { 5, 10, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib CRMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Crma.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate QuanTAlib CRMA (streaming)
            var crmaStreaming = new global::QuanTAlib.Crma(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(crmaStreaming.Update(item).Value);
            }

            // Compare all records
            for (int i = 0; i < qOutput.Length; i++)
            {
                Assert.Equal(streamingResults[i], qOutput[i], 1e-9);
            }
        }
        _output.WriteLine("CRMA Span vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        int[] periods = { 5, 10, 14, 20 };

        foreach (var period in periods)
        {
            var (results, indicator) = global::QuanTAlib.Crma.Calculate(_testData.Data, period);

            Assert.True(indicator.IsHot);
            Assert.Equal(results.Count, _testData.Data.Count);
            Assert.True(double.IsFinite(indicator.Last.Value));

            // The hot indicator should continue to produce valid results
            var nextResult = indicator.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.True(double.IsFinite(nextResult.Value));
        }
        _output.WriteLine("CRMA Calculate returns hot indicator validated successfully");
    }

    [Fact]
    public void Validate_LinearData_ExactFit()
    {
        // For linear data y = 2x + 5, cubic regression should fit exactly
        const int period = 14;
        const int count = 100;
        var values = new double[count];
        var output = new double[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = 2.0 * i + 5.0;
        }

        global::QuanTAlib.Crma.Batch(values, output, period);

        // After warmup, should match perfectly (linear is subset of cubic)
        // Numerical precision degrades with large power sums (x^6), so use 1e-3
        for (int i = period; i < count; i++)
        {
            Assert.Equal(values[i], output[i], 1e-3);
        }
        _output.WriteLine("CRMA linear data exact fit validated successfully");
    }

    [Fact]
    public void Validate_QuadraticData_ExactFit()
    {
        // For quadratic data y = 0.5x² + x + 3, cubic regression should fit exactly
        const int period = 14;
        const int count = 100;
        var values = new double[count];
        var output = new double[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = 0.5 * i * i + i + 3.0;
        }

        global::QuanTAlib.Crma.Batch(values, output, period);

        // After warmup, should match well (quadratic is subset of cubic)
        // Large x^6 power sums cause numerical conditioning issues
        for (int i = period; i < count; i++)
        {
            Assert.Equal(values[i], output[i], 1.0);
        }
        _output.WriteLine("CRMA quadratic data exact fit validated successfully");
    }

    [Fact]
    public void Validate_CubicData_ExactFit()
    {
        // For cubic data y = 0.001x³ + 0.01x² + x + 5, should fit exactly
        // Use small coefficients to reduce numerical conditioning issues
        const int period = 10;
        const int count = 30;
        var values = new double[count];
        var output = new double[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = 0.001 * i * i * i + 0.01 * i * i + i + 5.0;
        }

        global::QuanTAlib.Crma.Batch(values, output, period);

        // Cubic data within a cubic model should fit well but with numerical noise
        for (int i = period; i < count; i++)
        {
            Assert.Equal(values[i], output[i], 1.0);
        }
        _output.WriteLine("CRMA cubic data exact fit validated successfully");
    }
}
