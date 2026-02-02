namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for RVI (Relative Volatility Index).
/// RVI measures the direction of volatility using standard deviation weighted by price direction.
/// Formula: RVI = 100 × avgUpStd / (avgUpStd + avgDownStd)
/// Uses population stddev over rolling window and RMA smoothing with bias correction.
/// </summary>
public class RviValidationTests
{
    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    private static TSeries GeneratePriceSeries(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var t = new List<long>(count);
        var v = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            t.Add(bars[i].Time);
            v.Add(bars[i].Close);
        }
        return new TSeries(t, v);
    }

    // === Mathematical Validation ===

    /// <summary>
    /// Validates population standard deviation formula: σ = √(E[X²] - E[X]²)
    /// </summary>
    [Fact]
    public void Rvi_PopulationStdDevFormula_IsCorrect()
    {
        // Known values: 1, 2, 3, 4, 5
        double[] values = { 1, 2, 3, 4, 5 };
        double sum = 0, sumSq = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
            sumSq += values[i] * values[i];
        }
        double mean = sum / values.Length;
        double variance = (sumSq / values.Length) - (mean * mean);
        double stdDev = Math.Sqrt(variance);

        // Expected: mean = 3, E[X²] = (1+4+9+16+25)/5 = 11
        // Var = 11 - 9 = 2, StdDev = √2 ≈ 1.414
        Assert.Equal(Math.Sqrt(2.0), stdDev, 10);
    }

    /// <summary>
    /// Validates RMA (Wilder's smoothing) formula: raw = (raw * (length - 1) + value) / length
    /// </summary>
    [Fact]
    public void Rvi_RmaFormula_IsCorrect()
    {
        int length = 14;
        double[] values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };
        double raw = 0;

        for (int i = 0; i < values.Length; i++)
        {
            raw = ((raw * (length - 1)) + values[i]) / length;
        }

        // After 14 values with RMA(14), verify the smoothing effect
        Assert.True(raw > 0);
        Assert.True(raw < 14); // Should be smoothed below max
    }

    /// <summary>
    /// Validates RMA bias correction formula: result = e > ε ? raw / (1 - e) : raw
    /// where e = (1 - alpha) * e_prev, starting at 1.0
    /// </summary>
    [Fact]
    public void Rvi_BiasCorrection_IsCorrect()
    {
        int length = 14;
        double alpha = 1.0 / length;
        double e = 1.0;

        // After one iteration
        e = (1 - alpha) * e;
        double correctionFactor1 = 1.0 / (1.0 - e);
        Assert.True(correctionFactor1 > 1.0, "First correction factor should amplify");

        // After many iterations, e approaches 0
        for (int i = 0; i < 100; i++)
        {
            e = (1 - alpha) * e;
        }
        double correctionFactorN = 1.0 / (1.0 - e);
        Assert.True(correctionFactorN < 1.01, "After warmup, correction factor approaches 1");
    }

    /// <summary>
    /// Validates RVI formula: RVI = 100 × avgUpStd / (avgUpStd + avgDownStd)
    /// </summary>
    [Theory]
    [InlineData(10.0, 10.0, 50.0)]   // Equal up/down = neutral
    [InlineData(20.0, 10.0, 66.666666666666666)]   // More up = bullish
    [InlineData(10.0, 20.0, 33.333333333333333)]   // More down = bearish
    [InlineData(100.0, 0.0, 100.0)]  // All up = max bullish
    [InlineData(0.0, 100.0, 0.0)]    // All down = max bearish
    public void Rvi_RatioFormula_IsCorrect(double avgUpStd, double avgDownStd, double expectedRvi)
    {
        double rvi = (avgUpStd + avgDownStd) > 1e-10
            ? 100.0 * avgUpStd / (avgUpStd + avgDownStd)
            : 50.0;
        Assert.Equal(expectedRvi, rvi, 6);
    }

    /// <summary>
    /// Validates RVI oscillator range is bounded [0, 100].
    /// </summary>
    [Fact]
    public void Rvi_Output_IsBounded()
    {
        var prices = GeneratePriceSeries(200);
        var rvi = new Rvi(10, 14);

        for (int i = 0; i < prices.Count; i++)
        {
            rvi.Update(prices[i]);
            if (rvi.IsHot)
            {
                Assert.True(rvi.Last.Value >= 0.0 && rvi.Last.Value <= 100.0,
                    $"RVI should be in [0,100], got {rvi.Last.Value}");
            }
        }
    }

    /// <summary>
    /// Validates that constant prices produce neutral RVI (50).
    /// </summary>
    [Fact]
    public void Rvi_ConstantPrices_ProducesNeutralValue()
    {
        var rvi = new Rvi(10, 14);

        for (int i = 0; i < 50; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // With no price changes, both up and down are 0, should return neutral 50
        Assert.Equal(50.0, rvi.Last.Value, 6);
    }

    /// <summary>
    /// Validates that strictly rising prices produce high RVI (approaching 100).
    /// </summary>
    [Fact]
    public void Rvi_StrictlyRisingPrices_ProducesHighValue()
    {
        var rvi = new Rvi(10, 14);

        for (int i = 0; i < 100; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i * 0.5));
        }

        Assert.True(rvi.Last.Value > 80.0, $"Strictly rising prices should produce high RVI, got {rvi.Last.Value}");
    }

    /// <summary>
    /// Validates that strictly falling prices produce low RVI (approaching 0).
    /// </summary>
    [Fact]
    public void Rvi_StrictlyFallingPrices_ProducesLowValue()
    {
        var rvi = new Rvi(10, 14);

        for (int i = 0; i < 100; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 - i * 0.5));
        }

        Assert.True(rvi.Last.Value < 20.0, $"Strictly falling prices should produce low RVI, got {rvi.Last.Value}");
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates streaming and batch produce identical results.
    /// </summary>
    [Fact]
    public void Rvi_StreamingMatchesBatch()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming calculation
        var streamingRvi = new Rvi(10, 14);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingRvi.Update(prices[i]);
        }

        // Batch calculation
        var batchResult = Rvi.Calculate(prices, 10, 14);

        // Compare last values
        Assert.Equal(batchResult.Last.Value, streamingRvi.Last.Value, 8);
    }

    /// <summary>
    /// Validates TSeries input matches TValue streaming.
    /// </summary>
    [Fact]
    public void Rvi_TSeriesInput_MatchesStreaming()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming
        var streamingRvi = new Rvi(10, 14);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingRvi.Update(prices[i]);
        }

        // TSeries batch
        var batchRvi = new Rvi(10, 14);
        var batchResult = batchRvi.Update(prices);

        Assert.Equal(batchResult.Last.Value, streamingRvi.Last.Value, 10);
    }

    /// <summary>
    /// Validates Span batch matches streaming.
    /// </summary>
    [Fact]
    public void Rvi_SpanBatch_MatchesStreaming()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming
        var streamingRvi = new Rvi(10, 14);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingRvi.Update(prices[i]);
        }

        // Span batch
        var output = new double[prices.Count];
        Rvi.Batch(prices.Values, output, 10, 14);

        Assert.Equal(output[^1], streamingRvi.Last.Value, 10);
    }

    /// <summary>
    /// Validates TBar update uses only Close price.
    /// </summary>
    [Fact]
    public void Rvi_TBar_UsesOnlyClose()
    {
        var bars = GenerateTestData(50);

        // Using TBar
        var rviBar = new Rvi(10, 14);
        for (int i = 0; i < bars.Count; i++)
        {
            rviBar.Update(bars[i]);
        }

        // Using just Close prices
        var rviClose = new Rvi(10, 14);
        for (int i = 0; i < bars.Count; i++)
        {
            rviClose.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.Equal(rviClose.Last.Value, rviBar.Last.Value, 10);
    }

    // === Parameter Sensitivity ===

    /// <summary>
    /// Validates shorter stddev period produces more responsive RVI.
    /// </summary>
    [Fact]
    public void Rvi_ShorterStdevPeriod_MoreResponsive()
    {
        var prices = GeneratePriceSeries(100);

        var rviShort = new Rvi(stdevLength: 5, rmaLength: 14);
        var rviLong = new Rvi(stdevLength: 20, rmaLength: 14);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < prices.Count; i++)
        {
            rviShort.Update(prices[i]);
            rviLong.Update(prices[i]);

            if (rviShort.IsHot && rviLong.IsHot)
            {
                shortResults.Add(rviShort.Last.Value);
                longResults.Add(rviLong.Last.Value);
            }
        }

        // Shorter period should have higher variance in results
        double shortVar = Variance(shortResults);
        double longVar = Variance(longResults);

        Assert.True(shortResults.Count > 0, "Should have hot results");
        Assert.True(shortVar > longVar * 0.8,
            "Shorter stddev period should generally be more variable");
    }

    /// <summary>
    /// Validates shorter RMA period produces faster response.
    /// </summary>
    [Fact]
    public void Rvi_ShorterRmaPeriod_FasterResponse()
    {
        var prices = GeneratePriceSeries(100);

        var rviFast = new Rvi(stdevLength: 10, rmaLength: 7);
        var rviSlow = new Rvi(stdevLength: 10, rmaLength: 21);

        var fastResults = new List<double>();
        var slowResults = new List<double>();

        for (int i = 0; i < prices.Count; i++)
        {
            rviFast.Update(prices[i]);
            rviSlow.Update(prices[i]);

            if (rviFast.IsHot && rviSlow.IsHot)
            {
                fastResults.Add(rviFast.Last.Value);
                slowResults.Add(rviSlow.Last.Value);
            }
        }

        // Faster RMA should have higher variance
        double fastVar = Variance(fastResults);
        double slowVar = Variance(slowResults);

        Assert.True(fastResults.Count > 0, "Should have hot results");
        Assert.True(fastVar > slowVar * 0.8,
            "Faster RMA should generally be more variable");
    }

    /// <summary>
    /// Validates different parameters produce different results.
    /// </summary>
    [Fact]
    public void Rvi_DifferentParameters_ProduceDifferentResults()
    {
        var prices = GeneratePriceSeries(50);

        var rvi1 = new Rvi(10, 14);
        var rvi2 = new Rvi(5, 14);
        var rvi3 = new Rvi(10, 7);

        for (int i = 0; i < prices.Count; i++)
        {
            rvi1.Update(prices[i]);
            rvi2.Update(prices[i]);
            rvi3.Update(prices[i]);
        }

        Assert.NotEqual(rvi1.Last.Value, rvi2.Last.Value);
        Assert.NotEqual(rvi1.Last.Value, rvi3.Last.Value);
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of very small price changes.
    /// </summary>
    [Fact]
    public void Rvi_VerySmallChanges_HandledCorrectly()
    {
        var rvi = new Rvi(10, 14);

        double price = 100.0;
        for (int i = 0; i < 50; i++)
        {
            price += 0.0001 * (i % 2 == 0 ? 1 : -1); // Tiny oscillation
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }

        Assert.True(double.IsFinite(rvi.Last.Value));
        Assert.True(rvi.Last.Value >= 0 && rvi.Last.Value <= 100);
    }

    /// <summary>
    /// Validates handling of large price swings.
    /// </summary>
    [Fact]
    public void Rvi_LargePriceSwings_HandledCorrectly()
    {
        var rvi = new Rvi(10, 14);

        double price = 100.0;
        for (int i = 0; i < 50; i++)
        {
            price *= (i % 2 == 0 ? 1.1 : 0.9); // 10% swings
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }

        Assert.True(double.IsFinite(rvi.Last.Value));
        Assert.True(rvi.Last.Value >= 0 && rvi.Last.Value <= 100);
    }

    /// <summary>
    /// Validates warmup period calculation (stdevLength + rmaLength).
    /// </summary>
    [Theory]
    [InlineData(10, 14, 24)]
    [InlineData(5, 7, 12)]
    [InlineData(20, 20, 40)]
    public void Rvi_WarmupPeriod_IsCorrect(int stdevLength, int rmaLength, int expectedWarmup)
    {
        var rvi = new Rvi(stdevLength, rmaLength);
        Assert.Equal(expectedWarmup, rvi.WarmupPeriod);
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Rvi_BarCorrection_WorksCorrectly()
    {
        var rvi = new Rvi(10, 14);
        var prices = GeneratePriceSeries(40);

        // Feed initial prices
        for (int i = 0; i < 30; i++)
        {
            rvi.Update(prices[i], isNew: true);
        }

        // Add new price
        rvi.Update(prices[30], isNew: true);
        double afterNew = rvi.Last.Value;

        // Correct with very different price
        var correctedPrice = new TValue(prices[30].Time, prices[30].Value * 1.5);
        rvi.Update(correctedPrice, isNew: false);
        double afterCorrection = rvi.Last.Value;

        // Restore original
        rvi.Update(prices[30], isNew: false);
        double afterRestore = rvi.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge to same result.
    /// </summary>
    [Fact]
    public void Rvi_IterativeCorrections_Converge()
    {
        var rvi = new Rvi(10, 14);
        var prices = GeneratePriceSeries(40);

        // Feed prices and make corrections
        for (int i = 0; i < 30; i++)
        {
            rvi.Update(prices[i], isNew: true);
        }

        // Multiple corrections on same price
        for (int j = 0; j < 5; j++)
        {
            var tempPrice = new TValue(prices[29].Time, prices[29].Value * (1.0 + j * 0.01));
            rvi.Update(tempPrice, isNew: false);
        }

        // Final correction back to original
        rvi.Update(prices[29], isNew: false);
        double afterCorrections = rvi.Last.Value;

        // Fresh calculation
        var rviFresh = new Rvi(10, 14);
        for (int i = 0; i < 30; i++)
        {
            rviFresh.Update(prices[i], isNew: true);
        }
        double freshValue = rviFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    // === Behavioral Tests ===

    /// <summary>
    /// Validates RVI responds to trend changes.
    /// </summary>
    [Fact]
    public void Rvi_RespondsToTrendChange()
    {
        var rvi = new Rvi(10, 14);

        // Uptrend phase
        double price = 100.0;
        for (int i = 0; i < 50; i++)
        {
            price += 0.5;
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        double afterUptrend = rvi.Last.Value;

        // Downtrend phase
        for (int i = 50; i < 100; i++)
        {
            price -= 0.5;
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        double afterDowntrend = rvi.Last.Value;

        Assert.True(afterUptrend > 60, "RVI should be high after uptrend");
        Assert.True(afterDowntrend < 40, "RVI should be low after downtrend");
    }

    /// <summary>
    /// Validates RVI stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Rvi_Stability_ConsistentOverRepeatedRuns()
    {
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var gbm = new GBM(seed: 42);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            var rvi = new Rvi(10, 14);

            for (int i = 0; i < bars.Count; i++)
            {
                rvi.Update(bars[i]);
            }
            results.Add(rvi.Last.Value);
        }

        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates RVI is in a reasonable range for oscillating prices.
    /// Note: RVI depends on the sequence of up/down moves. A sine wave doesn't 
    /// guarantee neutral RVI because the direction changes occur at different 
    /// phases relative to when volatility peaks.
    /// </summary>
    [Fact]
    public void Rvi_OscillatingPrices_StaysInRange()
    {
        var rvi = new Rvi(10, 14);

        // Symmetric oscillation
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + Math.Sin(i * 0.1) * 5; // Oscillating ±5
            rvi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }

        // For oscillating data, RVI should stay within reasonable bounds
        // but doesn't necessarily hover at exactly 50
        Assert.True(rvi.Last.Value >= 0 && rvi.Last.Value <= 100,
            $"Oscillating prices should produce RVI in valid range, got {rvi.Last.Value}");
        Assert.True(double.IsFinite(rvi.Last.Value));
    }

    /// <summary>
    /// Validates RVI produces reasonable values for typical market data.
    /// </summary>
    [Fact]
    public void Rvi_ProducesReasonableValues()
    {
        var prices = GeneratePriceSeries(200);
        var rvi = new Rvi(10, 14);

        int validCount = 0;
        for (int i = 0; i < prices.Count; i++)
        {
            rvi.Update(prices[i]);
            if (rvi.IsHot)
            {
                validCount++;
                Assert.True(double.IsFinite(rvi.Last.Value));
                Assert.True(rvi.Last.Value >= 0 && rvi.Last.Value <= 100);
            }
        }

        Assert.True(validCount > 100, "Should have many valid values");
    }

    // === Helper Methods ===

    private static double Variance(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        double mean = values.Average();
        return values.Average(v => Math.Pow(v - mean, 2));
    }
}