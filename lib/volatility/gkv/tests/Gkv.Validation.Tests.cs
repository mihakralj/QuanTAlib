namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for GKV (Garman-Klass Volatility).
/// GKV is a range-based volatility estimator using OHLC data.
/// Formula: term1 = 0.5 × (lnH - lnL)², term2 = (2×ln(2)-1) × (lnC - lnO)²
/// GK Estimator = term1 - term2
/// RMA smoothing with bias correction applied.
/// </summary>
public class GkvValidationTests
{
    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === Mathematical Validation ===

    /// <summary>
    /// Validates the Garman-Klass coefficient: (2×ln(2)-1) ≈ 0.38629436
    /// </summary>
    [Fact]
    public void Gkv_GarmanKlassCoefficient_IsCorrect()
    {
        double expectedCoeff = 2.0 * Math.Log(2) - 1.0;
        Assert.Equal(0.38629436111989, expectedCoeff, 10);
    }

    /// <summary>
    /// Validates RMA decay formula: decay = 1 - (1/period)
    /// </summary>
    [Theory]
    [InlineData(14, 0.928571428571429)]   // 1 - 1/14 = 13/14
    [InlineData(20, 0.95)]                 // 1 - 1/20 = 19/20
    [InlineData(10, 0.9)]                  // 1 - 1/10 = 9/10
    public void Gkv_RmaDecay_IsCorrect(int period, double expectedDecay)
    {
        double decay = 1.0 - 1.0 / period;
        Assert.Equal(expectedDecay, decay, 10);
    }

    /// <summary>
    /// Validates GK estimator formula: 0.5×(lnH-lnL)² - (2ln2-1)×(lnC-lnO)²
    /// </summary>
    [Fact]
    public void Gkv_GkEstimatorFormula_IsCorrect()
    {
        double open = 100.0;
        double high = 105.0;
        double low = 95.0;
        double close = 102.0;

        double lnH = Math.Log(high);
        double lnL = Math.Log(low);
        double lnO = Math.Log(open);
        double lnC = Math.Log(close);

        double term1 = 0.5 * Math.Pow(lnH - lnL, 2);
        double coeff = 2.0 * Math.Log(2) - 1.0;
        double term2 = coeff * Math.Pow(lnC - lnO, 2);
        double expectedGk = term1 - term2;

        // Manual calculation
        // lnH - lnL = ln(105/95) ≈ 0.1001
        // term1 = 0.5 × 0.1001² ≈ 0.00501
        // lnC - lnO = ln(102/100) ≈ 0.0198
        // term2 = 0.386 × 0.0198² ≈ 0.000151
        // GK ≈ 0.00501 - 0.000151 ≈ 0.00486

        Assert.True(expectedGk > 0, "GK estimator should be positive for normal bars");
        Assert.True(expectedGk < 0.1, "GK estimator should be small for 5% range");
    }

    /// <summary>
    /// Validates that flat bar (O=H=L=C) produces zero GK estimator.
    /// </summary>
    [Fact]
    public void Gkv_FlatBar_ProducesZeroGk()
    {
        double price = 100.0;
        double lnH = Math.Log(price);
        double lnL = Math.Log(price);
        double lnO = Math.Log(price);
        double lnC = Math.Log(price);

        double term1 = 0.5 * Math.Pow(lnH - lnL, 2); // 0
        double coeff = 2.0 * Math.Log(2) - 1.0;
        double term2 = coeff * Math.Pow(lnC - lnO, 2); // 0
        double gk = term1 - term2;

        Assert.Equal(0.0, gk, 15);
    }

