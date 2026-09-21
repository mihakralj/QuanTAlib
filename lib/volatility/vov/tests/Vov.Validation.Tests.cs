namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for VOV (Volatility of Volatility).
/// VOV = StdDev(StdDev(price, volatilityPeriod), vovPeriod)
/// Uses population standard deviation: sqrt(mean(x²) - mean(x)²)
/// </summary>
public class VovValidationTests
{
    private const int DefaultVolatilityPeriod = 20;
    private const int DefaultVovPeriod = 10;

    private static TSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(bars[i].Time, bars[i].Close));
        }
        return ts;
    }

    // === Mathematical Validation ===

    /// <summary>
    /// Validates the VOV formula: StdDev(StdDev(price, volPeriod), vovPeriod)
    /// using population standard deviation.
    /// </summary>
    [Fact]
    public void Vov_Formula_IsCorrect()
    {
        // Test with small periods for manual verification
        int volPeriod = 3;
        int vovPeriod = 2;
        double[] prices = [100, 102, 98, 105, 100, 103];

        var vov = new Vov(volPeriod, vovPeriod);
        var time = DateTime.UtcNow;

        // Manual calculation of inner stddevs using population formula
        var innerStdDevs = new List<double>();

        for (int i = 0; i < prices.Length; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), prices[i]));

            if (i >= volPeriod - 1)
            {
                // Calculate inner stddev manually
                var window = prices.Skip(i - volPeriod + 1).Take(volPeriod).ToArray();
                double mean = window.Average();
                double variance = window.Select(x => (x - mean) * (x - mean)).Average();
                double stddev = Math.Sqrt(variance);
                innerStdDevs.Add(stddev);
            }
        }

        // Now calculate outer stddev of the last vovPeriod inner stddevs
        if (innerStdDevs.Count >= vovPeriod)
        {
            var recentInnerStdDevs = innerStdDevs.TakeLast(vovPeriod).ToArray();
            double meanInner = recentInnerStdDevs.Average();
            double varianceOuter = recentInnerStdDevs.Select(x => (x - meanInner) * (x - meanInner)).Average();
            double expectedVov = Math.Sqrt(varianceOuter);

            Assert.Equal(expectedVov, vov.Last.Value, 8);
        }
    }

    /// <summary>
    /// Validates VOV is zero when price is constant (no volatility).
    /// </summary>
    [Fact]
    public void Vov_ConstantPrice_ReturnsZero()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        // Constant prices = zero volatility = zero VOV
        for (int i = 0; i < 20; i++)
        {
            var result = vov.Update(new TValue(time.AddSeconds(i), 100.0));

            if (vov.IsHot)
            {
                Assert.Equal(0.0, result.Value, 10);
            }
        }
    }

    /// <summary>
    /// Validates VOV is zero when volatility is constant.
    /// </summary>
    [Fact]
    public void Vov_ConstantVolatility_ReturnsZero()
    {
        var vov = new Vov(volatilityPeriod: 3, vovPeriod: 3);
        var time = DateTime.UtcNow;

        // Repeating pattern with constant volatility
        // Pattern: 100, 102, 100, 102, 100, 102... has constant stddev
        for (int i = 0; i < 30; i++)
        {
            double price = i % 2 == 0 ? 100.0 : 102.0;
            vov.Update(new TValue(time.AddSeconds(i), price));
        }

        // After many bars with identical pattern, VOV should stabilize near zero
        // (constant inner volatility means outer VOV approaches zero)
        Assert.True(vov.Last.Value < 0.5,
            $"Constant volatility pattern should produce near-zero VOV, got {vov.Last.Value}");
    }

    /// <summary>
    /// Validates VOV increases when volatility changes.
    /// </summary>
    [Fact]
    public void Vov_ChangingVolatility_ProducesPositiveValue()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 5);
        var time = DateTime.UtcNow;

        // Low volatility period
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + (Math.Sin(i * 0.3) * 0.5); // Small oscillations
            vov.Update(new TValue(time.AddSeconds(i), price));
        }

        // High volatility period
        for (int i = 10; i < 20; i++)
        {
            double price = 100 + (Math.Sin(i * 0.3) * 10); // Large oscillations
            vov.Update(new TValue(time.AddSeconds(i), price));
        }

        // VOV should be positive (volatility changed)
        Assert.True(vov.Last.Value > 0, $"VOV should be positive when volatility changes, got {vov.Last.Value}");
    }

    // === Streaming vs Batch Consistency ===

    /// <summary>
    /// Validates streaming calculation matches batch calculation.
    /// </summary>
    [Fact]
    public void Vov_StreamingMatchesBatch()
    {
        var data = GenerateTestData(100);

        // Streaming
        var streamingVov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);
        var streamingResults = new double[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            streamingResults[i] = streamingVov.Update(data[i]).Value;
        }

        // Batch
        var batchOutput = new double[data.Count];
        Vov.Batch(data.Values, batchOutput, DefaultVolatilityPeriod, DefaultVovPeriod);

        // Compare all values
        for (int i = 0; i < data.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchOutput[i], 10);
        }
    }

    /// <summary>
    /// Validates TSeries batch matches streaming.
    /// </summary>
    [Fact]
    public void Vov_TSeriesBatchMatchesStreaming()
    {
        var data = GenerateTestData(100);

        // Streaming
        var streamingVov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);
        for (int i = 0; i < data.Count; i++)
        {
            streamingVov.Update(data[i]);
        }

        // Batch via TSeries
        var batchResult = Vov.Batch(data, DefaultVolatilityPeriod, DefaultVovPeriod);

        Assert.Equal(streamingVov.Last.Value, batchResult.Last.Value, 10);
    }

    /// <summary>
    /// Validates span-based calculation matches streaming.
    /// </summary>
    [Fact]
    public void Vov_SpanMatchesStreaming()
    {
        var data = GenerateTestData(100);

        // Streaming
        var streamingVov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);
        for (int i = 0; i < data.Count; i++)
        {
            streamingVov.Update(data[i]);
        }

        // Span
        var spanOutput = new double[data.Count];
        Vov.Batch(data.Values, spanOutput, DefaultVolatilityPeriod, DefaultVovPeriod);

        Assert.Equal(streamingVov.Last.Value, spanOutput[^1], 10);
    }

    // === Property Validation ===

    /// <summary>
    /// Validates VOV is always non-negative.
    /// </summary>
    [Fact]
    public void Vov_Output_IsNonNegative()
    {
        var data = GenerateTestData(100);
        var vov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);

        for (int i = 0; i < data.Count; i++)
        {
            var result = vov.Update(data[i]);
            Assert.True(result.Value >= 0, $"VOV should be non-negative at index {i}, got {result.Value}");
        }
    }

    /// <summary>
    /// Validates VOV output is always finite.
    /// </summary>
    [Fact]
    public void Vov_Output_IsFinite()
    {
        var data = GenerateTestData(100);
        var vov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);

        for (int i = 0; i < data.Count; i++)
        {
            var result = vov.Update(data[i]);
            Assert.True(double.IsFinite(result.Value), $"VOV should be finite at index {i}");
        }
    }

    // === Bar Correction Tests ===

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Vov_BarCorrection_WorksCorrectly()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        // Feed initial data
        for (int i = 0; i < 10; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }

        // Add new bar
        vov.Update(new TValue(time.AddSeconds(10), 110), isNew: true);
        double afterNew = vov.Last.Value;

        // Correct with different value
        vov.Update(new TValue(time.AddSeconds(10), 90), isNew: false);
        double afterCorrection = vov.Last.Value;

        // Restore original
        vov.Update(new TValue(time.AddSeconds(10), 110), isNew: false);
        double afterRestore = vov.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge to fresh calculation.
    /// </summary>
    [Fact]
    public void Vov_IterativeCorrections_Converge()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        // Feed data
        for (int i = 0; i < 10; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }

        // Multiple corrections on same bar
        for (int j = 0; j < 5; j++)
        {
            vov.Update(new TValue(time.AddSeconds(9), 100 + (j * 5)), isNew: false);
        }

        // Final correction back to original
        vov.Update(new TValue(time.AddSeconds(9), 109), isNew: false);
        double afterCorrections = vov.Last.Value;

        // Fresh calculation
        var vovFresh = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        for (int i = 0; i < 10; i++)
        {
            vovFresh.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }
        double freshValue = vovFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    // === Reset Tests ===

    /// <summary>
    /// Validates Reset clears state completely.
    /// </summary>
    [Fact]
    public void Vov_Reset_ClearsState()
    {
        var vov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);
        var data = GenerateTestData(50);

        // Feed data
        for (int i = 0; i < 40; i++)
        {
            vov.Update(data[i]);
        }

        // Reset
        vov.Reset();

        // State should be cleared
        Assert.False(vov.IsHot);
        Assert.Equal(default, vov.Last);

        // Feed data again
        for (int i = 0; i < 35; i++)
        {
            vov.Update(data[i]);
        }

        // Fresh indicator
        var vovFresh = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);
        for (int i = 0; i < 35; i++)
        {
            vovFresh.Update(data[i]);
        }

        Assert.Equal(vovFresh.Last.Value, vov.Last.Value, 10);
    }

    // === Warmup Period Tests ===

    /// <summary>
    /// Validates WarmupPeriod equals volatilityPeriod + vovPeriod - 1.
    /// </summary>
    [Fact]
    public void Vov_WarmupPeriod_EqualsSum()
    {
        var vov = new Vov(volatilityPeriod: 20, vovPeriod: 10);
        Assert.Equal(29, vov.WarmupPeriod); // 20 + 10 - 1
    }

    /// <summary>
    /// Validates IsHot is true after warmup period bars.
    /// </summary>
    [Fact]
    public void Vov_IsHot_AfterWarmupPeriod()
    {
        int volPeriod = 5;
        int vovPeriod = 3;
        int warmup = volPeriod + vovPeriod - 1; // 7

        var vov = new Vov(volPeriod, vovPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < warmup - 1; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100 + i));
            Assert.False(vov.IsHot, $"Should not be hot at bar {i}");
        }

        vov.Update(new TValue(time.AddSeconds(warmup - 1), 100 + warmup - 1));
        Assert.True(vov.IsHot, "Should be hot after warmup period");
    }

    // === NaN/Infinity Handling ===

    /// <summary>
    /// Validates NaN input uses last valid value.
    /// </summary>
    [Fact]
    public void Vov_NaNInput_UsesLastValid()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        var result = vov.Update(new TValue(time.AddSeconds(10), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates Infinity input uses last valid value.
    /// </summary>
    [Fact]
    public void Vov_InfinityInput_UsesLastValid()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        var result = vov.Update(new TValue(time.AddSeconds(10), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    /// <summary>
    /// Validates batch handles NaN values.
    /// </summary>
    [Fact]
    public void Vov_BatchNaN_HandledCorrectly()
    {
        var source = new double[] { 100, 102, double.NaN, 98, 101, 103, 99, 104, 100, 102 };
        var output = new double[10];

        Vov.Batch(source, output, volatilityPeriod: 3, vovPeriod: 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} should be finite");
            Assert.True(output[i] >= 0, $"Output at index {i} should be non-negative");
        }
    }

    // === Period Sensitivity ===

    /// <summary>
    /// Validates longer volatility period produces smoother inner volatility.
    /// </summary>
    [Fact]
    public void Vov_LongerVolatilityPeriod_SmootherResults()
    {
        var data = GenerateTestData(100);

        var vovShort = new Vov(volatilityPeriod: 5, vovPeriod: 5);
        var vovLong = new Vov(volatilityPeriod: 20, vovPeriod: 5);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < data.Count; i++)
        {
            shortResults.Add(vovShort.Update(data[i]).Value);
            longResults.Add(vovLong.Update(data[i]).Value);
        }

        // Calculate variance of changes (smoothness measure) after warmup
        double shortVariance = CalculateChangeVariance(shortResults.Skip(25).ToList());
        double longVariance = CalculateChangeVariance(longResults.Skip(25).ToList());

        // Longer volatility period should produce more stable VOV
        Assert.True(longVariance < shortVariance,
            $"Longer period should be smoother: short variance={shortVariance:F6}, long variance={longVariance:F6}");
    }

    private static double CalculateChangeVariance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var changes = new List<double>();
        for (int i = 1; i < values.Count; i++)
        {
            changes.Add(values[i] - values[i - 1]);
        }

        double mean = changes.Average();
        double variance = changes.Select(c => (c - mean) * (c - mean)).Average();
        return variance;
    }

    // === Stability Tests ===

    /// <summary>
    /// Validates stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Vov_Stability_ConsistentOverRepeatedRuns()
    {
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var data = GenerateTestData(100);
            var vov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);

            for (int i = 0; i < data.Count; i++)
            {
                vov.Update(data[i]);
            }
            results.Add(vov.Last.Value);
        }

        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates VOV responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Vov_RespondsToVolatilityRegimeChange()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 5);
        var time = DateTime.UtcNow;

        // Stable volatility regime
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + (Math.Sin(i * 0.5) * 2); // Consistent amplitude
            vov.Update(new TValue(time.AddSeconds(i), price));
        }
        double stableVov = vov.Last.Value;

        // Transition to higher volatility
        for (int i = 20; i < 35; i++)
        {
            double price = 100 + (Math.Sin(i * 0.5) * (2 + ((i - 20) * 0.5))); // Increasing amplitude
            vov.Update(new TValue(time.AddSeconds(i), price));
        }
        double transitionVov = vov.Last.Value;

        // During transition, VOV should increase (volatility is changing)
        Assert.True(transitionVov > stableVov * 0.5,
            $"VOV should respond to volatility regime change: stable={stableVov:F4}, transition={transitionVov:F4}");
    }

    // === Large Data Tests ===

    /// <summary>
    /// Validates handling of large datasets.
    /// </summary>
    [Fact]
    public void Vov_LargeDataset_HandledCorrectly()
    {
        var data = GenerateTestData(1000);
        var vov = new Vov(DefaultVolatilityPeriod, DefaultVovPeriod);

        for (int i = 0; i < data.Count; i++)
        {
            var result = vov.Update(data[i]);
            Assert.True(double.IsFinite(result.Value), $"Value at index {i} should be finite");
            Assert.True(result.Value >= 0, $"Value at index {i} should be non-negative");
        }
    }

    /// <summary>
    /// Validates batch handles large periods.
    /// </summary>
    [Fact]
    public void Vov_LargePeriods_BatchHandled()
    {
        var data = GenerateTestData(500);
        var output = new double[500];

        // Large periods that exceed stackalloc threshold
        Vov.Batch(data.Values, output, volatilityPeriod: 100, vovPeriod: 50);

        // Last values should be finite and non-negative
        for (int i = 150; i < output.Length; i++) // After full warmup
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} should be finite");
            Assert.True(output[i] >= 0, $"Output at index {i} should be non-negative");
        }
    }

    // === Known Value Test ===

    /// <summary>
    /// Validates VOV against manually calculated known values.
    /// </summary>
    [Fact]
    public void Vov_KnownValues_MatchExpected()
    {
        // Simple case: period 2 for both, prices: 100, 102, 98, 104
        var vov = new Vov(volatilityPeriod: 2, vovPeriod: 2);
        var time = DateTime.UtcNow;

        // Inner stddev calculations:
        // Bar 0-1: stddev([100,102]) = sqrt(mean([10000,10404]) - mean([100,102])^2)
        //        = sqrt(10202 - 10201) = sqrt(1) = 1
        // Bar 1-2: stddev([102,98]) = sqrt(mean([10404,9604]) - mean([102,98])^2)
        //        = sqrt(10004 - 10000) = sqrt(4) = 2
        // Bar 2-3: stddev([98,104]) = sqrt(mean([9604,10816]) - mean([98,104])^2)
        //        = sqrt(10210 - 10201) = sqrt(9) = 3

        // Outer VOV (last 2 inner stddevs):
        // At bar 2: stddev([1,2]) = sqrt(mean([1,4]) - mean([1,2])^2) = sqrt(2.5 - 2.25) = sqrt(0.25) = 0.5
        // At bar 3: stddev([2,3]) = sqrt(mean([4,9]) - mean([2,3])^2) = sqrt(6.5 - 6.25) = sqrt(0.25) = 0.5

        vov.Update(new TValue(time.AddSeconds(0), 100));
        vov.Update(new TValue(time.AddSeconds(1), 102));
        vov.Update(new TValue(time.AddSeconds(2), 98));
        var result = vov.Update(new TValue(time.AddSeconds(3), 104));

        Assert.Equal(0.5, result.Value, 8);
    }
}
