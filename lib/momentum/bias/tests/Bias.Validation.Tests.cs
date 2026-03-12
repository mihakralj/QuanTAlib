using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Bias indicator.
/// Validates against mathematical calculations since BIAS = (Price - SMA) / SMA.
/// No direct TA-Lib/Tulip/Skender equivalent exists.
/// </summary>
public sealed class BiasValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public BiasValidationTests(ITestOutputHelper output)
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
        const int period = 10;
        var bias = new Bias(period);
        var qResult = bias.Update(_testData.Data);

        var rawData = _testData.RawData.ToArray();

        for (int i = 0; i < rawData.Length; i++)
        {
            // Calculate SMA manually
            double sum = 0;
            int startIdx = Math.Max(0, i - period + 1);
            int windowSize = i - startIdx + 1;
            for (int j = startIdx; j <= i; j++)
            {
                sum += rawData[j];
            }
            double sma = sum / windowSize;

            // BIAS = (Price - SMA) / SMA = Price/SMA - 1
            double expectedBias = sma != 0 ? (rawData[i] / sma) - 1.0 : 0;

            double qValue = qResult[i].Value;

            Assert.True(
                Math.Abs(qValue - expectedBias) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qValue:G17}, Expected={expectedBias:G17}");
        }

        _output.WriteLine("Bias Batch(TSeries) validated against manual calculation");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_Streaming()
    {
        int period = 10;
        var bias = new Bias(period);
        var qResults = new List<double>();
        var rawData = _testData.RawData.ToArray();

        foreach (var item in _testData.Data)
        {
            qResults.Add(bias.Update(item).Value);
        }

        for (int i = 0; i < rawData.Length; i++)
        {
            // Calculate SMA manually
            double sum = 0;
            int startIdx = Math.Max(0, i - period + 1);
            int windowSize = i - startIdx + 1;
            for (int j = startIdx; j <= i; j++)
            {
                sum += rawData[j];
            }
            double sma = sum / windowSize;

            // BIAS = (Price - SMA) / SMA = Price/SMA - 1
            double expectedBias = sma != 0 ? (rawData[i] / sma) - 1.0 : 0;

            Assert.True(
                Math.Abs(qResults[i] - expectedBias) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Expected={expectedBias:G17}");
        }

        _output.WriteLine("Bias Streaming validated against manual calculation");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_Span()
    {
        int period = 10;
        var sourceData = _testData.RawData.ToArray();
        var qOutput = new double[sourceData.Length];

        Bias.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

        for (int i = 0; i < sourceData.Length; i++)
        {
            // Calculate SMA manually
            double sum = 0;
            int startIdx = Math.Max(0, i - period + 1);
            int windowSize = i - startIdx + 1;
            for (int j = startIdx; j <= i; j++)
            {
                sum += sourceData[j];
            }
            double sma = sum / windowSize;

            // BIAS = (Price - SMA) / SMA = Price/SMA - 1
            double expectedBias = sma != 0 ? (sourceData[i] / sma) - 1.0 : 0;

            Assert.True(
                Math.Abs(qOutput[i] - expectedBias) <= ValidationHelper.DefaultTolerance,
                $"Mismatch at index {i}: QuanTAlib={qOutput[i]:G17}, Expected={expectedBias:G17}");
        }

        _output.WriteLine("Bias Span validated against manual calculation");
    }

    [Fact]
    public void Validate_KnownValues_UpTrend()
    {
        // Steadily increasing prices: bias should be positive after warmup
        double[] values = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];
        var bias = new Bias(5);

        for (int i = 0; i < values.Length; i++)
        {
            bias.Update(new TValue(DateTime.UtcNow, values[i]));

            // Calculate expected
            int startIdx = Math.Max(0, i - 4);
            double sum = 0;
            for (int j = startIdx; j <= i; j++)
            {
                sum += values[j];
            }
            double sma = sum / (i - startIdx + 1);
            double expectedBias = (values[i] / sma) - 1.0;

            Assert.Equal(expectedBias, bias.Last.Value, 1e-10);
        }

        // After warmup, bias should be positive (price above SMA)
        Assert.True(bias.Last.Value > 0, "Bias should be positive in uptrend");
        _output.WriteLine($"Uptrend bias: {bias.Last.Value:P4}");
    }

    [Fact]
    public void Validate_KnownValues_DownTrend()
    {
        // Steadily decreasing prices: bias should be negative after warmup
        double[] values = [110, 109, 108, 107, 106, 105, 104, 103, 102, 101, 100];
        var bias = new Bias(5);

        for (int i = 0; i < values.Length; i++)
        {
            bias.Update(new TValue(DateTime.UtcNow, values[i]));
        }

        // After warmup, bias should be negative (price below SMA)
        Assert.True(bias.Last.Value < 0, "Bias should be negative in downtrend");
        _output.WriteLine($"Downtrend bias: {bias.Last.Value:P4}");
    }

    [Fact]
    public void Validate_KnownValues_Constant()
    {
        // Constant prices: bias should be exactly 0
        double constant = 100.0;
        var bias = new Bias(10);

        for (int i = 0; i < 100; i++)
        {
            bias.Update(new TValue(DateTime.UtcNow, constant));
        }

        // Bias = (Price - SMA) / SMA = (100 - 100) / 100 = 0
        Assert.Equal(0.0, bias.Last.Value, 1e-10);
        _output.WriteLine("Constant sequence bias = 0 confirmed");
    }

    [Fact]
    public void Validate_KnownValues_SinglePriceSpike()
    {
        // 9 values at 100, then one spike to 200
        var bias = new Bias(10);
        for (int i = 0; i < 9; i++)
        {
            bias.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        bias.Update(new TValue(DateTime.UtcNow, 200.0));

        // SMA = (9 * 100 + 200) / 10 = 1100 / 10 = 110
        // BIAS = (200 / 110) - 1 = 1.8181818... - 1 = 0.8181818...
        double expectedSma = 110.0;
        double expectedBias = (200.0 / expectedSma) - 1.0;

        Assert.Equal(expectedBias, bias.Last.Value, 1e-10);
        _output.WriteLine($"Single spike bias: {bias.Last.Value:P4} (expected {expectedBias:P4})");
    }

    [Fact]
    public void Validate_KnownValues_PriceAtSMA()
    {
        // When current price equals SMA, bias should be 0
        // Use sequence where last value equals the average
        // Values: 90, 110, 90, 110, 100 → SMA(5) = 100, last price = 100 → bias = 0
        double[] values = [90, 110, 90, 110, 100];
        var bias = new Bias(5);

        foreach (var val in values)
        {
            bias.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(0.0, bias.Last.Value, 1e-10);
        _output.WriteLine("Price at SMA produces bias = 0 confirmed");
    }

    [Fact]
    public void Validate_NumericalStability_LargeValues()
    {
        // Test numerical stability with large values
        var bias = new Bias(100);
        double baseValue = 1e10;

        for (int i = 0; i < 1000; i++)
        {
            double value = baseValue + i;
            bias.Update(new TValue(DateTime.UtcNow, value));

            if (i >= 99) // After warmup
            {
                Assert.True(double.IsFinite(bias.Last.Value), $"Bias should be finite at index {i}");
            }
        }

        _output.WriteLine($"Large values stability test passed: {bias.Last.Value:G10}");
    }

    [Fact]
    public void Validate_NumericalStability_SmallValues()
    {
        // Test with small values
        var bias = new Bias(10);
        double baseValue = 1e-10;

        for (int i = 0; i < 100; i++)
        {
            double value = baseValue * (1 + i * 0.01);
            bias.Update(new TValue(DateTime.UtcNow, value));

            Assert.True(double.IsFinite(bias.Last.Value), $"Bias should be finite at index {i}");
        }

        _output.WriteLine($"Small values stability test passed: {bias.Last.Value:G10}");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int period = 20;
        var sourceData = _testData.RawData.ToArray();

        // Mode 1: TSeries Batch
        var bias1 = new Bias(period);
        var batchResult = bias1.Update(_testData.Data);

        // Mode 2: Streaming
        var bias2 = new Bias(period);
        var streamingResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamingResults.Add(bias2.Update(item).Value);
        }

        // Mode 3: Span
        var spanOutput = new double[sourceData.Length];
        Bias.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

        // Compare all three
        for (int i = 0; i < sourceData.Length; i++)
        {
            double batchVal = batchResult[i].Value;
            double streamVal = streamingResults[i];
            double spanVal = spanOutput[i];

            Assert.Equal(batchVal, streamVal, 1e-10);
            Assert.Equal(batchVal, spanVal, 1e-10);
        }

        _output.WriteLine("All Bias calculation modes produce consistent results");
    }

    [Fact]
    public void Validate_MultiplePeriods()
    {
        int[] periods = [5, 10, 20, 50, 100];
        var rawData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var bias = new Bias(period);
            var qResult = bias.Update(_testData.Data);

            // Verify last 50 values
            for (int i = rawData.Length - 50; i < rawData.Length; i++)
            {
                // Calculate SMA manually
                double sum = 0;
                int startIdx = Math.Max(0, i - period + 1);
                int windowSize = i - startIdx + 1;
                for (int j = startIdx; j <= i; j++)
                {
                    sum += rawData[j];
                }
                double sma = sum / windowSize;
                double expectedBias = sma != 0 ? (rawData[i] / sma) - 1.0 : 0;

                Assert.True(
                    Math.Abs(qResult[i].Value - expectedBias) <= ValidationHelper.DefaultTolerance,
                    $"Period {period}, index {i}: QuanTAlib={qResult[i].Value:G17}, Expected={expectedBias:G17}");
            }
        }

        _output.WriteLine("Bias validated for multiple periods");
    }

    [Fact]
    public void Validate_PercentageInterpretation()
    {
        // Bias of 0.05 means price is 5% above SMA
        // Bias of -0.05 means price is 5% below SMA
        var bias = new Bias(10);

        // Create scenario where we know the exact bias
        // SMA will be 100, price will be 105 → bias = 0.05
        for (int i = 0; i < 9; i++)
        {
            bias.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        // For 10th value: need SMA = 100 and price = 105
        // SMA of (9 * 100 + x) / 10 = 100 → x = 100
        // So we add another 100 first
        bias.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, bias.Last.Value, 1e-10);

        // Now add one more value at 105 (old 100 drops out, new comes in)
        bias.Update(new TValue(DateTime.UtcNow, 105.0));
        // SMA = (9 * 100 + 105) / 10 = 1005 / 10 = 100.5
        // Bias = (105 / 100.5) - 1 = 1.04477... - 1 ≈ 0.04478
        double expectedSma = 100.5;
        double expectedBias = (105.0 / expectedSma) - 1.0;
        Assert.Equal(expectedBias, bias.Last.Value, 1e-10);

        _output.WriteLine($"Percentage interpretation validated: {bias.Last.Value:P4}");
    }

    [Fact]
    public void Validate_AgainstSmaIndicator()
    {
        // Cross-validate with Sma indicator
        int period = 20;
        var bias = new Bias(period);
        var sma = new Sma(period);

        foreach (var item in _testData.Data)
        {
            var biasResult = bias.Update(item);
            var smaResult = sma.Update(item);

            // BIAS = (Price - SMA) / SMA = Price/SMA - 1
            double expectedBias = smaResult.Value != 0
                ? (item.Value / smaResult.Value) - 1.0
                : 0;

            Assert.Equal(expectedBias, biasResult.Value, 1e-10);
        }

        _output.WriteLine("Bias validated against Sma indicator");
    }

    [Fact]
    public void Validate_OscillatingSequence()
    {
        // Oscillating around a mean: bias should oscillate around 0
        var bias = new Bias(10);
        double mean = 100.0;
        double amplitude = 10.0;

        var biasValues = new List<double>();
        for (int i = 0; i < 100; i++)
        {
            double value = mean + amplitude * Math.Sin(i * 0.5);
            bias.Update(new TValue(DateTime.UtcNow, value));
            if (i >= 9) // After warmup
            {
                biasValues.Add(bias.Last.Value);
            }
        }

        // Average bias should be close to 0 for oscillating sequence
        double avgBias = biasValues.Average();
        Assert.True(Math.Abs(avgBias) < 0.01, $"Average bias should be near 0, got {avgBias}");

        // Should have both positive and negative values
        Assert.True(biasValues.Any(b => b > 0), "Should have positive bias values");
        Assert.True(biasValues.Any(b => b < 0), "Should have negative bias values");

        _output.WriteLine($"Oscillating sequence: avg bias = {avgBias:F6}");
    }
}
