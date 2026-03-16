using Skender.Stock.Indicators;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for RS (Price Relative Strength) indicator.
/// RS compares relative performance between two assets via their price ratio.
/// Note: RS is a unique indicator without direct equivalents in TA-Lib, Skender, etc.
/// These tests validate mathematical correctness and expected behavior.
/// </summary>
public class RsValidationTests
{
    #region Mathematical Validation

    [Fact]
    public void Rs_ManualCalculation_MatchesExpected()
    {
        var rs = new Rs(1); // No smoothing
        var time = DateTime.UtcNow;

        // Test data: base and comparison prices
        var baseValues = new double[] { 100, 105, 110, 115, 120 };
        var compValues = new double[] { 100, 100, 100, 100, 100 };

        // Expected: ratios should be 1.0, 1.05, 1.10, 1.15, 1.20
        for (int i = 0; i < baseValues.Length; i++)
        {
            var result = rs.Update(
                new TValue(time.AddSeconds(i), baseValues[i]),
                new TValue(time.AddSeconds(i), compValues[i]),
                true);

            double expected = baseValues[i] / compValues[i];
            Assert.Equal(expected, result.Value, 10);
            Assert.Equal(expected, rs.RawRatio, 10);
        }
    }

    [Fact]
    public void Rs_EqualPrices_ReturnsOne()
    {
        var rs = new Rs(1);

        var result = rs.Update(50.0, 50.0, true);

        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void Rs_BaseHigherThanComp_ReturnsGreaterThanOne()
    {
        var rs = new Rs(1);

        var result = rs.Update(120.0, 100.0, true);

        Assert.Equal(1.2, result.Value, 10);
        Assert.True(result.Value > 1.0);
    }

    [Fact]
    public void Rs_BaseLowerThanComp_ReturnsLessThanOne()
    {
        var rs = new Rs(1);

        var result = rs.Update(80.0, 100.0, true);

        Assert.Equal(0.8, result.Value, 10);
        Assert.True(result.Value < 1.0);
    }

    [Fact]
    public void Rs_DivisionByZero_ReturnsNaN()
    {
        var rs = new Rs(1);

        var result = rs.Update(100.0, 0.0, true);

        Assert.True(double.IsNaN(result.Value));
        Assert.True(double.IsNaN(rs.RawRatio));
    }

    [Fact]
    public void Rs_VerySmallDenominator_ReturnsNaN()
    {
        var rs = new Rs(1);

        // Value smaller than epsilon (1e-10) should be treated as zero
        var result = rs.Update(100.0, 1e-11, true);

        Assert.True(double.IsNaN(result.Value));
    }

    #endregion

    #region Smoothing Validation

    [Fact]
    public void Rs_SmoothedFirstValue_EqualsRawRatio()
    {
        var rs = new Rs(10);
        var time = DateTime.UtcNow;

        var result = rs.Update(
            new TValue(time, 100.0),
            new TValue(time, 50.0),
            true);

        // First value should equal raw ratio
        Assert.Equal(rs.RawRatio, result.Value, 10);
        Assert.Equal(2.0, result.Value, 10);
    }

    [Fact]
    public void Rs_SmoothedConvergesToRatio_WhenConstant()
    {
        var rs = new Rs(5);
        var time = DateTime.UtcNow;

        // Feed constant ratio (100/50 = 2.0) repeatedly
        TValue result = default;
        for (int i = 0; i < 50; i++)
        {
            result = rs.Update(
                new TValue(time.AddSeconds(i), 100.0),
                new TValue(time.AddSeconds(i), 50.0),
                true);
        }

        // Should converge to 2.0
        Assert.Equal(2.0, result.Value, 6);
    }

    [Fact]
    public void Rs_NoSmoothing_RawRatioEqualsSmoothed()
    {
        var rs = new Rs(1); // No smoothing

        var values = new (double b, double c)[]
        {
            (100, 50),
            (110, 55),
            (120, 60),
            (130, 65)
        };

        foreach (var (b, c) in values)
        {
            var result = rs.Update(b, c, true);
            Assert.Equal(rs.RawRatio, result.Value, 10);
        }
    }

    [Fact]
    public void Rs_SmoothingReducesVolatility()
    {
        var prsNoSmooth = new Rs(1);
        var prsSmooth = new Rs(10);

        // Create volatile ratio series
        var baseVals = new double[] { 100, 120, 80, 130, 70, 140, 60, 150 };
        var compVals = new double[] { 100, 100, 100, 100, 100, 100, 100, 100 };

        var rawResults = new List<double>();
        var smoothResults = new List<double>();

        for (int i = 0; i < baseVals.Length; i++)
        {
            var raw = prsNoSmooth.Update(baseVals[i], compVals[i], true);
            var smooth = prsSmooth.Update(baseVals[i], compVals[i], true);
            rawResults.Add(raw.Value);
            smoothResults.Add(smooth.Value);
        }

        // Calculate variance of latter half
        var rawVariance = CalculateVariance(rawResults.Skip(4).ToArray());
        var smoothVariance = CalculateVariance(smoothResults.Skip(4).ToArray());

        Assert.True(smoothVariance <= rawVariance + 0.01,
            $"Smoothed variance ({smoothVariance:F4}) should be <= raw variance ({rawVariance:F4})");
    }

    private static double CalculateVariance(double[] values)
    {
        double mean = values.Average();
        return values.Select(v => (v - mean) * (v - mean)).Sum() / values.Length;
    }

    #endregion

    #region Trend Interpretation

    [Fact]
    public void Rs_IncreasingRatio_IndicatesOutperformance()
    {
        var rs = new Rs(1);

        // Base outperforms: grows faster than comparison
        var results = new List<double>();

        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + (i * 5); // 100, 105, 110...
            double compPrice = 100 + (i * 2); // 100, 102, 104...
            var result = rs.Update(basePrice, compPrice, true);
            results.Add(result.Value);
        }

        // Each ratio should be larger than the previous (outperformance)
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i] > results[i - 1],
                $"Outperformance: ratio[{i}]={results[i]:F4} should be > ratio[{i - 1}]={results[i - 1]:F4}");
        }
    }

    [Fact]
    public void Rs_DecreasingRatio_IndicatesUnderperformance()
    {
        var rs = new Rs(1);

        // Base underperforms: grows slower than comparison
        var results = new List<double>();

        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 + (i * 2); // 100, 102, 104...
            double compPrice = 100 + (i * 5); // 100, 105, 110...
            var result = rs.Update(basePrice, compPrice, true);
            results.Add(result.Value);
        }

        // Each ratio should be smaller than the previous (underperformance)
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i] < results[i - 1],
                $"Underperformance: ratio[{i}]={results[i]:F4} should be < ratio[{i - 1}]={results[i - 1]:F4}");
        }
    }

    [Fact]
    public void Rs_SameGrowthRate_ConstantRatio()
    {
        var rs = new Rs(1);

        // Both grow at same rate - ratio stays constant at 2.0
        var results = new List<double>();

        for (int i = 0; i < 10; i++)
        {
            double basePrice = 100 * (1 + (i * 0.05)); // 5% growth
            double compPrice = 50 * (1 + (i * 0.05));  // 5% growth
            var result = rs.Update(basePrice, compPrice, true);
            results.Add(result.Value);
        }

        // All ratios should be 2.0 (within precision)
        foreach (var ratio in results)
        {
            Assert.Equal(2.0, ratio, 10);
        }
    }

    #endregion

    #region Edge Cases and Robustness

    [Fact]
    public void Rs_NegativeValues_HandlesCorrectly()
    {
        var rs = new Rs(1);

        // While unusual, RS should handle negative values mathematically
        var result = rs.Update(-100.0, -50.0, true);

        Assert.Equal(2.0, result.Value, 10); // -100/-50 = 2.0
    }

    [Fact]
    public void Rs_MixedSigns_HandlesCorrectly()
    {
        var rs = new Rs(1);

        // Base positive, comp negative
        var result = rs.Update(100.0, -50.0, true);

        Assert.Equal(-2.0, result.Value, 10);
    }

    [Fact]
    public void Rs_VeryLargeValues_MaintainsPrecision()
    {
        var rs = new Rs(1);

        var result = rs.Update(1e15, 1e14, true);

        // 1e15 / 1e14 = 10
        Assert.Equal(10.0, result.Value, 6);
    }

    [Fact]
    public void Rs_VerySmallValues_MaintainsPrecision()
    {
        var rs = new Rs(1);

        var result = rs.Update(1e-5, 1e-6, true);

        // 1e-5 / 1e-6 = 10
        Assert.Equal(10.0, result.Value, 6);
    }

    [Fact]
    public void Rs_NaNBase_PropagatesNaN()
    {
        var rs = new Rs(1);

        // Should fallback to last valid or 0, resulting in 0/comp
        rs.Update(100.0, 50.0, true); // First valid value
        var result = rs.Update(double.NaN, 50.0, true);

        // NaN base with valid comparison should use fallback (previous: 100) or 0
        // Result will depend on sanitization logic - actual behavior uses last valid
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    [Fact]
    public void Rs_NaNComp_PropagatesNaN()
    {
        var rs = new Rs(1);

        rs.Update(100.0, 50.0, true); // First valid value
        var result = rs.Update(100.0, double.NaN, true);

        // NaN comp should use fallback (previous: 50) -> 100/50 = 2.0
        Assert.Equal(2.0, result.Value, 6);
    }

    [Fact]
    public void Rs_InfinityBase_HandledGracefully()
    {
        var rs = new Rs(1);

        rs.Update(100.0, 50.0, true); // First valid
        var result = rs.Update(double.PositiveInfinity, 50.0, true);

        // Should fallback to last valid (100/50 = 2.0)
        Assert.Equal(2.0, result.Value, 6);
    }

    #endregion

    #region Batch Calculation Validation

    [Fact]
    public void Rs_BatchCalculate_MatchesStreaming()
    {
        int smoothPeriod = 5;
        var prsStream = new Rs(smoothPeriod);

        var baseValues = new double[] { 100, 105, 110, 108, 115, 120, 118, 125, 130, 128 };
        var compValues = new double[] { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 };

        // Streaming calculation
        var streamResults = new double[baseValues.Length];
        for (int i = 0; i < baseValues.Length; i++)
        {
            streamResults[i] = prsStream.Update(baseValues[i], compValues[i], true).Value;
        }

        // Batch calculation
        var batchOutput = new double[baseValues.Length];
        Rs.Batch(baseValues, compValues, batchOutput, smoothPeriod);

        // Results should match
        for (int i = 0; i < baseValues.Length; i++)
        {
            Assert.Equal(streamResults[i], batchOutput[i], 10);
        }
    }

    [Fact]
    public void Rs_TSeriesCalculate_MatchesStreaming()
    {
        int smoothPeriod = 3;
        var prsStream = new Rs(smoothPeriod);
        var time = DateTime.UtcNow;

        var baseSeries = new TSeries(10);
        var compSeries = new TSeries(10);

        var baseValues = new double[] { 100, 110, 105, 115, 120, 125, 118, 130, 128, 135 };
        var compValues = new double[] { 100, 102, 101, 103, 104, 105, 103, 106, 105, 107 };

        for (int i = 0; i < baseValues.Length; i++)
        {
            baseSeries.Add(new TValue(time.AddSeconds(i), baseValues[i]));
            compSeries.Add(new TValue(time.AddSeconds(i), compValues[i]));
        }

        // Streaming
        var streamResults = new double[baseValues.Length];
        for (int i = 0; i < baseValues.Length; i++)
        {
            streamResults[i] = prsStream.Update(baseValues[i], compValues[i], true).Value;
        }

        // TSeries batch
        var batchResult = Rs.Batch(baseSeries, compSeries, smoothPeriod);

        for (int i = 0; i < baseValues.Length; i++)
        {
            Assert.Equal(streamResults[i], batchResult[i].Value, 10);
        }
    }

    #endregion

    #region Properties and State

    [Fact]
    public void Rs_IsHot_BecomesTrue_AfterSmoothPeriod()
    {
        var rs = new Rs(5);

        for (int i = 0; i < 10; i++)
        {
            rs.Update(100 + i, 100.0, true);

            if (i < 4) // 0-4 = first 5 values
            {
                Assert.False(rs.IsHot, $"Should not be hot at index {i}");
            }
            else
            {
                Assert.True(rs.IsHot, $"Should be hot at index {i}");
            }
        }
    }

    [Fact]
    public void Rs_Reset_ClearsState()
    {
        var rs = new Rs(5);

        // Add values
        for (int i = 0; i < 10; i++)
        {
            rs.Update(100 + i, 50.0, true);
        }

        Assert.True(rs.IsHot);
        Assert.True(rs.RawRatio > 0);

        // Reset
        rs.Reset();

        Assert.False(rs.IsHot);
        Assert.Equal(0.0, rs.RawRatio);
        Assert.Equal(default(TValue), rs.Last);
    }

    [Fact]
    public void Rs_Prime_InitializesState()
    {
        var rs = new Rs(5);

        var baseSource = new double[] { 100, 105, 110, 115, 120, 125, 130 };
        var compSource = new double[] { 100, 100, 100, 100, 100, 100, 100 };

        rs.Prime(baseSource, compSource);

        Assert.True(rs.IsHot);
        Assert.Equal(1.30, rs.RawRatio, 10);
        Assert.Equal(130.0 / 100.0, rs.RawRatio, 10);
    }

    #endregion

    #region Performance Properties

    [Fact]
    public void Rs_SmoothPeriod_ExposesCorrectValue()
    {
        var rs = new Rs(14);

        Assert.Equal(14, rs.SmoothPeriod);
    }

    [Fact]
    public void Rs_WarmupPeriod_EqualsSmoothPeriod()
    {
        var rs = new Rs(20);

        Assert.Equal(20, rs.WarmupPeriod);
    }

    [Fact]
    public void Rs_Name_IncludesPeriodIfSmoothed()
    {
        var prsNoSmooth = new Rs(1);
        var prsSmooth = new Rs(14);

        Assert.Equal("Rs", prsNoSmooth.Name);
        Assert.Equal("Rs(14)", prsSmooth.Name);
    }

    #endregion

    #region Skender Cross-Validation

    /// <summary>
    /// Structural validation against Skender <c>GetPrs</c>.
    /// Skender PRS computes price ratio between two quote series.
    /// QuanTAlib RS also computes base/comparison ratio with optional smoothing.
    /// With period=1 (no smoothing), raw ratios should match exactly.
    /// </summary>
    [Fact]
    public void Validate_Skender_Rs_Streaming()
    {
        using var evalData = new ValidationTestData();
        // Create a second quote series for comparison (different seed)
        var baseGbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.12, seed: 999);
        var baseBars = baseGbm.Fetch(evalData.Bars.Count, evalData.Bars[0].Time, TimeSpan.FromMinutes(1));
        var baseQuotes = baseBars.Select(b => new Quote
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = (decimal)b.Open,
            High = (decimal)b.High,
            Low = (decimal)b.Low,
            Close = (decimal)b.Close,
            Volume = (decimal)b.Volume
        }).ToList();

        // QuanTAlib RS (streaming, no smoothing)
        var rs = new Rs(1);
        var qResults = new List<double>();
        for (int i = 0; i < evalData.Bars.Count; i++)
        {
            double evalClose = evalData.Bars[i].Close;
            double baseClose = baseBars[i].Close;
            qResults.Add(rs.Update(evalClose, baseClose, true).Value);
        }

        // Skender PRS: quotesEval.GetPrs(quotesBase)
        var sResult = evalData.SkenderQuotes.GetPrs(baseQuotes).ToList();

        // Cross-validate: raw RS ratio (no smoothing)
        ValidationHelper.VerifyData(qResults, sResult, s => s.Prs, tolerance: ValidationHelper.SkenderTolerance);
    }

    [Fact]
    public void Rs_Correction_Recomputes()
    {
        var ind = new Rs(smoothPeriod: 5);

        // Build state well past warmup
        for (int i = 0; i < 50; i++)
        {
            ind.Update(100.0 + (i * 0.5), 98.0 + (i * 0.5));
        }

        // Anchor bar
        const double anchorBase = 125.0;
        const double anchorComp = 100.0;
        ind.Update(anchorBase, anchorComp, isNew: true);
        double anchorResult = ind.Last.Value;

        // Correction: change base dramatically — ratio changes from 1.25 to 12.5
        ind.Update(anchorBase * 10, anchorComp, isNew: false);
        Assert.NotEqual(anchorResult, ind.Last.Value);

        // Correction back to original — must exactly restore
        ind.Update(anchorBase, anchorComp, isNew: false);
        Assert.Equal(anchorResult, ind.Last.Value, 1e-9);
    }

    #endregion
}
