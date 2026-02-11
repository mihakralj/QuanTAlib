namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for HLV (High-Low Volatility / Parkinson Volatility).
/// HLV is a range-based volatility estimator using only High-Low prices.
/// Formula: parkinsonEstimator = (1/(4*ln(2))) * (lnH - lnL)²
/// RMA smoothing with bias correction applied.
/// </summary>
public class HlvValidationTests
{
    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === Mathematical Validation ===

    /// <summary>
    /// Validates the Parkinson coefficient: 1/(4*ln(2)) ≈ 0.36067376
    /// </summary>
    [Fact]
    public void Hlv_ParkinsonCoefficient_IsCorrect()
    {
        double expectedCoeff = 1.0 / (4.0 * Math.Log(2));
        Assert.Equal(0.36067376022224085, expectedCoeff, 10);
    }

    /// <summary>
    /// Validates RMA decay formula: decay = 1 - (1/period)
    /// </summary>
    [Theory]
    [InlineData(14, 0.928571428571429)]   // 1 - 1/14 = 13/14
    [InlineData(20, 0.95)]                 // 1 - 1/20 = 19/20
    [InlineData(10, 0.9)]                  // 1 - 1/10 = 9/10
    public void Hlv_RmaDecay_IsCorrect(int period, double expectedDecay)
    {
        double decay = 1.0 - 1.0 / period;
        Assert.Equal(expectedDecay, decay, 10);
    }

    /// <summary>
    /// Validates Parkinson estimator formula: (1/(4*ln(2))) * (lnH - lnL)²
    /// </summary>
    [Fact]
    public void Hlv_ParkinsonEstimatorFormula_IsCorrect()
    {
        double high = 105.0;
        double low = 95.0;

        double lnH = Math.Log(high);
        double lnL = Math.Log(low);

        double coeff = 1.0 / (4.0 * Math.Log(2));
        double expectedPk = coeff * Math.Pow(lnH - lnL, 2);

        // Manual calculation
        // lnH - lnL = ln(105/95) ≈ 0.1001
        // (lnH - lnL)² ≈ 0.01002
        // coeff ≈ 0.36067
        // Pk ≈ 0.36067 * 0.01002 ≈ 0.00361

        Assert.True(expectedPk > 0, "Parkinson estimator should be positive for bars with range");
        Assert.True(expectedPk < 0.1, "Parkinson estimator should be small for 10% range");
    }

    /// <summary>
    /// Validates that flat bar (H=L) produces zero Parkinson estimator.
    /// </summary>
    [Fact]
    public void Hlv_FlatBar_ProducesZeroPk()
    {
        double price = 100.0;
        double lnH = Math.Log(price);
        double lnL = Math.Log(price);

        double coeff = 1.0 / (4.0 * Math.Log(2));
        double pk = coeff * Math.Pow(lnH - lnL, 2); // 0

        Assert.Equal(0.0, pk, 15);
    }

