namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for HV (Historical Volatility / Close-to-Close Volatility).
/// HV is the standard volatility estimator using log returns of closing prices.
/// Formula: σ = √(Var(log returns)) × √(annualPeriods)
/// Uses population variance over rolling window.
/// </summary>
public class HvValidationTests
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
    /// Validates log return formula: r_t = ln(price_t / price_{t-1})
    /// </summary>
    [Theory]
    [InlineData(100.0, 101.0, 0.00995033)]   // ~1% return
    [InlineData(100.0, 110.0, 0.09531018)]   // ~10% return
    [InlineData(100.0, 90.0, -0.10536052)]   // ~-10% return
    [InlineData(100.0, 100.0, 0.0)]          // no change
    public void Hv_LogReturnFormula_IsCorrect(double prevPrice, double curPrice, double expectedReturn)
    {
        double logReturn = Math.Log(curPrice / prevPrice);
        Assert.Equal(expectedReturn, logReturn, 6);
    }

    /// <summary>
    /// Validates population variance formula: Var = E[X²] - E[X]²
    /// </summary>
    [Fact]
    public void Hv_PopulationVarianceFormula_IsCorrect()
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

        // Expected: mean = 3, E[X²] = (1+4+9+16+25)/5 = 11
        // Var = 11 - 9 = 2
        Assert.Equal(2.0, variance, 10);
    }

    /// <summary>
    /// Validates standard deviation is square root of variance.
    /// </summary>
    [Fact]
    public void Hv_StandardDeviationFormula_IsCorrect()
    {
        double variance = 4.0;
        double stdDev = Math.Sqrt(variance);
        Assert.Equal(2.0, stdDev, 10);
    }

    /// <summary>
    /// Validates annualization factor: √(annualPeriods)
    /// </summary>
    [Theory]
    [InlineData(252, 15.8745078663875)]   // Daily trading days
    [InlineData(365, 19.1049731745428)]   // Calendar days
    [InlineData(52, 7.21110255092798)]    // Weekly
    [InlineData(12, 3.46410161513775)]    // Monthly
    public void Hv_AnnualizationFactor_IsCorrect(int annualPeriods, double expectedFactor)
    {
        double factor = Math.Sqrt(annualPeriods);
        Assert.Equal(expectedFactor, factor, 10);
    }

    /// <summary>
    /// Validates known volatility calculation.
    /// </summary>
    [Fact]
    public void Hv_KnownCalculation_IsCorrect()
    {
        // Prices: 100, 102, 101, 103, 102 (5 prices = 4 returns)
        double[] prices = { 100.0, 102.0, 101.0, 103.0, 102.0 };
        double[] returns = new double[4];

        for (int i = 1; i < prices.Length; i++)
        {
            returns[i - 1] = Math.Log(prices[i] / prices[i - 1]);
        }

        // Calculate population std dev
        double sum = 0, sumSq = 0;
        for (int i = 0; i < returns.Length; i++)
        {
            sum += returns[i];
            sumSq += returns[i] * returns[i];
        }
        double mean = sum / returns.Length;
        double variance = (sumSq / returns.Length) - (mean * mean);
        double expected = Math.Sqrt(variance);

        // Verify with indicator
        var hv = new Hv(period: 4, annualize: false);
        for (int i = 0; i < prices.Length; i++)
        {
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), prices[i]));
        }

        Assert.Equal(expected, hv.Last.Value, 10);
    }

    /// <summary>
    /// Validates that constant prices produce zero volatility.
    /// </summary>
    [Fact]
    public void Hv_ConstantPrices_ProducesZeroVolatility()
    {
        var hv = new Hv(period: 10, annualize: false);

        for (int i = 0; i < 20; i++)
        {
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        // All returns are 0, so variance and std dev are 0
        Assert.Equal(0.0, hv.Last.Value, 10);
    }

    /// <summary>
    /// Validates rolling window properly removes old values.
    /// </summary>
    [Fact]
    public void Hv_RollingWindow_RemovesOldValues()
    {
        var hv = new Hv(period: 5, annualize: false);

        // First phase: volatile returns
        double[] volatilePrices = { 100, 110, 90, 120, 80, 100 }; // 6 prices = 5 returns
        for (int i = 0; i < volatilePrices.Length; i++)
        {
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), volatilePrices[i]));
        }
        double highVolValue = hv.Last.Value;

        // Second phase: constant prices (5 more)
        for (int i = 6; i < 11; i++)
        {
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        double afterConstantValue = hv.Last.Value;

        // Rolling window should now only have zero returns
        Assert.True(afterConstantValue < highVolValue, "Volatility should drop after constant prices");
        Assert.Equal(0.0, afterConstantValue, 10);
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates streaming and batch produce identical results.
    /// </summary>
    [Fact]
    public void Hv_StreamingMatchesBatch()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming calculation
        var streamingHv = new Hv(14);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingHv.Update(prices[i]);
        }

        // Batch calculation
        var batchResult = Hv.Calculate(prices, 14);

        // Compare last values
        Assert.Equal(batchResult.Last.Value, streamingHv.Last.Value, 8);
    }

    /// <summary>
    /// Validates TSeries input matches TValue streaming.
    /// </summary>
    [Fact]
    public void Hv_TSeriesInput_MatchesStreaming()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming
        var streamingHv = new Hv(14);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingHv.Update(prices[i]);
        }

        // TSeries batch
        var batchHv = new Hv(14);
        var batchResult = batchHv.Update(prices);

        Assert.Equal(batchResult.Last.Value, streamingHv.Last.Value, 10);
    }

    /// <summary>
    /// Validates Span batch matches streaming.
    /// </summary>
    [Fact]
    public void Hv_SpanBatch_MatchesStreaming()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming
        var streamingHv = new Hv(14);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingHv.Update(prices[i]);
        }

        // Span batch
        var output = new double[prices.Count];
        Hv.Batch(prices.Values, output, 14);

        Assert.Equal(output[^1], streamingHv.Last.Value, 10);
    }

    /// <summary>
    /// Validates annualized output is scaled correctly.
    /// </summary>
    [Fact]
    public void Hv_Annualized_ScaledCorrectly()
    {
        var prices = GeneratePriceSeries(50);

        // Non-annualized
        var hvRaw = new Hv(14, annualize: false);

        // Annualized (default 252 periods)
        var hvAnn = new Hv(14, annualize: true, annualPeriods: 252);

        for (int i = 0; i < prices.Count; i++)
        {
            hvRaw.Update(prices[i]);
            hvAnn.Update(prices[i]);
        }

        double expectedRatio = Math.Sqrt(252);
        double actualRatio = hvAnn.Last.Value / hvRaw.Last.Value;

        Assert.Equal(expectedRatio, actualRatio, 6);
    }

    /// <summary>
    /// Validates TBar update uses only Close price.
    /// </summary>
    [Fact]
    public void Hv_TBar_UsesOnlyClose()
    {
        var bars = GenerateTestData(50);

        // Using TBar
        var hvBar = new Hv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            hvBar.Update(bars[i]);
        }

        // Using just Close prices
        var hvClose = new Hv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            hvClose.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.Equal(hvClose.Last.Value, hvBar.Last.Value, 10);
    }

    // === Parameter Sensitivity ===

    /// <summary>
    /// Validates shorter period produces more responsive volatility.
    /// </summary>
    [Fact]
    public void Hv_ShorterPeriod_MoreResponsive()
    {
        var prices = GeneratePriceSeries(50);

        var hvShort = new Hv(5);
        var hvLong = new Hv(20);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < prices.Count; i++)
        {
            hvShort.Update(prices[i]);
            hvLong.Update(prices[i]);

            if (hvShort.IsHot && hvLong.IsHot)
            {
                shortResults.Add(hvShort.Last.Value);
                longResults.Add(hvLong.Last.Value);
            }
        }

        // Shorter period should have higher variance in results
        double shortVar = Variance(shortResults);
        double longVar = Variance(longResults);

        Assert.True(shortResults.Count > 0, "Should have hot results");
        Assert.True(shortVar > longVar * 0.5,
            "Shorter period should generally be more variable");
    }

    /// <summary>
    /// Validates different periods produce different results.
    /// </summary>
    [Fact]
    public void Hv_DifferentPeriods_ProduceDifferentResults()
    {
        var prices = GeneratePriceSeries(50);

        var hv10 = new Hv(10);
        var hv14 = new Hv(14);
        var hv20 = new Hv(20);

        for (int i = 0; i < prices.Count; i++)
        {
            hv10.Update(prices[i]);
            hv14.Update(prices[i]);
            hv20.Update(prices[i]);
        }

        Assert.NotEqual(hv10.Last.Value, hv14.Last.Value);
        Assert.NotEqual(hv14.Last.Value, hv20.Last.Value);
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of very small price changes.
    /// </summary>
    [Fact]
    public void Hv_VerySmallChanges_HandledCorrectly()
    {
        var hv = new Hv(14, annualize: false);

        double price = 100.0;
        for (int i = 0; i < 30; i++)
        {
            price += 0.001 * (i % 2 == 0 ? 1 : -1); // Tiny oscillation
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }

        Assert.True(double.IsFinite(hv.Last.Value));
        Assert.True(hv.Last.Value >= 0, "Volatility should be non-negative");
        Assert.True(hv.Last.Value < 0.01, "Small changes should produce small volatility");
    }

    /// <summary>
    /// Validates handling of large price swings.
    /// </summary>
    [Fact]
    public void Hv_LargePriceSwings_HandledCorrectly()
    {
        var hv = new Hv(14, annualize: false);

        double price = 100.0;
        for (int i = 0; i < 30; i++)
        {
            price *= (i % 2 == 0 ? 1.1 : 0.9); // 10% swings
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }

        Assert.True(double.IsFinite(hv.Last.Value));
        Assert.True(hv.Last.Value > 0, "Large swings should produce positive volatility");
    }

    /// <summary>
    /// Validates warmup period calculation (period + 1).
    /// </summary>
    [Theory]
    [InlineData(10, 11)]
    [InlineData(14, 15)]
    [InlineData(20, 21)]
    public void Hv_WarmupPeriod_IsPeriodPlusOne(int period, int expectedWarmup)
    {
        var hv = new Hv(period);
        Assert.Equal(expectedWarmup, hv.WarmupPeriod);
    }

    /// <summary>
    /// Validates output is always non-negative (volatility property).
    /// </summary>
    [Fact]
    public void Hv_Output_IsNonNegative()
    {
        var prices = GeneratePriceSeries(100);
        var hv = new Hv(14);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
            if (hv.IsHot)
            {
                Assert.True(hv.Last.Value >= 0,
                    $"Volatility should be non-negative at bar {i}");
            }
        }
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Hv_BarCorrection_WorksCorrectly()
    {
        var hv = new Hv(14);
        var prices = GeneratePriceSeries(30);

        // Feed initial prices
        for (int i = 0; i < 20; i++)
        {
            hv.Update(prices[i], isNew: true);
        }

        // Add new price
        hv.Update(prices[20], isNew: true);
        double afterNew = hv.Last.Value;

        // Correct with very different price
        var correctedPrice = new TValue(prices[20].Time, prices[20].Value * 2.0);
        hv.Update(correctedPrice, isNew: false);
        double afterCorrection = hv.Last.Value;

        // Restore original
        hv.Update(prices[20], isNew: false);
        double afterRestore = hv.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge to same result.
    /// </summary>
    [Fact]
    public void Hv_IterativeCorrections_Converge()
    {
        var hv = new Hv(14);
        var prices = GeneratePriceSeries(30);

        // Feed prices and make corrections
        for (int i = 0; i < 20; i++)
        {
            hv.Update(prices[i], isNew: true);
        }

        // Multiple corrections on same price
        for (int j = 0; j < 5; j++)
        {
            var tempPrice = new TValue(prices[19].Time, prices[19].Value * (1.0 + j * 0.01));
            hv.Update(tempPrice, isNew: false);
        }

        // Final correction back to original
        hv.Update(prices[19], isNew: false);
        double afterCorrections = hv.Last.Value;

        // Fresh calculation
        var hvFresh = new Hv(14);
        for (int i = 0; i < 20; i++)
        {
            hvFresh.Update(prices[i], isNew: true);
        }
        double freshValue = hvFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    // === Comparison with Other Estimators ===

    /// <summary>
    /// Validates HV vs HLV: close-to-close vs high-low estimator.
    /// </summary>
    [Fact]
    public void Hv_VsHlv_DifferentBehavior()
    {
        var bars = GenerateTestData(50);

        var hv = new Hv(14, annualize: false);
        var hlv = new Hlv(14, annualize: false);

        for (int i = 0; i < bars.Count; i++)
        {
            hv.Update(bars[i]);
            hlv.Update(bars[i]);
        }

        // Both should produce positive values
        Assert.True(hv.Last.Value > 0);
        Assert.True(hlv.Last.Value > 0);

        // They should generally be different (HLV uses high-low range)
        Assert.NotEqual(hv.Last.Value, hlv.Last.Value);
    }

    /// <summary>
    /// Validates HV stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Hv_Stability_ConsistentOverRepeatedRuns()
    {
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var gbm = new GBM(seed: 42);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            var hv = new Hv(14);

            for (int i = 0; i < bars.Count; i++)
            {
                hv.Update(bars[i]);
            }
            results.Add(hv.Last.Value);
        }

        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates HV responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Hv_RespondsToVolatilityRegimeChange()
    {
        var hv = new Hv(10, annualize: false);

        // Low volatility regime: small price changes
        double price = 100.0;
        for (int i = 0; i < 20; i++)
        {
            price *= (i % 2 == 0 ? 1.001 : 0.999); // 0.1% changes
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        double lowVolValue = hv.Last.Value;

        // High volatility regime: large price changes
        for (int i = 20; i < 40; i++)
        {
            price *= (i % 2 == 0 ? 1.05 : 0.95); // 5% changes
            hv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        double highVolValue = hv.Last.Value;

        Assert.True(highVolValue > lowVolValue * 5,
            "HV should significantly increase with higher volatility regime");
    }

    /// <summary>
    /// Validates HV produces reasonable volatility estimate.
    /// </summary>
    [Fact]
    public void Hv_ProducesReasonableVolatilityEstimate()
    {
        var prices = GeneratePriceSeries(100);
        var hv = new Hv(14, annualize: false);

        for (int i = 0; i < prices.Count; i++)
        {
            hv.Update(prices[i]);
        }

        Assert.True(double.IsFinite(hv.Last.Value));
        Assert.True(hv.Last.Value > 0);
        Assert.True(hv.Last.Value < 1, "Raw daily volatility should be < 100%");
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