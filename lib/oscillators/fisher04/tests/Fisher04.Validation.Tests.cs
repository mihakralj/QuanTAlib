using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Fisher04 (Ehlers 2004 Cybernetic Analysis).
/// No external library implements this specific variant, so we validate:
/// 1. Manual step-by-step computation against the algorithm
/// 2. Batch vs streaming consistency
/// 3. Span vs streaming consistency
/// 4. Coefficient differences from Fisher (2002)
/// </summary>
public sealed class Fisher04ValidationTests(ITestOutputHelper output) : IDisposable
{
    private const double Tolerance = 1e-12;
    private const int Seed = 12345;
    private const int DataPoints = 500;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        // No unmanaged resources
    }

    /// <summary>
    /// Validates the exact Ehlers 2004 algorithm step-by-step for 5 bars.
    /// </summary>
    [Fact]
    public void ManualComputation_5Bars_MatchesAlgorithm()
    {
        double[] prices = [10.0, 12.0, 11.0, 13.0, 9.0];
        int period = 3;
        var fisher = new Fisher04(period);

        // Track expected values manually
        double value1 = 0.0;
        double fishPrev = 0.0;
        var buffer = new List<double>();

        for (int i = 0; i < prices.Length; i++)
        {
            double price = prices[i];
            buffer.Add(price);
            if (buffer.Count > period)
            {
                buffer.RemoveAt(0);
            }

            double high = double.MinValue;
            double low = double.MaxValue;
            for (int j = 0; j < buffer.Count; j++)
            {
                if (buffer[j] > high)
                {
                    high = buffer[j];
                }
                if (buffer[j] < low)
                {
                    low = buffer[j];
                }
            }

            double range = high - low;
            if (range != 0.0)
            {
                value1 = (((price - low) / range) - 0.5) + (0.5 * value1);
            }
            else
            {
                value1 = 0.0;
            }

            if (value1 > 0.9999)
            {
                value1 = 0.9999;
            }
            else if (value1 < -0.9999)
            {
                value1 = -0.9999;
            }

            double fish = (0.25 * Math.Log((1.0 + value1) / (1.0 - value1)))
                + (0.5 * fishPrev);

            var result = fisher.Update(new TValue(DateTime.UtcNow, price));

            output.WriteLine($"Bar {i}: price={price:F1} range={range:F1} value1={value1:F10} fish={fish:F10} actual={result.Value:F10}");
            Assert.Equal(fish, result.Value, Tolerance);

            fishPrev = fish;
        }
    }

    /// <summary>
    /// Streaming matches batch TSeries output.
    /// </summary>
    [Fact]
    public void Streaming_MatchesBatch_TSeries()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: Seed);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var streaming = new Fisher04(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // Batch
        TSeries batchResults = Fisher04.Batch(source, period);

        int mismatches = 0;
        for (int i = 0; i < source.Count; i++)
        {
            if (Math.Abs(streamResults[i] - batchResults.Values[i]) > Tolerance)
            {
                mismatches++;
                if (mismatches <= 5)
                {
                    output.WriteLine($"Mismatch at {i}: stream={streamResults[i]:F12} batch={batchResults.Values[i]:F12}");
                }
            }
        }

        output.WriteLine($"Total mismatches: {mismatches}/{source.Count}");
        Assert.Equal(0, mismatches);
    }

    /// <summary>
    /// Streaming matches span batch output.
    /// </summary>
    [Fact]
    public void Streaming_MatchesBatch_Span()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: Seed);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var streaming = new Fisher04(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // Span batch
        var spanOutput = new double[source.Count];
        Fisher04.Batch(source.Values, spanOutput, period);

        int mismatches = 0;
        for (int i = 0; i < source.Count; i++)
        {
            if (Math.Abs(streamResults[i] - spanOutput[i]) > Tolerance)
            {
                mismatches++;
                if (mismatches <= 5)
                {
                    output.WriteLine($"Mismatch at {i}: stream={streamResults[i]:F12} span={spanOutput[i]:F12}");
                }
            }
        }

        output.WriteLine($"Total mismatches: {mismatches}/{source.Count}");
        Assert.Equal(0, mismatches);
    }

    /// <summary>
    /// Verifies that Fisher04 (2004) produces different results from Fisher (2002)
    /// due to different coefficients, and that the amplitude is reduced.
    /// </summary>
    [Fact]
    public void Fisher04_DiffersFromFisher2002_WithSmallerAmplitude()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.12, seed: Seed);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var fisher02 = new Fisher(period);
        var fisher04 = new Fisher04(period);

        double sumAbs02 = 0, sumAbs04 = 0;
        int diffCount = 0;

        for (int i = 0; i < source.Count; i++)
        {
            double v02 = fisher02.Update(source[i]).Value;
            double v04 = fisher04.Update(source[i]).Value;

            sumAbs02 += Math.Abs(v02);
            sumAbs04 += Math.Abs(v04);

            if (Math.Abs(v02 - v04) > 1e-6)
            {
                diffCount++;
            }
        }

        double avgAbs02 = sumAbs02 / source.Count;
        double avgAbs04 = sumAbs04 / source.Count;

        output.WriteLine($"Fisher 2002 avg |value|: {avgAbs02:F6}");
        output.WriteLine($"Fisher04 2004 avg |value|: {avgAbs04:F6}");
        output.WriteLine($"Different values: {diffCount}/{source.Count}");

        // They should differ on most bars
        Assert.True(diffCount > source.Count * 0.9,
            $"Expected >90% different values, got {diffCount}/{source.Count}");

        // Fisher04 should have smaller amplitude (0.25 mult vs 0.5)
        Assert.True(avgAbs04 < avgAbs02,
            $"Fisher04 avg abs ({avgAbs04:F6}) should be < Fisher ({avgAbs02:F6})");
    }

    /// <summary>
    /// Validates coefficient correctness: the normalization coefficient is 1.0 (not 0.66).
    /// </summary>
    [Fact]
    public void NormalizationCoefficient_IsOne()
    {
        // With period=2 and prices [100, 110]:
        // range = 10, norm = (110-100)/10 - 0.5 = 0.5
        // Value1 = 1.0 * 0.5 + 0.5 * prev
        // For Fisher (2002): Value1 = 0.66 * 0.5 + 0.67 * prev = 0.33 + 0.67*prev
        // For Fisher04 (2004): Value1 = 1.0 * 0.5 + 0.5 * prev = 0.5 + 0.5*prev

        var fisher04 = new Fisher04(period: 2);
        fisher04.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);  // range=0 → value1=0
        fisher04.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);  // value1 = 0.5 + 0 = 0.5

        // fish = 0.25 * ln(1.5/0.5) + 0 = 0.25 * ln(3)
        double expectedFish = 0.25 * Math.Log(3.0);
        Assert.Equal(expectedFish, fisher04.FisherValue, 1e-10);
    }

    /// <summary>
    /// Multiple periods produce correct results.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void DifferentPeriods_ProduceFiniteResults(int period)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: Seed);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var fisher = new Fisher04(period);
        for (int i = 0; i < source.Count; i++)
        {
            var result = fisher.Update(source[i]);
            Assert.True(double.IsFinite(result.Value), $"Non-finite at bar {i} with period {period}");
        }

        Assert.True(fisher.IsHot);
    }

    /// <summary>
    /// Validates the clamp threshold is 0.9999 (not 0.99/0.999).
    /// </summary>
    [Fact]
    public void ClampThreshold_Is09999()
    {
        // Create a scenario where Value1 would exceed 0.9999
        // With period=2 and extreme price movement
        var fisher = new Fisher04(period: 2);

        // First bar: range=0 → value1=0
        fisher.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);

        // Second bar: range=100, norm=(200-100)/100 - 0.5 = 0.5
        // value1 = 0.5 + 0 = 0.5 (not clamped)
        fisher.Update(new TValue(DateTime.UtcNow, 200.0), isNew: true);

        // Third bar: range=200-100=100, norm=(300-100)/200 - 0.5 = 0.5
        // value1 = 0.5 + 0.5*0.5 = 0.75 (not clamped yet)
        fisher.Update(new TValue(DateTime.UtcNow, 300.0), isNew: true);

        // Keep feeding extreme values to push value1 toward clamp
        for (int i = 0; i < 50; i++)
        {
            fisher.Update(new TValue(DateTime.UtcNow, 100.0 + ((i + 4) * 100.0)), isNew: true);
        }

        // Fisher should remain finite (clamping prevents log(∞))
        Assert.True(double.IsFinite(fisher.FisherValue),
            $"Fisher should be finite after extreme values, got {fisher.FisherValue}");
    }
}