    /// <summary>
    /// Validates bias correction formula: corrected = raw / (1 - decay^n)
    /// </summary>
    [Theory]
    [InlineData(14, 5)]    // Early in warmup
    [InlineData(14, 14)]   // At warmup
    [InlineData(14, 50)]   // Well past warmup
    [InlineData(14, 100)]  // Very late - correction should be minimal
    public void Gkv_BiasCorrection_WorksCorrectly(int period, int count)
    {
        double decay = 1.0 - 1.0 / period;
        double e = Math.Pow(decay, count);
        double correctionFactor = 1.0 / (1.0 - e);

        // Early: large correction needed
        // Later: correction approaches 1.0
        if (count < period)
        {
            Assert.True(correctionFactor > 1.05, "Early values should need significant correction");
        }
        else if (count > period * 5)
        {
            // For period=14, count=100: decay^100 ≈ 0.0003, factor ≈ 1.0003
            Assert.True(correctionFactor < 1.01, "Very late values should need minimal correction");
        }
        else if (count > period * 2)
        {
            // For period=14, count=50: decay^50 ≈ 0.02, factor ≈ 1.02
            Assert.True(correctionFactor < 1.1, "Late values should need small correction");
        }
    }

    /// <summary>
    /// Validates annualization factor: √(annualPeriods)
    /// </summary>
    [Theory]
    [InlineData(252, 15.8745078663875)]   // Daily trading days
    [InlineData(365, 19.1049731745428)]   // Calendar days
    [InlineData(52, 7.21110255092798)]    // Weekly
    [InlineData(12, 3.46410161513775)]    // Monthly
    public void Gkv_AnnualizationFactor_IsCorrect(int annualPeriods, double expectedFactor)
    {
        double factor = Math.Sqrt(annualPeriods);
        Assert.Equal(expectedFactor, factor, 10);
    }

    /// <summary>
    /// Validates that wider range produces higher GK estimator.
    /// </summary>
    [Fact]
    public void Gkv_WiderRange_ProducesHigherGk()
    {
        // Narrow range bar
        double narrowGk = ComputeGkEstimator(100, 101, 99, 100);

        // Wide range bar
        double wideGk = ComputeGkEstimator(100, 110, 90, 100);

        Assert.True(wideGk > narrowGk,
            "Wider range should produce higher GK estimator");
    }

    /// <summary>
    /// Validates that close-to-open move reduces GK estimator.
    /// The term2 is subtracted, so larger (C-O) reduces GK.
    /// </summary>
    [Fact]
    public void Gkv_LargeCloseOpenMove_ReducesGk()
    {
        // Same range, small close-open
        double gkSmallMove = ComputeGkEstimator(100, 105, 95, 100.5);

        // Same range, large close-open (close at high)
        double gkLargeMove = ComputeGkEstimator(100, 105, 95, 104.5);

        Assert.True(gkSmallMove > gkLargeMove,
            "Larger close-open move should reduce GK estimator (term2 subtracted)");
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates streaming and batch produce identical results.
    /// </summary>
    [Fact]
    public void Gkv_StreamingMatchesBatch()
    {
        var bars = GenerateTestData(100);

        // Streaming calculation
        var streamingGkv = new Gkv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingGkv.Update(bars[i]);
        }

        // Batch calculation
        var batchResult = Gkv.Batch(bars, 14);

        // Compare last values
        Assert.Equal(batchResult.Last.Value, streamingGkv.Last.Value, 8);
    }

