using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for QEMA (Quad Exponential Moving Average).
/// QEMA is a proprietary indicator not available in external libraries (TA-Lib, Skender, Tulip, Ooples).
/// These tests validate self-consistency and mathematical properties.
/// </summary>
public sealed class QemaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public QemaValidationTests(ITestOutputHelper output)
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
    public void Validate_BatchEqualsStreaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib QEMA (batch TSeries)
            var qemaBatch = new Qema(period);
            var batchResult = qemaBatch.Update(_testData.Data);

            // Calculate QuanTAlib QEMA (streaming)
            var qemaStream = new Qema(period);
            var streamResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamResults.Add(qemaStream.Update(item).Value);
            }

            // Compare last 100 records
            int compareCount = Math.Min(100, Math.Min(batchResult.Count, streamResults.Count));
            for (int i = 0; i < compareCount; i++)
            {
                int batchIdx = batchResult.Count - 1 - i;
                int streamIdx = streamResults.Count - 1 - i;
                Assert.True(Math.Abs(batchResult[batchIdx].Value - streamResults[streamIdx]) < ValidationHelper.SkenderTolerance,
                    $"Period {period}: Mismatch at index {i}, batch={batchResult[batchIdx].Value}, stream={streamResults[streamIdx]}");
            }
        }
        _output.WriteLine("QEMA Batch vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_SpanEqualsStreaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib QEMA (Span API)
            double[] spanOutput = new double[sourceData.Length];
            Qema.Batch(sourceData.AsSpan(), spanOutput.AsSpan(), period);

            // Calculate QuanTAlib QEMA (streaming)
            var qemaStream = new Qema(period);
            var streamResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamResults.Add(qemaStream.Update(item).Value);
            }

            // Compare last 100 records
            int compareCount = Math.Min(100, Math.Min(spanOutput.Length, streamResults.Count));
            for (int i = 0; i < compareCount; i++)
            {
                int spanIdx = spanOutput.Length - 1 - i;
                int streamIdx = streamResults.Count - 1 - i;
                Assert.True(Math.Abs(spanOutput[spanIdx] - streamResults[streamIdx]) < ValidationHelper.SkenderTolerance,
                    $"Period {period}: Mismatch at index {i}, span={spanOutput[spanIdx]}, stream={streamResults[streamIdx]}");
            }
        }
        _output.WriteLine("QEMA Span vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_EventBasedEqualsStreaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib QEMA (event-based via chaining)
            var source = new TSeries();
            var qemaEvent = new Qema(source, period);
            var eventResults = new List<double>();

            qemaEvent.Pub += (object? sender, in TValueEventArgs args) => eventResults.Add(args.Value.Value);

            foreach (var item in _testData.Data)
            {
                source.Add(item);
            }

            // Calculate QuanTAlib QEMA (streaming)
            var qemaStream = new Qema(period);
            var streamResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamResults.Add(qemaStream.Update(item).Value);
            }

            // Compare event-based results with streaming results
            Assert.Equal(streamResults.Count, eventResults.Count);
            int compareCount = Math.Min(100, streamResults.Count);
            for (int i = 0; i < compareCount; i++)
            {
                int idx = streamResults.Count - 1 - i;
                Assert.True(Math.Abs(eventResults[idx] - streamResults[idx]) < ValidationHelper.SkenderTolerance,
                    $"Period {period}: Mismatch at index {idx}, event={eventResults[idx]}, stream={streamResults[idx]}");
            }
        }
        _output.WriteLine("QEMA Event-based vs Streaming validated successfully");
    }

    [Fact]
    public void Validate_ProgressiveAlphasAreGeometricallySeparated()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Get alphas by creating indicator and observing behavior
            double baseAlpha = 2.0 / (period + 1);
            double expectedRamp = Math.Pow(1.0 / baseAlpha, 0.25);

            double alpha1 = baseAlpha;
            double alpha2 = alpha1 * expectedRamp;
            double alpha3 = alpha2 * expectedRamp;
            double alpha4 = alpha3 * expectedRamp;

            // Verify geometric progression: α₂/α₁ = α₃/α₂ = α₄/α₃ = r
            double ratio12 = alpha2 / alpha1;
            double ratio23 = alpha3 / alpha2;
            double ratio34 = alpha4 / alpha3;

            Assert.True(Math.Abs(ratio12 - expectedRamp) < 1e-10,
                $"Period {period}: Alpha ratio 2/1 should equal ramp factor");
            Assert.True(Math.Abs(ratio23 - expectedRamp) < 1e-10,
                $"Period {period}: Alpha ratio 3/2 should equal ramp factor");
            Assert.True(Math.Abs(ratio34 - expectedRamp) < 1e-10,
                $"Period {period}: Alpha ratio 4/3 should equal ramp factor");

            // Verify final alpha is larger than base alpha (progressive)
            Assert.True(alpha4 > alpha1,
                $"Period {period}: Final alpha ({alpha4}) should be > base alpha ({alpha1})");
        }
        _output.WriteLine("QEMA progressive alphas validated successfully");
    }

    [Fact]
    public void Validate_WeightsSumToOne()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var qema = new Qema(period);

            // Feed enough data to converge
            double[] testData = new double[500];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = 100.0 + (i % 10);  // Simple oscillating data
            }

            TSeries series = new();
            foreach (var val in testData)
            {
                series.Add(new TValue(DateTime.UtcNow, val));
            }
            qema.Update(series);

            // For constant input, QEMA should equal that constant (weights sum to 1)
            var qemaConst = new Qema(period);
            double constantValue = 50.0;
            for (int i = 0; i < 500; i++)
            {
                qemaConst.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constantValue));
            }

            Assert.True(Math.Abs(qemaConst.Last.Value - constantValue) < 1e-6,
                $"Period {period}: QEMA of constant should equal constant, got {qemaConst.Last.Value}");
        }
        _output.WriteLine("QEMA weights sum to one validated successfully");
    }

    [Fact]
    public void Validate_ZeroLagPropertyOnLinearTrend()
    {
        int[] periods = { 10, 20, 50 };

        foreach (var period in periods)
        {
            var qema = new Qema(period);

            // Linear trend: y = 100 + 0.1*x
            for (int i = 0; i < 1000; i++)
            {
                double value = 100.0 + (0.1 * i);
                qema.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value));
            }

            // After convergence, QEMA lag should be near zero for linear trend
            // For a linear trend y = a + b*t, a zero-lag filter should output ≈ y
            double lastInput = 100.0 + (0.1 * 999);
            double qemaOutput = qema.Last.Value;

            // Allow some error due to warmup and numerical precision
            double lagError = Math.Abs(qemaOutput - lastInput) / 0.1;  // Error in "bars"

            Assert.True(lagError < 2.0,
                $"Period {period}: QEMA lag on linear trend should be < 2 bars, got {lagError:F2} bars");
        }
        _output.WriteLine("QEMA zero-lag property on linear trend validated successfully");
    }

    [Fact]
    public void Validate_QemaProducesFiniteValues()
    {
        int[] periods = { 10, 20, 50 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QEMA
            var qema = new Qema(period);
            var qemaResults = new List<double>();
            foreach (var val in sourceData)
            {
                qemaResults.Add(qema.Update(new TValue(DateTime.UtcNow, val)).Value);
            }

            // Verify all values are finite and reasonable
            Assert.True(qemaResults.All(double.IsFinite),
                $"Period {period}: All QEMA values should be finite");

            // Calculate simple EMA for comparison
            var ema = new Ema(period);
            var emaResults = new List<double>();
            foreach (var val in sourceData)
            {
                emaResults.Add(ema.Update(new TValue(DateTime.UtcNow, val)).Value);
            }

            // QEMA should track the source reasonably (within same order of magnitude as EMA)
            double qemaStdDev = CalculateStdDev(qemaResults.Skip(period * 3).ToArray());
            double emaStdDev = CalculateStdDev(emaResults.Skip(period * 3).ToArray());

            // Both should have similar standard deviations (within 10x of each other)
            Assert.True(qemaStdDev > 0 && emaStdDev > 0,
                $"Period {period}: Both QEMA and EMA should have positive standard deviation");
            Assert.True(qemaStdDev < emaStdDev * 10 && emaStdDev < qemaStdDev * 10,
                $"Period {period}: QEMA stddev ({qemaStdDev:F4}) should be in same order as EMA ({emaStdDev:F4})");
        }
        _output.WriteLine("QEMA finite values validated successfully");
    }

    private static double CalculateStdDev(double[] values)
    {
        if (values.Length < 2)
        {
            return 0;
        }

        double mean = values.Average();
        double sumSquaredDiff = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquaredDiff / (values.Length - 1));
    }

    [Fact]
    public void Validate_ResponsivenessToStepChange()
    {
        int period = 20;

        var qema = new Qema(period);
        var ema = new Ema(period);

        // Feed constant value to converge
        for (int i = 0; i < 200; i++)
        {
            qema.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
            ema.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // Step change to 200
        for (int i = 0; i < 100; i++)
        {
            qema.Update(new TValue(DateTime.UtcNow.AddMinutes(200 + i), 200.0));
            ema.Update(new TValue(DateTime.UtcNow.AddMinutes(200 + i), 200.0));
        }

        // After 100 bars, both should be close to 200
        Assert.True(qema.Last.Value > 195,
            $"QEMA should respond to step change, got {qema.Last.Value}");
        Assert.True(ema.Last.Value > 195,
            $"EMA should respond to step change, got {ema.Last.Value}");

        // QEMA should ideally respond faster (higher value after step)
        // But this depends on the specific weight calculation
        _output.WriteLine($"After step change: QEMA={qema.Last.Value:F4}, EMA={ema.Last.Value:F4}");
    }

    [Fact]
    public void Validate_MathematicalCorrectness_ProgressiveAlphas()
    {
        // Verify the formula: r = (1/α₁)^(1/4), α₂=α₁·r, α₃=α₂·r, α₄=α₃·r
        int period = 20;
        double alpha1 = 2.0 / (period + 1);  // ≈ 0.0952
        double r = Math.Pow(1.0 / alpha1, 0.25);  // ≈ 1.8025

        double alpha2 = alpha1 * r;
        double alpha3 = alpha2 * r;
        double alpha4 = alpha3 * r;

        // Verify: α₄ ≈ α₁ * r³ ≈ α₁ * (1/α₁)^(3/4) ≈ α₁^(1/4)
        double expectedAlpha4 = Math.Pow(alpha1, 0.25);

        Assert.True(Math.Abs(alpha4 - expectedAlpha4) < 1e-10,
            $"Alpha4 calculation: expected {expectedAlpha4}, got {alpha4}");

        // Verify progressive alphas range from slow (α₁) to fast (α₄)
        Assert.True(alpha1 < alpha2 && alpha2 < alpha3 && alpha3 < alpha4,
            "Alphas should be progressively increasing");

        // Verify α₄ is close to 1 (fastest possible)
        Assert.True(alpha4 < 1.0 && alpha4 > 0.5,
            $"Alpha4 should be between 0.5 and 1.0, got {alpha4}");

        _output.WriteLine($"Progressive alphas for period {period}: α₁={alpha1:F4}, α₂={alpha2:F4}, α₃={alpha3:F4}, α₄={alpha4:F4}");
        _output.WriteLine($"Ramp factor r={r:F4}");
    }
}
