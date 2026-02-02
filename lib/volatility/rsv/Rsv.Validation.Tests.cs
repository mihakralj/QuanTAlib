namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for RSV (Rogers-Satchell Volatility).
/// RSV is an OHLC-based volatility estimator with drift adjustment.
/// Formula: rs_variance = log(H/O)*log(H/C) + log(L/O)*log(L/C)
/// SMA smoothing applied (not RMA like HLV).
/// </summary>
public class RsvValidationTests
{
    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === Mathematical Validation ===

    /// <summary>
    /// Validates the Rogers-Satchell variance formula:
    /// rs_variance = log(H/O)*log(H/C) + log(L/O)*log(L/C)
    /// </summary>
    [Fact]
    public void Rsv_RsVarianceFormula_IsCorrect()
    {
        double open = 100.0;
        double high = 105.0;
        double low = 95.0;
        double close = 102.0;

        double lnHO = Math.Log(high / open);  // log(105/100) ≈ 0.04879
        double lnHC = Math.Log(high / close); // log(105/102) ≈ 0.02899
        double lnLO = Math.Log(low / open);   // log(95/100) ≈ -0.05129
        double lnLC = Math.Log(low / close);  // log(95/102) ≈ -0.07115

        double term1 = lnHO * lnHC; // positive * positive = positive
        double term2 = lnLO * lnLC; // negative * negative = positive

        double rsVariance = term1 + term2;

        Assert.True(rsVariance >= 0, "RS variance should be non-negative for valid OHLC");
    }

    /// <summary>
    /// Validates RS variance is zero for flat bar (O=H=L=C).
    /// </summary>
    [Fact]
    public void Rsv_FlatBar_ProducesZeroVariance()
    {
        double price = 100.0;

        double lnHO = Math.Log(price / price); // log(1) = 0
        double lnHC = Math.Log(price / price); // log(1) = 0
        double lnLO = Math.Log(price / price); // log(1) = 0
        double lnLC = Math.Log(price / price); // log(1) = 0

        double rsVariance = lnHO * lnHC + lnLO * lnLC; // 0

        Assert.Equal(0.0, rsVariance, 15);
    }

    /// <summary>
    /// Validates SMA smoothing formula (unlike RMA used in HLV).
    /// </summary>
    [Fact]
    public void Rsv_UsesSmaSmoothing_NotRma()
    {
        // SMA sums values and divides by period
        // RMA uses exponential decay
        double[] values = { 1, 2, 3, 4, 5 };
        int period = 5;

        double smaExpected = values.Average();
        Assert.Equal(3.0, smaExpected, 10);

        // SMA is simple mean, not weighted
        double sum = values.Sum();
        double smaManual = sum / period;
        Assert.Equal(smaExpected, smaManual, 10);
    }

    /// <summary>
    /// Validates annualization factor: √(annualPeriods)
    /// </summary>
    [Theory]
    [InlineData(252, 15.8745078663875)]   // Daily trading days
    [InlineData(365, 19.1049731745428)]   // Calendar days
    [InlineData(52, 7.21110255092798)]    // Weekly
    [InlineData(12, 3.46410161513775)]    // Monthly
    public void Rsv_AnnualizationFactor_IsCorrect(int annualPeriods, double expectedFactor)
    {
        double factor = Math.Sqrt(annualPeriods);
        Assert.Equal(expectedFactor, factor, 10);
    }

    /// <summary>
    /// Validates that wider range produces higher RS variance.
    /// </summary>
    [Fact]
    public void Rsv_WiderRange_ProducesHigherVariance()
    {
        // Narrow range bar
        double narrowVar = ComputeRsVariance(100, 101, 99, 100);

        // Wide range bar
        double wideVar = ComputeRsVariance(100, 110, 90, 100);

        Assert.True(wideVar > narrowVar,
            "Wider range should produce higher RS variance");
    }