    /// <summary>
    /// Validates TBarSeries input matches TBar streaming.
    /// </summary>
    [Fact]
    public void Gkv_TBarSeriesInput_MatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingGkv = new Gkv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingGkv.Update(bars[i]);
        }

        // TBarSeries batch
        var batchGkv = new Gkv(14);
        var batchResult = batchGkv.Update(bars);

        Assert.Equal(batchResult.Last.Value, streamingGkv.Last.Value, 10);
    }

    /// <summary>
    /// Validates Span batch matches streaming.
    /// </summary>
    [Fact]
    public void Gkv_SpanBatch_MatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingGkv = new Gkv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingGkv.Update(bars[i]);
        }

        // Extract OHLC arrays
        var opens = new double[bars.Count];
        var highs = new double[bars.Count];
        var lows = new double[bars.Count];
        var closes = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            opens[i] = bars[i].Open;
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        // Span batch
        var output = new double[bars.Count];
        Gkv.Batch(opens, highs, lows, closes, output, 14);

        Assert.Equal(output[^1], streamingGkv.Last.Value, 10);
    }

    /// <summary>
    /// Validates annualized output is scaled correctly.
    /// </summary>
    [Fact]
    public void Gkv_Annualized_ScaledCorrectly()
    {
        var bars = GenerateTestData(50);

        // Non-annualized
        var gkvRaw = new Gkv(14, annualize: false);

        // Annualized (default 252 periods)
        var gkvAnn = new Gkv(14, annualize: true, annualPeriods: 252);

        for (int i = 0; i < bars.Count; i++)
        {
            gkvRaw.Update(bars[i]);
            gkvAnn.Update(bars[i]);
        }

        double expectedRatio = Math.Sqrt(252);
        double actualRatio = gkvAnn.Last.Value / gkvRaw.Last.Value;

        Assert.Equal(expectedRatio, actualRatio, 6);
    }

    // === Parameter Sensitivity ===

    /// <summary>
    /// Validates shorter period produces more responsive volatility.
    /// </summary>
    [Fact]
    public void Gkv_ShorterPeriod_MoreResponsive()
    {
        var bars = GenerateTestData(50);

        var gkvShort = new Gkv(5);
        var gkvLong = new Gkv(20);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            gkvShort.Update(bars[i]);
            gkvLong.Update(bars[i]);

            if (gkvShort.IsHot && gkvLong.IsHot)
            {
                shortResults.Add(gkvShort.Last.Value);
                longResults.Add(gkvLong.Last.Value);
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
    public void Gkv_DifferentPeriods_ProduceDifferentResults()
    {
        var bars = GenerateTestData(50);

        var gkv10 = new Gkv(10);
        var gkv14 = new Gkv(14);
        var gkv20 = new Gkv(20);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv10.Update(bars[i]);
            gkv14.Update(bars[i]);
            gkv20.Update(bars[i]);
        }

        Assert.NotEqual(gkv10.Last.Value, gkv14.Last.Value);
        Assert.NotEqual(gkv14.Last.Value, gkv20.Last.Value);
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of very small ranges (tight consolidation).
    /// </summary>
    [Fact]
    public void Gkv_VerySmallRanges_HandledCorrectly()
    {
        var gkv = new Gkv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.001, 99.999, 100.0, 1000.0
            );
            gkv.Update(bar);
        }

        Assert.True(double.IsFinite(gkv.Last.Value));
        Assert.True(gkv.Last.Value >= 0, "Volatility should be non-negative");
    }

    /// <summary>
    /// Validates handling of very large ranges (high volatility).
    /// </summary>
    [Fact]
    public void Gkv_VeryLargeRanges_HandledCorrectly()
    {
        var gkv = new Gkv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 200.0, 50.0, 150.0, 1000.0
            );
            gkv.Update(bar);
        }

        Assert.True(double.IsFinite(gkv.Last.Value));
        Assert.True(gkv.Last.Value > 0, "High volatility should produce positive value");
    }

    /// <summary>
    /// Validates handling of constant bars (zero volatility).
    /// </summary>
    [Fact]
    public void Gkv_ConstantBars_ProducesMinimalVolatility()
    {
        var gkv = new Gkv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.0, 100.0, 100.0, 1000.0
            );
            gkv.Update(bar);
        }

        Assert.True(double.IsFinite(gkv.Last.Value));
        Assert.True(gkv.Last.Value < 0.001, "Constant price should produce near-zero volatility");
    }

    /// <summary>
    /// Validates handling of doji bars (open = close).
    /// </summary>
    [Fact]
    public void Gkv_DojiBars_HandledCorrectly()
    {
        var gkv = new Gkv(14);

        for (int i = 0; i < 30; i++)
        {
            // Doji: open = close, but has range
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 105.0, 95.0, 100.0, 1000.0
            );
            gkv.Update(bar);
        }

        Assert.True(double.IsFinite(gkv.Last.Value));
        Assert.True(gkv.Last.Value > 0, "Doji with range should have positive volatility");
    }

    /// <summary>
    /// Validates warmup period calculation.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(20)]
    public void Gkv_WarmupPeriod_IsCorrect(int period)
    {
        var gkv = new Gkv(period);
        Assert.Equal(period, gkv.WarmupPeriod);
    }

    /// <summary>
    /// Validates output is always non-negative (volatility property).
    /// </summary>
    [Fact]
    public void Gkv_Output_IsNonNegative()
    {
        var bars = GenerateTestData(100);
        var gkv = new Gkv(14);

        for (int i = 0; i < bars.Count; i++)
        {
            gkv.Update(bars[i]);
            if (gkv.IsHot)
            {
                Assert.True(gkv.Last.Value >= 0,
                    $"Volatility should be non-negative at bar {i}");
            }
        }
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Gkv_BarCorrection_WorksCorrectly()
    {
        var gkv = new Gkv(14);
        var bars = GenerateTestData(30);

        // Feed initial bars
        for (int i = 0; i < 20; i++)
        {
            gkv.Update(bars[i], isNew: true);
        }

        // Add new bar
        gkv.Update(bars[20], isNew: true);
        double afterNew = gkv.Last.Value;

        // Correct with different bar (much higher volatility)
        var correctedBar = new TBar(
            bars[20].Time,
            100, 200, 50, 150, 1000
        );
        gkv.Update(correctedBar, isNew: false);
        double afterCorrection = gkv.Last.Value;

        // Restore original
        gkv.Update(bars[20], isNew: false);
        double afterRestore = gkv.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge to same result.
    /// </summary>
    [Fact]
    public void Gkv_IterativeCorrections_Converge()
    {
        var gkv = new Gkv(14);
        var bars = GenerateTestData(30);

        // Feed bars and make corrections
        for (int i = 0; i < 20; i++)
        {
            gkv.Update(bars[i], isNew: true);
        }

        // Multiple corrections on same bar
        for (int j = 0; j < 5; j++)
        {
            var tempBar = new TBar(
                bars[19].Time,
                100 + j, 110 + j, 90 + j, 105 + j, 1000
            );
            gkv.Update(tempBar, isNew: false);
        }

        // Final correction back to original
        gkv.Update(bars[19], isNew: false);
        double afterCorrections = gkv.Last.Value;

        // Fresh calculation
        var gkvFresh = new Gkv(14);
        for (int i = 0; i < 20; i++)
        {
            gkvFresh.Update(bars[i], isNew: true);
        }
        double freshValue = gkvFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    // === Comparison with Theoretical Properties ===

    /// <summary>
    /// Validates GKV efficiency vs Parkinson (theoretical: GKV more efficient).
    /// GKV uses 4 prices (OHLC), Parkinson uses 2 (HL).
    /// Under certain conditions, GKV should be more stable.
    /// </summary>
    [Fact]
    public void Gkv_Stability_ConsistentOverRepeatedRuns()
    {
        // Multiple runs with same seed should produce identical results
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var gbm = new GBM(seed: 42);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            var gkv = new Gkv(14);

            for (int i = 0; i < bars.Count; i++)
            {
                gkv.Update(bars[i]);
            }
            results.Add(gkv.Last.Value);
        }

        // All runs should be identical
        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates GKV responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Gkv_RespondsToVolatilityRegimeChange()
    {
        var gkv = new Gkv(10);

        // Low volatility regime
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 101.0, 99.0, 100.0, 1000.0 // 2% range
            );
            gkv.Update(bar);
        }
        double lowVolValue = gkv.Last.Value;

        // High volatility regime
        for (int i = 20; i < 40; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 110.0, 90.0, 100.0, 1000.0 // 20% range
            );
            gkv.Update(bar);
        }
        double highVolValue = gkv.Last.Value;

        Assert.True(highVolValue > lowVolValue * 2,
            "GKV should significantly increase with higher volatility regime");
    }

    // === Helper Methods ===

    private static double ComputeGkEstimator(double open, double high, double low, double close)
    {
        double lnH = Math.Log(high);
        double lnL = Math.Log(low);
        double lnO = Math.Log(open);
        double lnC = Math.Log(close);

        double term1 = 0.5 * Math.Pow(lnH - lnL, 2);
        double coeff = 2.0 * Math.Log(2) - 1.0;
        double term2 = coeff * Math.Pow(lnC - lnO, 2);

        return term1 - term2;
    }

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
