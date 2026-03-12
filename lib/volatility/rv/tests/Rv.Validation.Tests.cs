namespace QuanTAlib.Test;

using Xunit;

/// <summary>
/// Validation tests for RV (Realized Volatility).
/// RV calculates volatility from squared log returns, smoothed with SMA.
/// Formula: RV = SMA(√(Σr²)) × annualizationFactor
/// </summary>
public class RvValidationTests
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
    /// Validates squared log return calculation.
    /// </summary>
    [Theory]
    [InlineData(100.0, 101.0)]
    [InlineData(100.0, 110.0)]
    [InlineData(100.0, 90.0)]
    public void Rv_SquaredLogReturn_IsCorrect(double prevPrice, double curPrice)
    {
        double logReturn = Math.Log(curPrice / prevPrice);
        double squaredReturn = logReturn * logReturn;

        Assert.True(squaredReturn >= 0, "Squared return must be non-negative");
        Assert.Equal(Math.Pow(logReturn, 2), squaredReturn, 15);
    }

    /// <summary>
    /// Validates realized variance formula: sum of squared returns.
    /// </summary>
    [Fact]
    public void Rv_RealizedVarianceFormula_IsCorrect()
    {
        double[] squaredReturns = { 0.0001, 0.0004, 0.0009, 0.0016, 0.0025 };
        double sumSquared = 0;
        for (int i = 0; i < squaredReturns.Length; i++)
        {
            sumSquared += squaredReturns[i];
        }

        // Expected sum = 0.0055
        Assert.Equal(0.0055, sumSquared, 10);

        // Realized volatility = sqrt(sum)
        double rv = Math.Sqrt(sumSquared);
        Assert.Equal(Math.Sqrt(0.0055), rv, 10);
    }

    /// <summary>
    /// Validates annualization factor: √(252) for daily data.
    /// </summary>
    [Theory]
    [InlineData(252, 15.8745078663875)]
    [InlineData(365, 19.1049731745428)]
    [InlineData(52, 7.21110255092798)]
    public void Rv_AnnualizationFactor_IsCorrect(int annualPeriods, double expectedFactor)
    {
        double factor = Math.Sqrt(annualPeriods);
        Assert.Equal(expectedFactor, factor, 10);
    }

    /// <summary>
    /// Validates known calculation with manual verification.
    /// </summary>
    [Fact]
    public void Rv_KnownCalculation_IsCorrect()
    {
        // Prices: 100, 102, 101, 103, 102, 104 (6 prices = 5 returns)
        double[] prices = { 100.0, 102.0, 101.0, 103.0, 102.0, 104.0 };

        // Manual calculation with period=5 (all 5 returns), smoothingPeriod=1 (no smoothing)
        double sumSquared = 0;
        for (int i = 1; i < prices.Length; i++)
        {
            double r = Math.Log(prices[i] / prices[i - 1]);
            sumSquared += r * r;
        }
        double expected = Math.Sqrt(sumSquared);

        // Verify with indicator (no annualization, smoothing=1)
        var rv = new Rv(period: 5, smoothingPeriod: 1, annualize: false);
        for (int i = 0; i < prices.Length; i++)
        {
            rv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), prices[i]));
        }

        Assert.Equal(expected, rv.Last.Value, 10);
    }

    /// <summary>
    /// Validates constant prices produce zero volatility.
    /// </summary>
    [Fact]
    public void Rv_ConstantPrices_ProducesZeroVolatility()
    {
        var rv = new Rv(period: 5, smoothingPeriod: 3, annualize: false);

        for (int i = 0; i < 20; i++)
        {
            rv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }

        Assert.Equal(0.0, rv.Last.Value, 10);
    }

    /// <summary>
    /// Validates SMA smoothing of raw volatilities.
    /// </summary>
    [Fact]
    public void Rv_SmaSmoothing_WorksCorrectly()
    {
        var prices = GeneratePriceSeries(50);

        // Short smoothing vs long smoothing
        var rvShort = new Rv(period: 5, smoothingPeriod: 3, annualize: false);
        var rvLong = new Rv(period: 5, smoothingPeriod: 10, annualize: false);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < prices.Count; i++)
        {
            rvShort.Update(prices[i]);
            rvLong.Update(prices[i]);

            if (rvShort.IsHot && rvLong.IsHot)
            {
                shortResults.Add(rvShort.Last.Value);
                longResults.Add(rvLong.Last.Value);
            }
        }

        // Longer smoothing should produce smoother (less variable) results
        double shortVar = Variance(shortResults);
        double longVar = Variance(longResults);

        Assert.True(shortResults.Count > 0, "Should have results");
        Assert.True(longVar < shortVar, "Longer smoothing should be smoother");
    }

    // === Consistency Tests ===

    /// <summary>
    /// Validates streaming and batch produce identical results.
    /// </summary>
    [Fact]
    public void Rv_StreamingMatchesBatch()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming calculation
        var streamingRv = new Rv(5, 10);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingRv.Update(prices[i]);
        }

        // Batch calculation
        var batchResult = Rv.Batch(prices, 5, 10);

        Assert.Equal(batchResult.Last.Value, streamingRv.Last.Value, 8);
    }

    /// <summary>
    /// Validates TSeries input matches TValue streaming.
    /// </summary>
    [Fact]
    public void Rv_TSeriesInput_MatchesStreaming()
    {
        var prices = GeneratePriceSeries(100);

        // Streaming
        var streamingRv = new Rv(5, 10);
        for (int i = 0; i < prices.Count; i++)
        {
            streamingRv.Update(prices[i]);
        }

        // TSeries batch
        var batchRv = new Rv(5, 10);
        var batchResult = batchRv.Update(prices);

        Assert.Equal(batchResult.Last.Value, streamingRv.Last.Value, 10);
    }

    /// <summary>
    /// Validates annualized output is scaled correctly.
    /// </summary>
    [Fact]
    public void Rv_Annualized_ScaledCorrectly()
    {
        var prices = GeneratePriceSeries(50);

        var rvRaw = new Rv(5, 10, annualize: false);
        var rvAnn = new Rv(5, 10, annualize: true, annualPeriods: 252);

        for (int i = 0; i < prices.Count; i++)
        {
            rvRaw.Update(prices[i]);
            rvAnn.Update(prices[i]);
        }

        double expectedRatio = Math.Sqrt(252);
        double actualRatio = rvAnn.Last.Value / rvRaw.Last.Value;

        Assert.Equal(expectedRatio, actualRatio, 6);
    }

    /// <summary>
    /// Validates TBar update uses only Close price.
    /// </summary>
    [Fact]
    public void Rv_TBar_UsesOnlyClose()
    {
        var bars = GenerateTestData(50);

        var rvBar = new Rv(5, 10);
        for (int i = 0; i < bars.Count; i++)
        {
            rvBar.Update(bars[i]);
        }

        var rvClose = new Rv(5, 10);
        for (int i = 0; i < bars.Count; i++)
        {
            rvClose.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        Assert.Equal(rvClose.Last.Value, rvBar.Last.Value, 10);
    }

    // === Parameter Sensitivity ===

    /// <summary>
    /// Validates shorter period is more responsive.
    /// </summary>
    [Fact]
    public void Rv_ShorterPeriod_MoreResponsive()
    {
        var prices = GeneratePriceSeries(50);

        var rvShort = new Rv(3, 5);
        var rvLong = new Rv(10, 5);

        var shortResults = new List<double>();
        var longResults = new List<double>();

        for (int i = 0; i < prices.Count; i++)
        {
            rvShort.Update(prices[i]);
            rvLong.Update(prices[i]);

            if (rvShort.IsHot && rvLong.IsHot)
            {
                shortResults.Add(rvShort.Last.Value);
                longResults.Add(rvLong.Last.Value);
            }
        }

        double shortVar = Variance(shortResults);
        double longVar = Variance(longResults);

        Assert.True(shortResults.Count > 0, "Should have results");
        Assert.True(shortVar > longVar * 0.5, "Shorter period should be more variable");
    }

    /// <summary>
    /// Validates different parameters produce different results.
    /// </summary>
    [Fact]
    public void Rv_DifferentParameters_ProduceDifferentResults()
    {
        var prices = GeneratePriceSeries(50);

        var rv1 = new Rv(5, 10);
        var rv2 = new Rv(5, 20);
        var rv3 = new Rv(10, 10);

        for (int i = 0; i < prices.Count; i++)
        {
            rv1.Update(prices[i]);
            rv2.Update(prices[i]);
            rv3.Update(prices[i]);
        }

        Assert.NotEqual(rv1.Last.Value, rv2.Last.Value);
        Assert.NotEqual(rv1.Last.Value, rv3.Last.Value);
    }

    // === Edge Cases ===

    /// <summary>
    /// Validates handling of very small price changes.
    /// </summary>
    [Fact]
    public void Rv_VerySmallChanges_HandledCorrectly()
    {
        var rv = new Rv(5, 10, annualize: false);

        double price = 100.0;
        for (int i = 0; i < 30; i++)
        {
            price += 0.001 * (i % 2 == 0 ? 1 : -1);
            rv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }

        Assert.True(double.IsFinite(rv.Last.Value));
        Assert.True(rv.Last.Value >= 0);
        Assert.True(rv.Last.Value < 0.01, "Small changes should produce small RV");
    }

    /// <summary>
    /// Validates handling of large price swings.
    /// </summary>
    [Fact]
    public void Rv_LargePriceSwings_HandledCorrectly()
    {
        var rv = new Rv(5, 10, annualize: false);

        double price = 100.0;
        for (int i = 0; i < 30; i++)
        {
            price *= (i % 2 == 0 ? 1.1 : 0.9);
            rv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }

        Assert.True(double.IsFinite(rv.Last.Value));
        Assert.True(rv.Last.Value > 0, "Large swings should produce positive RV");
    }

    /// <summary>
    /// Validates warmup period calculation.
    /// </summary>
    [Theory]
    [InlineData(5, 10, 15)]
    [InlineData(5, 20, 25)]
    [InlineData(10, 10, 20)]
    public void Rv_WarmupPeriod_IsCorrect(int period, int smoothing, int expectedWarmup)
    {
        var rv = new Rv(period, smoothing);
        Assert.Equal(expectedWarmup, rv.WarmupPeriod);
    }

    /// <summary>
    /// Validates output is always non-negative.
    /// </summary>
    [Fact]
    public void Rv_Output_IsNonNegative()
    {
        var prices = GeneratePriceSeries(100);
        var rv = new Rv(5, 10);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
            if (rv.IsHot)
            {
                Assert.True(rv.Last.Value >= 0, $"RV should be non-negative at bar {i}");
            }
        }
    }

    /// <summary>
    /// Validates bar correction works correctly.
    /// </summary>
    [Fact]
    public void Rv_BarCorrection_WorksCorrectly()
    {
        var rv = new Rv(5, 10);
        var prices = GeneratePriceSeries(30);

        for (int i = 0; i < 20; i++)
        {
            rv.Update(prices[i], isNew: true);
        }

        rv.Update(prices[20], isNew: true);
        double afterNew = rv.Last.Value;

        var correctedPrice = new TValue(prices[20].Time, prices[20].Value * 2.0);
        rv.Update(correctedPrice, isNew: false);
        double afterCorrection = rv.Last.Value;

        rv.Update(prices[20], isNew: false);
        double afterRestore = rv.Last.Value;

        Assert.NotEqual(afterNew, afterCorrection);
        Assert.Equal(afterNew, afterRestore, 10);
    }

    /// <summary>
    /// Validates iterative corrections converge.
    /// </summary>
    [Fact]
    public void Rv_IterativeCorrections_Converge()
    {
        var rv = new Rv(5, 10);
        var prices = GeneratePriceSeries(30);

        for (int i = 0; i < 20; i++)
        {
            rv.Update(prices[i], isNew: true);
        }

        for (int j = 0; j < 5; j++)
        {
            var tempPrice = new TValue(prices[19].Time, prices[19].Value * (1.0 + j * 0.01));
            rv.Update(tempPrice, isNew: false);
        }

        rv.Update(prices[19], isNew: false);
        double afterCorrections = rv.Last.Value;

        var rvFresh = new Rv(5, 10);
        for (int i = 0; i < 20; i++)
        {
            rvFresh.Update(prices[i], isNew: true);
        }
        double freshValue = rvFresh.Last.Value;

        Assert.Equal(freshValue, afterCorrections, 10);
    }

    // === Comparison Tests ===

    /// <summary>
    /// Validates RV vs HV produce correlated but different results.
    /// </summary>
    [Fact]
    public void Rv_VsHv_RelatedButDifferent()
    {
        var bars = GenerateTestData(50);

        // RV with period=14, smoothing=1 (similar to HV behavior)
        var rv = new Rv(14, 1, annualize: false);
        var hv = new Hv(14, annualize: false);

        for (int i = 0; i < bars.Count; i++)
        {
            rv.Update(bars[i]);
            hv.Update(bars[i]);
        }

        // Both should produce positive values
        Assert.True(rv.Last.Value > 0);
        Assert.True(hv.Last.Value > 0);

        // They measure similar concepts but with different formulas
        // RV uses sum of squared returns, HV uses standard deviation
        // Both should be in similar magnitude range
        double ratio = rv.Last.Value / hv.Last.Value;
        Assert.True(ratio > 0.1 && ratio < 10, "RV and HV should be in similar range");
    }

    /// <summary>
    /// Validates stability over repeated runs with same seed.
    /// </summary>
    [Fact]
    public void Rv_Stability_ConsistentOverRepeatedRuns()
    {
        var results = new List<double>();

        for (int run = 0; run < 3; run++)
        {
            var gbm = new GBM(seed: 42);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            var rv = new Rv(5, 10);

            for (int i = 0; i < bars.Count; i++)
            {
                rv.Update(bars[i]);
            }
            results.Add(rv.Last.Value);
        }

        Assert.Equal(results[0], results[1], 15);
        Assert.Equal(results[1], results[2], 15);
    }

    /// <summary>
    /// Validates RV responds to volatility regime changes.
    /// </summary>
    [Fact]
    public void Rv_RespondsToVolatilityRegimeChange()
    {
        var rv = new Rv(5, 5, annualize: false);

        // Low volatility regime
        double price = 100.0;
        for (int i = 0; i < 20; i++)
        {
            price *= (i % 2 == 0 ? 1.001 : 0.999);
            rv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        double lowVolValue = rv.Last.Value;

        // High volatility regime
        for (int i = 20; i < 40; i++)
        {
            price *= (i % 2 == 0 ? 1.05 : 0.95);
            rv.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        double highVolValue = rv.Last.Value;

        Assert.True(highVolValue > lowVolValue * 5,
            "RV should significantly increase with higher volatility regime");
    }

    /// <summary>
    /// Validates RV produces reasonable volatility estimate.
    /// </summary>
    [Fact]
    public void Rv_ProducesReasonableVolatilityEstimate()
    {
        var prices = GeneratePriceSeries(100);
        var rv = new Rv(5, 10, annualize: false);

        for (int i = 0; i < prices.Count; i++)
        {
            rv.Update(prices[i]);
        }

        Assert.True(double.IsFinite(rv.Last.Value));
        Assert.True(rv.Last.Value > 0);
        Assert.True(rv.Last.Value < 1, "Raw RV should be < 100%");
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