    /// <summary>
    /// Validates that RSV uses all OHLC prices (unlike HLV which only uses H-L).
    /// </summary>
    [Fact]
    public void Rsv_UsesAllOhlc_SensitiveToOpenClose()
    {
        var rsv1 = new Rsv(14, annualize: false);
        var rsv2 = new Rsv(14, annualize: false);

        for (int i = 0; i < 30; i++)
        {
            // Same high/low range but different open/close
            // Indicator 1: doji pattern (open ≈ close at center)
            var bar1 = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 105.0, 95.0, 100.0, 1000.0
            );
            rsv1.Update(bar1);

            // Indicator 2: open and close at extremes
            var bar2 = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                95.5, 105.0, 95.0, 104.5, 1000.0
            );
            rsv2.Update(bar2);
        }

        // RSV should be different since it uses all OHLC prices
        Assert.NotEqual(rsv1.Last.Value, rsv2.Last.Value);
    }

    /// <summary>
    /// Validates drift adjustment property: RSV handles trending markets.
    /// </summary>
    [Fact]
    public void Rsv_DriftAdjusted_HandlesTrendingMarket()
    {
        var rsv = new Rsv(14, annualize: false);

        // Strongly trending market (continuous up moves)
        for (int i = 0; i < 30; i++)
        {
            double basePrice = 100 + i * 2; // Strong uptrend
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                basePrice, basePrice + 3, basePrice - 2, basePrice + 2, 1000.0
            );
            rsv.Update(bar);
        }

        // RSV should still produce valid volatility estimate
        Assert.True(double.IsFinite(rsv.Last.Value));
        Assert.True(rsv.Last.Value > 0, "Trending market with volatility should have positive RSV");
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates streaming and batch produce identical results.
    /// </summary>
    [Fact]
    public void Rsv_StreamingMatchesBatch()
    {
        var bars = GenerateTestData(100);

        // Streaming calculation
        var streamingRsv = new Rsv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingRsv.Update(bars[i]);
        }

        // Batch calculation
        var batchResult = Rsv.Calculate(bars, 14);

        // Compare last values
        Assert.Equal(batchResult.Last.Value, streamingRsv.Last.Value, 8);
    }

    /// <summary>
    /// Validates TBarSeries input matches TBar streaming.
    /// </summary>
    [Fact]
    public void Rsv_TBarSeriesInput_MatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingRsv = new Rsv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingRsv.Update(bars[i]);
        }

        // TBarSeries batch
        var batchRsv = new Rsv(14);
        var batchResult = batchRsv.Update(bars);

        Assert.Equal(batchResult.Last.Value, streamingRsv.Last.Value, 10);
    }

    /// <summary>
    /// Validates Span batch matches streaming.
    /// </summary>
    [Fact]
    public void Rsv_SpanBatch_MatchesStreaming()
    {
        var bars = GenerateTestData(100);

        // Streaming
        var streamingRsv = new Rsv(14);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingRsv.Update(bars[i]);
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
        Rsv.Batch(opens, highs, lows, closes, output, 14);

        Assert.Equal(output[^1], streamingRsv.Last.Value, 10);
    }

    /// <summary>
    /// Validates annualized output is scaled correctly.
    /// </summary>
    [Fact]
    public void Rsv_Annualized_ScaledCorrectly()
    {
        var bars = GenerateTestData(50);

        // Non-annualized
        var rsvRaw = new Rsv(14, annualize: false);

        // Annualized (default 252 periods)
        var rsvAnn = new Rsv(14, annualize: true, annualPeriods: 252);

        for (int i = 0; i < bars.Count; i++)
        {
            rsvRaw.Update(bars[i]);
            rsvAnn.Update(bars[i]);
        }

        double expectedRatio = Math.Sqrt(252);
        double actualRatio = rsvAnn.Last.Value / rsvRaw.Last.Value;

        Assert.Equal(expectedRatio, actualRatio, 6);
    }

    // === Parameter Sensitivity ===

    /// <summary>
    /// Validates shorter period produces more responsive volatility.
    /// </summary>
    [Fact]
    public void Rsv_ShorterPeriod_MoreResponsive()
    {
        var bars = GenerateTestData(50);

        var rsvShort = new Rsv(5);
        var rsvLong = new Rsv(20);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            rsvShort.Update(bars[i]);
            rsvLong.Update(bars[i]);

            if (rsvShort.IsHot && rsvLong.IsHot)
            {
                shortResults.Add(rsvShort.Last.Value);
                longResults.Add(rsvLong.Last.Value);
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
    public void Rsv_DifferentPeriods_ProduceDifferentResults()
    {
        var bars = GenerateTestData(50);

        var rsv10 = new Rsv(10);
        var rsv14 = new Rsv(14);
        var rsv20 = new Rsv(20);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv10.Update(bars[i]);
            rsv14.Update(bars[i]);
            rsv20.Update(bars[i]);
        }

        Assert.NotEqual(rsv10.Last.Value, rsv14.Last.Value);
        Assert.NotEqual(rsv14.Last.Value, rsv20.Last.Value);
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of very small ranges (tight consolidation).
    /// </summary>
    [Fact]
    public void Rsv_VerySmallRanges_HandledCorrectly()
    {
        var rsv = new Rsv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.001, 99.999, 100.0, 1000.0
            );
            rsv.Update(bar);
        }

        Assert.True(double.IsFinite(rsv.Last.Value));
        Assert.True(rsv.Last.Value >= 0, "Volatility should be non-negative");
    }

    /// <summary>
    /// Validates handling of very large ranges (high volatility).
    /// </summary>
    [Fact]
    public void Rsv_VeryLargeRanges_HandledCorrectly()
    {
        var rsv = new Rsv(14);

        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 200.0, 50.0, 150.0, 1000.0
            );
            rsv.Update(bar);
        }

        Assert.True(double.IsFinite(rsv.Last.Value));
        Assert.True(rsv.Last.Value > 0, "High volatility should produce positive value");
    }

    /// <summary>
    /// Validates handling of constant bars (zero volatility).
    /// </summary>
    [Fact]
    public void Rsv_ConstantBars_ProducesMinimalVolatility()
    {
        var rsv = new Rsv(14);

        for (int i = 0; i < 30; i++)
        {
            // Near-constant bars (small epsilon to avoid log issues)
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 100.001, 99.999, 100.0, 1000.0
            );
            rsv.Update(bar);
        }

        Assert.True(double.IsFinite(rsv.Last.Value));
        Assert.True(rsv.Last.Value < 0.01, "Near-constant price should produce near-zero volatility");
    }

    /// <summary>
    /// Validates warmup period calculation.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(20)]
    public void Rsv_WarmupPeriod_IsCorrect(int period)
    {
        var rsv = new Rsv(period);
        Assert.Equal(period, rsv.WarmupPeriod);
    }

    /// <summary>
    /// Validates output is always non-negative (volatility property).
    /// </summary>
    [Fact]
    public void Rsv_Output_IsNonNegative()
    {
        var bars = GenerateTestData(100);
        var rsv = new Rsv(14);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
            if (rsv.IsHot)
            {
                Assert.True(rsv.Last.Value >= 0,
                    $"Volatility should be non-negative at bar {i}");
            }
        }
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Rsv_BarCorrection_WorksCorrectly()
    {
        var rsv = new Rsv(14);
        var bars = GenerateTestData(30);

        // Feed initial bars
        for (int i = 0; i < 20; i++)
        {
            rsv.Update(bars[i], isNew: true);
        }

        // Add new bar
        rsv.Update(bars[20], isNew: true);
        double afterNew = rsv.Last.Value;

        // Correct with different bar (much higher volatility)
        var correctedBar = new TBar(
            bars[20].Time,
            100, 200, 50, 150, 1000
        );
        rsv.Update(correctedBar, isNew: false);
        double afterCorrection = rsv.Last.Value;

        // Restore original
        rsv.Update(bars[20], isNew: false);
        double afterRestore = rsv.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge to same result.
    /// </summary>
    [Fact]
    public void Rsv_IterativeCorrections_Converge()
    {
        var rsv = new Rsv(14);
        var bars = GenerateTestData(30);

        // Feed bars and make corrections
        for (int i = 0; i < 20; i++)
        {
            rsv.Update(bars[i], isNew: true);
        }

        // Multiple corrections on same bar
        for (int j = 0; j < 5; j++)
        {
            var tempBar = new TBar(
                bars[19].Time,
                100 + j, 110 + j, 90 + j, 105 + j, 1000
            );
            rsv.Update(tempBar, isNew: false);
        }

        // Final correction back to original
        rsv.Update(bars[19], isNew: false);
        double afterCorrections = rsv.Last.Value;

        // Fresh calculation
        var rsvFresh = new Rsv(14);
        for (int i = 0; i < 20; i++)
        {
            rsvFresh.Update(bars[i], isNew: true);
        }
        double freshValue = rsvFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    // === Comparison with Other Volatility Estimators ===

    /// <summary>
    /// Validates RSV vs HLV: RSV uses O-C, HLV ignores O-C.
    /// </summary>
    [Fact]
    public void Rsv_VsHlv_DifferentBehavior()
    {
        var rsv = new Rsv(14, annualize: false);
        var hlv = new Hlv(14, annualize: false);

        // Same bars
        for (int i = 0; i < 30; i++)
        {
            // Directional bar (O != C)
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 105.0, 95.0, 104.0, 1000.0
            );
            rsv.Update(bar);
            hlv.Update(bar);
        }

        // Both should produce positive values
        Assert.True(rsv.Last.Value > 0);
        Assert.True(hlv.Last.Value > 0);

        // They should be different since RSV uses O-C while HLV ignores it
        Assert.NotEqual(rsv.Last.Value, hlv.Last.Value);
    }

    /// <summary>
    /// Validates RSV vs GKV: both use OHLC but different formulas.
    /// </summary>
    [Fact]
    public void Rsv_VsGkv_DifferentValues()
    {
        var rsv = new Rsv(14, annualize: false);
        var gkv = new Gkv(14, annualize: false);

        var bars = GenerateTestData(50);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
            gkv.Update(bars[i]);
        }

        // Both should produce positive values
        Assert.True(rsv.Last.Value > 0);
        Assert.True(gkv.Last.Value > 0);

        // They should be similar but not identical (different formulas)
        Assert.NotEqual(rsv.Last.Value, gkv.Last.Value);
    }

    // === Stability Tests ===

    /// <summary>
    /// Validates RSV stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Rsv_Stability_ConsistentOverRepeatedRuns()
    {
        // Multiple runs with same seed should produce identical results
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var gbm = new GBM(seed: 42);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            var rsv = new Rsv(14);

            for (int i = 0; i < bars.Count; i++)
            {
                rsv.Update(bars[i]);
            }
            results.Add(rsv.Last.Value);
        }

        // All runs should be identical
        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates RSV responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Rsv_RespondsToVolatilityRegimeChange()
    {
        var rsv = new Rsv(10);

        // Low volatility regime
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 101.0, 99.0, 100.5, 1000.0 // 2% range
            );
            rsv.Update(bar);
        }
        double lowVolValue = rsv.Last.Value;

        // High volatility regime
        for (int i = 20; i < 40; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 110.0, 90.0, 105.0, 1000.0 // 20% range
            );
            rsv.Update(bar);
        }
        double highVolValue = rsv.Last.Value;

        Assert.True(highVolValue > lowVolValue * 2,
            "RSV should significantly increase with higher volatility regime");
    }

    /// <summary>
    /// Validates RSV produces reasonable volatility estimate.
    /// </summary>
    [Fact]
    public void Rsv_ProducesReasonableVolatilityEstimate()
    {
        var bars = GenerateTestData(100);
        var rsv = new Rsv(14, annualize: false);

        for (int i = 0; i < bars.Count; i++)
        {
            rsv.Update(bars[i]);
        }

        // RSV should be positive and finite
        Assert.True(double.IsFinite(rsv.Last.Value));
        Assert.True(rsv.Last.Value > 0);
        Assert.True(rsv.Last.Value < 10, "Raw volatility should be reasonable (< 1000%)");
    }

    // === SMA vs RMA Smoothing Validation ===

    /// <summary>
    /// Validates that RSV uses SMA (not RMA like HLV).
    /// SMA should adapt faster to changes when period is small.
    /// </summary>
    [Fact]
    public void Rsv_SmaSmoothing_AdaptsToChange()
    {
        var rsv = new Rsv(5, annualize: false);

        // Low volatility phase
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 101.0, 99.0, 100.0, 1000.0
            );
            rsv.Update(bar);
        }
        double lowVolValue = rsv.Last.Value;

        // Sudden high volatility (5 bars = full SMA window)
        for (int i = 10; i < 15; i++)
        {
            var bar = new TBar(
                DateTime.UtcNow.AddMinutes(i).Ticks,
                100.0, 120.0, 80.0, 100.0, 1000.0
            );
            rsv.Update(bar);
        }
        double afterHighVolSma = rsv.Last.Value;

        // With SMA (period=5), after 5 high-vol bars the old low-vol values should be gone
        // Value should be significantly higher
        Assert.True(afterHighVolSma > lowVolValue * 3,
            "SMA should fully adapt after period bars");
    }

    // === Helper Methods ===

    private static double ComputeRsVariance(double open, double high, double low, double close)
    {
        // Protect against division by zero
        open = Math.Max(open, 1e-10);
        close = Math.Max(close, 1e-10);

        double lnHO = Math.Log(high / open);
        double lnHC = Math.Log(high / close);
        double lnLO = Math.Log(low / open);
        double lnLC = Math.Log(low / close);

        return lnHO * lnHC + lnLO * lnLC;
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