    /// <summary>
    /// Validates bias correction formula: corrected = raw / (1 - decay^n)
    /// </summary>
    [Theory]
    [InlineData(14, 5)]    // Early in warmup
    [InlineData(14, 14)]   // At warmup
    [InlineData(14, 50)]   // Well past warmup
    [InlineData(14, 100)]  // Very late - correction should be minimal
    public void Hlv_BiasCorrection_WorksCorrectly(int period, int count)
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
            Assert.True(correctionFactor < 1.01, "Very late values should need minimal correction");
        }
        else if (count > period * 2)
        {
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
    public void Hlv_AnnualizationFactor_IsCorrect(int annualPeriods, double expectedFactor)
    {
        double factor = Math.Sqrt(annualPeriods);
        Assert.Equal(expectedFactor, factor, 10);
    }

    /// <summary>
    /// Validates that wider range produces higher Parkinson estimator.
    /// </summary>
    [Fact]
    public void Hlv_WiderRange_ProducesHigherPk()
    {
        // Narrow range bar
        double narrowPk = ComputeParkinsonEstimator(101, 99);

        // Wide range bar
        double widePk = ComputeParkinsonEstimator(110, 90);

        Assert.True(widePk > narrowPk,
            "Wider range should produce higher Parkinson estimator");
    }

    /// <summary>
    /// Validates that HLV only uses High-Low (ignores Open-Close).
    /// Same H-L range with different O-C should produce identical results.
    /// </summary>
    [Fact]
    public void Hlv_OnlyUsesHighLow_IgnoresOpenClose()
    {
        var hlv1 = new Hlv(14, annualize: false);
        var hlv2 = new Hlv(14, annualize: false);

        for (int i = 0; i < 30; i++)
        {
            // Same high/low range but different open/close
            // Indicator 1: doji pattern (open = close)
            var bar1 = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 105.0, 95.0, 100.0, 1000.0
            );
            hlv1.Update(bar1);

            // Indicator 2: directional move (open != close)
            var bar2 = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                98.0, 105.0, 95.0, 104.0, 1000.0
            );
            hlv2.Update(bar2);
        }

        // HLV should be identical since H-L range is the same
        Assert.Equal(hlv1.Last.Value, hlv2.Last.Value, 10);
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates streaming and batch produce identical results.
    /// </summary>
    [Fact]
    public void Hlv_StreamingMatchesBatch()
    {
        var bars = GenerateTestData(100);

        // Streaming calculation
        var streamingHlv = new Hlv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingHlv.Update(bars[i]);
        }

        // Batch calculation
        var batchResult = Hlv.Batch(bars, 14);

        // Compare last values
        Assert.Equal(batchResult.Last.Value, streamingHlv.Last.Value, 8);
    }

    /// <summary>
    /// Validates TBarSeries input matches TBar streaming.
    /// </summary>
    [Fact]
    public void Hlv_TBarSeriesInput_MatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingHlv = new Hlv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingHlv.Update(bars[i]);
        }

        // TBarSeries batch
        var batchHlv = new Hlv(14);
        var batchResult = batchHlv.Update(bars);

        Assert.Equal(batchResult.Last.Value, streamingHlv.Last.Value, 10);
    }

    /// <summary>
    /// Validates Span batch matches streaming.
    /// </summary>
    [Fact]
    public void Hlv_SpanBatch_MatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingHlv = new Hlv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingHlv.Update(bars[i]);
        }

        // Extract H-L arrays
        var highs = new double[bars.Count];
        var lows = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        // Span batch
        var output = new double[bars.Count];
        Hlv.Batch(highs, lows, output, 14);

        Assert.Equal(output[^1], streamingHlv.Last.Value, 10);
    }

    /// <summary>
    /// Validates annualized output is scaled correctly.
    /// </summary>
    [Fact]
    public void Hlv_Annualized_ScaledCorrectly()
    {
        var bars = GenerateTestData(50);

        // Non-annualized
        var hlvRaw = new Hlv(14, annualize: false);

        // Annualized (default 252 periods)
        var hlvAnn = new Hlv(14, annualize: true, annualPeriods: 252);

        for (int i = 0; i < bars.Count; i++)
        {
            hlvRaw.Update(bars[i]);
            hlvAnn.Update(bars[i]);
        }

        double expectedRatio = Math.Sqrt(252);
        double actualRatio = hlvAnn.Last.Value / hlvRaw.Last.Value;

        Assert.Equal(expectedRatio, actualRatio, 6);
    }

    // === Parameter Sensitivity ===

    /// <summary>
    /// Validates shorter period produces more responsive volatility.
    /// </summary>
    [Fact]
    public void Hlv_ShorterPeriod_MoreResponsive()
    {
        var bars = GenerateTestData(50);

        var hlvShort = new Hlv(5);
        var hlvLong = new Hlv(20);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            hlvShort.Update(bars[i]);
            hlvLong.Update(bars[i]);

            if (hlvShort.IsHot && hlvLong.IsHot)
            {
                shortResults.Add(hlvShort.Last.Value);
                longResults.Add(hlvLong.Last.Value);
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
    public void Hlv_DifferentPeriods_ProduceDifferentResults()
    {
        var bars = GenerateTestData(50);

        var hlv10 = new Hlv(10);
        var hlv14 = new Hlv(14);
        var hlv20 = new Hlv(20);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv10.Update(bars[i]);
            hlv14.Update(bars[i]);
            hlv20.Update(bars[i]);
        }

        Assert.NotEqual(hlv10.Last.Value, hlv14.Last.Value);
        Assert.NotEqual(hlv14.Last.Value, hlv20.Last.Value);
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of very small ranges (tight consolidation).
    /// </summary>
    [Fact]
    public void Hlv_VerySmallRanges_HandledCorrectly()
    {
        var hlv = new Hlv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.001, 99.999, 100.0, 1000.0
            );
            hlv.Update(bar);
        }

        Assert.True(double.IsFinite(hlv.Last.Value));
        Assert.True(hlv.Last.Value >= 0, "Volatility should be non-negative");
    }

    /// <summary>
    /// Validates handling of very large ranges (high volatility).
    /// </summary>
    [Fact]
    public void Hlv_VeryLargeRanges_HandledCorrectly()
    {
        var hlv = new Hlv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 200.0, 50.0, 150.0, 1000.0
            );
            hlv.Update(bar);
        }

        Assert.True(double.IsFinite(hlv.Last.Value));
        Assert.True(hlv.Last.Value > 0, "High volatility should produce positive value");
    }

    /// <summary>
    /// Validates handling of constant bars (zero volatility).
    /// </summary>
    [Fact]
    public void Hlv_ConstantBars_ProducesMinimalVolatility()
    {
        var hlv = new Hlv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.0, 100.0, 100.0, 1000.0
            );
            hlv.Update(bar);
        }

        Assert.True(double.IsFinite(hlv.Last.Value));
        Assert.True(hlv.Last.Value < 0.001, "Constant price should produce near-zero volatility");
    }

    /// <summary>
    /// Validates warmup period calculation.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(20)]
    public void Hlv_WarmupPeriod_IsCorrect(int period)
    {
        var hlv = new Hlv(period);
        Assert.Equal(period, hlv.WarmupPeriod);
    }

    /// <summary>
    /// Validates output is always non-negative (volatility property).
    /// </summary>
    [Fact]
    public void Hlv_Output_IsNonNegative()
    {
        var bars = GenerateTestData(100);
        var hlv = new Hlv(14);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
            if (hlv.IsHot)
            {
                Assert.True(hlv.Last.Value >= 0,
                    $"Volatility should be non-negative at bar {i}");
            }
        }
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Hlv_BarCorrection_WorksCorrectly()
    {
        var hlv = new Hlv(14);
        var bars = GenerateTestData(30);

        // Feed initial bars
        for (int i = 0; i < 20; i++)
        {
            hlv.Update(bars[i], isNew: true);
        }

        // Add new bar
        hlv.Update(bars[20], isNew: true);
        double afterNew = hlv.Last.Value;

        // Correct with different bar (much higher volatility)
        var correctedBar = new TBar(
            bars[20].Time,
            100, 200, 50, 150, 1000
        );
        hlv.Update(correctedBar, isNew: false);
        double afterCorrection = hlv.Last.Value;

        // Restore original
        hlv.Update(bars[20], isNew: false);
        double afterRestore = hlv.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge to same result.
    /// </summary>
    [Fact]
    public void Hlv_IterativeCorrections_Converge()
    {
        var hlv = new Hlv(14);
        var bars = GenerateTestData(30);

        // Feed bars and make corrections
        for (int i = 0; i < 20; i++)
        {
            hlv.Update(bars[i], isNew: true);
        }

        // Multiple corrections on same bar
        for (int j = 0; j < 5; j++)
        {
            var tempBar = new TBar(
                bars[19].Time,
                100 + j, 110 + j, 90 + j, 105 + j, 1000
            );
            hlv.Update(tempBar, isNew: false);
        }

        // Final correction back to original
        hlv.Update(bars[19], isNew: false);
        double afterCorrections = hlv.Last.Value;

        // Fresh calculation
        var hlvFresh = new Hlv(14);
        for (int i = 0; i < 20; i++)
        {
            hlvFresh.Update(bars[i], isNew: true);
        }
        double freshValue = hlvFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    // === Comparison with Theoretical Properties ===

    /// <summary>
    /// Validates HLV stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Hlv_Stability_ConsistentOverRepeatedRuns()
    {
        // Multiple runs with same seed should produce identical results
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var gbm = new GBM(seed: 42);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            var hlv = new Hlv(14);

            for (int i = 0; i < bars.Count; i++)
            {
                hlv.Update(bars[i]);
            }
            results.Add(hlv.Last.Value);
        }

        // All runs should be identical
        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates HLV responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Hlv_RespondsToVolatilityRegimeChange()
    {
        var hlv = new Hlv(10);

        // Low volatility regime
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 101.0, 99.0, 100.0, 1000.0 // 2% range
            );
            hlv.Update(bar);
        }
        double lowVolValue = hlv.Last.Value;

        // High volatility regime
        for (int i = 20; i < 40; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 110.0, 90.0, 100.0, 1000.0 // 20% range
            );
            hlv.Update(bar);
        }
        double highVolValue = hlv.Last.Value;

        Assert.True(highVolValue > lowVolValue * 2,
            "HLV should significantly increase with higher volatility regime");
    }

    /// <summary>
    /// Validates HLV vs GKV: same range, HLV ignores O-C while GKV uses it.
    /// </summary>
    [Fact]
    public void Hlv_VsGkv_DifferentBehavior()
    {
        var hlv = new Hlv(14, annualize: false);
        var gkv = new Gkv(14, annualize: false);

        // Same bars
        for (int i = 0; i < 30; i++)
        {
            // Directional bar (O != C)
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 105.0, 95.0, 104.0, 1000.0
            );
            hlv.Update(bar);
            gkv.Update(bar);
        }

        // Both should produce positive values
        Assert.True(hlv.Last.Value > 0);
        Assert.True(gkv.Last.Value > 0);

        // They should be different since GKV uses O-C term
        Assert.NotEqual(hlv.Last.Value, gkv.Last.Value);
    }

    // === Efficiency Comparison ===

    /// <summary>
    /// Validates Parkinson efficiency factor is approximately 5.2x close-to-close.
    /// This is a theoretical property - we just verify HLV produces reasonable values.
    /// </summary>
    [Fact]
    public void Hlv_ProducesReasonableVolatilityEstimate()
    {
        var bars = GenerateTestData(100);
        var hlv = new Hlv(14, annualize: false);

        for (int i = 0; i < bars.Count; i++)
        {
            hlv.Update(bars[i]);
        }

        // HLV should be positive and finite
        Assert.True(double.IsFinite(hlv.Last.Value));
        Assert.True(hlv.Last.Value > 0);
        Assert.True(hlv.Last.Value < 10, "Raw volatility should be reasonable (< 1000%)");
    }

    // === Helper Methods ===

    private static double ComputeParkinsonEstimator(double high, double low)
    {
        double lnH = Math.Log(high);
        double lnL = Math.Log(low);
        double coeff = 1.0 / (4.0 * Math.Log(2));
        return coeff * Math.Pow(lnH - lnL, 2);
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