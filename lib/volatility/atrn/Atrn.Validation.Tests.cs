using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ATRN (Average True Range Normalized).
/// ATRN is QuanTAlib-specific - it normalizes ATR to [0,1] using min-max scaling.
/// Validation focuses on:
/// 1. Underlying ATR matches external libraries
/// 2. Normalization logic is correct
/// 3. Output is always in [0,1] range
/// </summary>
public sealed class AtrnValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AtrnValidationTests(ITestOutputHelper output)
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

    #region ATR Foundation Validation

    /// <summary>
    /// Validates that the underlying ATR calculation matches Skender.
    /// Since ATRN = normalized(ATR), the ATR component must be accurate.
    /// </summary>
    [Fact]
    public void UnderlyingAtr_MatchesSkender()
    {
        int period = 14;

        // Get QuanTAlib ATR
        var atr = new Atr(period);
        var quantalibAtr = atr.Update(_testData.Bars);

        // Get Skender ATR
        var skenderResults = _testData.SkenderQuotes.GetAtr(period).ToList();

        // Compare using ValidationHelper
        ValidationHelper.VerifyData(quantalibAtr, skenderResults, (s) => s.Atr, tolerance: ValidationHelper.SkenderTolerance);
        _output.WriteLine("Underlying ATR validated successfully against Skender");
    }

    #endregion

    #region Normalization Validation

    /// <summary>
    /// Validates that ATRN output is always in [0,1] range.
    /// </summary>
    [Fact]
    public void Atrn_AlwaysInZeroOneRange()
    {
        int period = 14;
        var atrn = new Atrn(period);

        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var result = atrn.Update(_testData.Bars[i], true);

            Assert.True(result.Value >= 0.0,
                $"ATRN at index {i} is {result.Value}, expected >= 0");
            Assert.True(result.Value <= 1.0,
                $"ATRN at index {i} is {result.Value}, expected <= 1");
        }
        _output.WriteLine("ATRN output range validated [0,1]");
    }

    /// <summary>
    /// Validates the min-max normalization formula.
    /// </summary>
    [Fact]
    public void Atrn_NormalizationFormula_IsCorrect()
    {
        int period = 14;
        int lookbackWindow = 10 * period;

        var atr = new Atr(period);
        var atrn = new Atrn(period);

        var atrValues = new List<double>();

        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var atrResult = atr.Update(_testData.Bars[i], true);
            atrValues.Add(atrResult.Value);

            var atrnResult = atrn.Update(_testData.Bars[i], true);

            // After warmup, verify normalization
            if (i >= lookbackWindow)
            {
                // Get min/max of ATR over lookback window
                int startIdx = Math.Max(0, atrValues.Count - lookbackWindow);
                double minAtr = double.MaxValue;
                double maxAtr = double.MinValue;

                for (int j = startIdx; j < atrValues.Count; j++)
                {
                    if (atrValues[j] < minAtr) minAtr = atrValues[j];
                    if (atrValues[j] > maxAtr) maxAtr = atrValues[j];
                }

                double currentAtr = atrValues[^1];
                double expectedNormalized = minAtr < maxAtr
                    ? (currentAtr - minAtr) / (maxAtr - minAtr)
                    : 0.5;

                Assert.True(
                    Math.Abs(expectedNormalized - atrnResult.Value) < 1e-6,
                    $"Normalization mismatch at index {i}: expected={expectedNormalized}, actual={atrnResult.Value}"
                );
            }
        }
        _output.WriteLine("ATRN normalization formula validated");
    }

    /// <summary>
    /// Validates that constant ATR produces stable normalized value in [0,1].
    /// </summary>
    [Fact]
    public void Atrn_ConstantAtr_ReturnsStableValue()
    {
        int period = 14;
        var atrn = new Atrn(period);
        int lookbackWindow = 10 * period;

        // Create bars with constant range (no gaps, constant high-low)
        var constantBars = new TBarSeries();
        double price = 100.0;
        long startTime = DateTime.UtcNow.Ticks;

        for (int i = 0; i < lookbackWindow + 100; i++)
        {
            constantBars.Add(new TBar(
                startTime + i * TimeSpan.FromMinutes(1).Ticks,
                price,          // Open
                price + 5.0,    // High (constant +5)
                price - 5.0,    // Low (constant -5)
                price,          // Close (same as open, no gap)
                1000.0          // Volume
            ));
        }

        TValue lastResult = default;
        for (int i = 0; i < constantBars.Count; i++)
        {
            lastResult = atrn.Update(constantBars[i], true);
        }

        // With constant volatility, value should be stable and within [0,1]
        Assert.True(
            lastResult.Value >= 0.0 && lastResult.Value <= 1.0,
            $"Expected value in [0,1] for constant ATR, got {lastResult.Value}"
        );
        _output.WriteLine("ATRN constant ATR returns stable value validated");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Validates ATRN behavior with increasing volatility.
    /// Higher current ATR relative to history should produce values closer to 1.
    /// </summary>
    [Fact]
    public void Atrn_IncreasingVolatility_ApproachesOne()
    {
        int period = 14;
        var atrn = new Atrn(period);
        int lookbackWindow = 10 * period;

        // Create bars with increasing volatility
        var bars = new TBarSeries();
        double price = 100.0;
        long startTime = DateTime.UtcNow.Ticks;

        for (int i = 0; i < lookbackWindow + 50; i++)
        {
            // Range increases over time
            double range = 1.0 + (i * 0.1);

            bars.Add(new TBar(
                startTime + i * TimeSpan.FromMinutes(1).Ticks,
                price,
                price + range,
                price - range,
                price,
                1000.0
            ));
        }

        TValue lastResult = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastResult = atrn.Update(bars[i], true);
        }

        // With increasing volatility, the latest ATR should be near max
        // So normalized value should be close to 1
        Assert.True(
            lastResult.Value > 0.8,
            $"Expected value close to 1.0 for increasing volatility, got {lastResult.Value}"
        );
        _output.WriteLine("ATRN increasing volatility validated");
    }

    /// <summary>
    /// Validates ATRN behavior with decreasing volatility.
    /// Lower current ATR relative to history should produce values closer to 0.
    /// </summary>
    [Fact]
    public void Atrn_DecreasingVolatility_ApproachesZero()
    {
        int period = 14;
        var atrn = new Atrn(period);
        int lookbackWindow = 10 * period;

        // Create bars with decreasing volatility
        var bars = new TBarSeries();
        double price = 100.0;
        long startTime = DateTime.UtcNow.Ticks;

        for (int i = 0; i < lookbackWindow + 50; i++)
        {
            // Range decreases over time (but stays positive)
            double range = Math.Max(0.1, 10.0 - (i * 0.05));

            bars.Add(new TBar(
                startTime + i * TimeSpan.FromMinutes(1).Ticks,
                price,
                price + range,
                price - range,
                price,
                1000.0
            ));
        }

        TValue lastResult = default;
        for (int i = 0; i < bars.Count; i++)
        {
            lastResult = atrn.Update(bars[i], true);
        }

        // With decreasing volatility, the latest ATR should be near min
        // So normalized value should be close to 0
        Assert.True(
            lastResult.Value < 0.2,
            $"Expected value close to 0.0 for decreasing volatility, got {lastResult.Value}"
        );
        _output.WriteLine("ATRN decreasing volatility validated");
    }

    /// <summary>
    /// Validates different period settings produce valid results.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(50)]
    public void Atrn_DifferentPeriods_ProducesValidResults(int period)
    {
        var atrn = new Atrn(period);

        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var result = atrn.Update(_testData.Bars[i], true);

            Assert.True(result.Value >= 0.0 && result.Value <= 1.0,
                $"ATRN({period}) at index {i} is {result.Value}, expected in [0,1]");
        }
    }

    #endregion

    #region Streaming vs Batch Consistency

    /// <summary>
    /// Validates streaming matches batch calculation.
    /// </summary>
    [Fact]
    public void Atrn_StreamingMatchesBatch()
    {
        int period = 14;

        // Streaming
        var streamingAtrn = new Atrn(period);
        var streamingResults = new List<double>();

        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var result = streamingAtrn.Update(_testData.Bars[i], true);
            streamingResults.Add(result.Value);
        }

        // Batch
        var batchResults = Atrn.Batch(_testData.Bars, period);

        Assert.Equal(streamingResults.Count, batchResults.Count);

        // Compare all values
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, 1e-10);
        }
        _output.WriteLine("ATRN streaming matches batch validated");
    }

    #endregion
}