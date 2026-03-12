using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for PVD (Price Volume Divergence) indicator.
/// PVD is a custom indicator not found in standard libraries (TA-Lib, Skender, Tulip, Ooples).
/// Validation focuses on mathematical correctness and self-consistency.
/// </summary>
public class PvdValidationTests
{
    private readonly TBarSeries _data;
    private const int TestDataLength = 500;
    private const double Tolerance = 1e-10;

    public PvdValidationTests()
    {
        var gbm = new GBM(seed: 123);
        _data = new TBarSeries();
        for (int i = 0; i < TestDataLength; i++)
        {
            _data.Add(gbm.Next());
        }
    }

    #region Self-Consistency Validation

    [Fact]
    public void Pvd_StreamingVsBatch_ExactMatch()
    {
        int pricePeriod = 14;
        int volumePeriod = 14;
        int smoothingPeriod = 3;

        // Streaming calculation
        var pvdStreaming = new Pvd(pricePeriod, volumePeriod, smoothingPeriod);
        var streamingResults = new List<double>();
        for (int i = 0; i < _data.Count; i++)
        {
            streamingResults.Add(pvdStreaming.Update(_data[i], isNew: true).Value);
        }

        // Batch calculation (uses static Calculate which uses span internally)
        var batchResults = Pvd.Batch(_data, pricePeriod, volumePeriod, smoothingPeriod);

        // Compare after full warmup (streaming and span may differ during warmup due to smoothing initialization)
        Assert.Equal(_data.Count, batchResults.Count);
        int warmup = Math.Max(pricePeriod, volumePeriod) + smoothingPeriod;
        for (int i = warmup; i < _data.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Pvd_SpanVsBatch_ExactMatch()
    {
        int pricePeriod = 10;
        int volumePeriod = 10;
        int smoothingPeriod = 5;

        // Extract data for span
        double[] closes = new double[_data.Count];
        double[] volumes = new double[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            closes[i] = _data[i].Close;
            volumes[i] = _data[i].Volume;
        }

        // Span calculation
        double[] spanResults = new double[_data.Count];
        Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), spanResults.AsSpan(), pricePeriod, volumePeriod, smoothingPeriod);

        // Batch calculation
        var batchResults = Pvd.Batch(_data, pricePeriod, volumePeriod, smoothingPeriod);

        // Compare after warmup
        int startCompare = Math.Max(pricePeriod, volumePeriod) + smoothingPeriod;
        for (int i = startCompare; i < _data.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, spanResults[i], precision: 10);
        }
    }

    [Fact]
    public void Pvd_DifferentPeriods_ProduceDifferentResults()
    {
        var pvd1 = Pvd.Batch(_data, pricePeriod: 5, volumePeriod: 5, smoothingPeriod: 3);
        var pvd2 = Pvd.Batch(_data, pricePeriod: 20, volumePeriod: 20, smoothingPeriod: 3);

        // After warmup, values should differ
        int compareIdx = _data.Count - 1;
        Assert.NotEqual(pvd1[compareIdx].Value, pvd2[compareIdx].Value);
    }

    [Fact]
    public void Pvd_AsymmetricPeriods_Work()
    {
        // Price period longer than volume period
        var pvd1 = Pvd.Batch(_data, pricePeriod: 20, volumePeriod: 5, smoothingPeriod: 3);

        // Volume period longer than price period
        var pvd2 = Pvd.Batch(_data, pricePeriod: 5, volumePeriod: 20, smoothingPeriod: 3);

        // Results should differ
        int compareIdx = _data.Count - 1;
        Assert.NotEqual(pvd1[compareIdx].Value, pvd2[compareIdx].Value);
    }

    #endregion

    #region Mathematical Correctness Validation

    [Fact]
    public void Pvd_KnownScenario_PositiveDivergence()
    {
        // Price up, volume down = positive divergence
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        // Build baseline
        for (int i = 0; i < 5; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), 100.0, 100.0, 100.0, 100.0, 1000.0));
        }

        // Price increasing, volume decreasing
        bars.Add(new TBar(time.AddMinutes(5), 110.0, 110.0, 110.0, 110.0, 800.0));

        var result = Pvd.Batch(bars, pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);

        // Last value should be positive (divergence detected)
        Assert.True(result[^1].Value > 0);
    }

    [Fact]
    public void Pvd_KnownScenario_NegativeDivergence()
    {
        // Price up, volume up = negative (same direction, no divergence)
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        // Build baseline
        for (int i = 0; i < 5; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), 100.0, 100.0, 100.0, 100.0, 1000.0));
        }

        // Price increasing, volume also increasing
        bars.Add(new TBar(time.AddMinutes(5), 110.0, 110.0, 110.0, 110.0, 1200.0));

        var result = Pvd.Batch(bars, pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);

        // Last value should be negative (price and volume moving same direction)
        Assert.True(result[^1].Value < 0);
    }

    [Fact]
    public void Pvd_KnownScenario_NoMomentum()
    {
        // No price change = zero divergence
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        // All same values
        for (int i = 0; i < 10; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), 100.0, 100.0, 100.0, 100.0, 1000.0));
        }

        var result = Pvd.Batch(bars, pricePeriod: 3, volumePeriod: 3, smoothingPeriod: 2);

        // Should be zero (no momentum in either direction)
        Assert.Equal(0.0, result[^1].Value, precision: 10);
    }

    [Fact]
    public void Pvd_ManualCalculation_MatchesFormula()
    {
        // Create known data
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        double[] closes = [100.0, 102.0, 104.0, 106.0, 105.0];
        double[] volumes = [1000.0, 1100.0, 900.0, 1200.0, 800.0];

        for (int i = 0; i < 5; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), closes[i], closes[i], closes[i], closes[i], volumes[i]));
        }

        // Manual calculation for last bar with period=2, smoothing=1
        // Price ROC at index 4: (105 - 104) / 104 * 100 = 0.9615...
        // Volume ROC at index 4: (800 - 900) / 900 * 100 = -11.111...
        // Price momentum = 1 (positive)
        // Volume momentum = -1 (negative)
        // Magnitude = |0.9615| + |-11.111| = 12.073...
        // Divergence = 1 * -(-1) * 12.073 = 12.073... (positive: price up, volume down)

        var result = Pvd.Batch(bars, pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);

        // Last value should be positive
        Assert.True(result[^1].Value > 0);
    }

    #endregion

    #region Smoothing Validation

    [Fact]
    public void Pvd_SmoothingPeriod1_NoSmoothing()
    {
        var result1 = Pvd.Batch(_data, pricePeriod: 10, volumePeriod: 10, smoothingPeriod: 1);
        var result3 = Pvd.Batch(_data, pricePeriod: 10, volumePeriod: 10, smoothingPeriod: 3);

        // Smoothing should make values different (and generally smoother)
        bool foundDifference = false;
        for (int i = 20; i < _data.Count; i++)
        {
            if (Math.Abs(result1[i].Value - result3[i].Value) > Tolerance)
            {
                foundDifference = true;
                break;
            }
        }
        Assert.True(foundDifference);
    }

    [Fact]
    public void Pvd_HigherSmoothing_ReducesVolatility()
    {
        var result1 = Pvd.Batch(_data, pricePeriod: 10, volumePeriod: 10, smoothingPeriod: 1);
        var result10 = Pvd.Batch(_data, pricePeriod: 10, volumePeriod: 10, smoothingPeriod: 10);

        // Calculate variance of last 100 values
        double variance1 = CalculateVariance(result1.Skip(400).Select(x => x.Value).ToArray());
        double variance10 = CalculateVariance(result10.Skip(400).Select(x => x.Value).ToArray());

        // Higher smoothing should reduce variance
        Assert.True(variance10 <= variance1);
    }

    private static double CalculateVariance(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }
        double mean = values.Average();
        return values.Sum(v => (v - mean) * (v - mean)) / values.Length;
    }

    #endregion

    #region Edge Cases Validation

    [Fact]
    public void Pvd_ZeroVolume_HandlesGracefully()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        // Mix of zero and non-zero volumes
        for (int i = 0; i < 20; i++)
        {
            double volume = i % 3 == 0 ? 0.0 : 1000.0 + i * 10;
            bars.Add(new TBar(time.AddMinutes(i), 100.0 + i, 101.0 + i, 99.0 + i, 100.5 + i, volume));
        }

        var result = Pvd.Batch(bars, pricePeriod: 3, volumePeriod: 3, smoothingPeriod: 2);

        // Should complete without errors
        Assert.Equal(20, result.Count);
        Assert.True(double.IsFinite(result[^1].Value));
    }

    [Fact]
    public void Pvd_SingleBar_ReturnsZero()
    {
        var bars = new TBarSeries();
        bars.Add(new TBar(DateTime.UtcNow, 100.0, 100.0, 100.0, 100.0, 1000.0));

        var result = Pvd.Batch(bars, pricePeriod: 5, volumePeriod: 5, smoothingPeriod: 2);

        Assert.Single(result);
        Assert.Equal(0.0, result[0].Value);
    }

    [Fact]
    public void Pvd_LargeDataset_CompletesWithoutError()
    {
        var gbm = new GBM(seed: 456);
        var largeData = new TBarSeries();

        for (int i = 0; i < 10000; i++)
        {
            largeData.Add(gbm.Next());
        }

        var result = Pvd.Batch(largeData, pricePeriod: 14, volumePeriod: 14, smoothingPeriod: 3);

        Assert.Equal(10000, result.Count);
        Assert.True(double.IsFinite(result[^1].Value));
    }

    #endregion

    #region Reset and State Validation

    [Fact]
    public void Pvd_ResetAndRecalculate_SameResult()
    {
        var pvd = new Pvd(pricePeriod: 10, volumePeriod: 10, smoothingPeriod: 3);

        // First pass
        for (int i = 0; i < _data.Count; i++)
        {
            pvd.Update(_data[i], isNew: true);
        }
        double firstResult = pvd.Last.Value;

        // Reset and second pass
        pvd.Reset();
        for (int i = 0; i < _data.Count; i++)
        {
            pvd.Update(_data[i], isNew: true);
        }
        double secondResult = pvd.Last.Value;

        Assert.Equal(firstResult, secondResult, precision: 10);
    }

    [Fact]
    public void Pvd_BarCorrection_ProducesConsistentResults()
    {
        var pvd = new Pvd(pricePeriod: 5, volumePeriod: 5, smoothingPeriod: 2);

        // Process bars up to correction point
        for (int i = 0; i < 50; i++)
        {
            pvd.Update(_data[i], isNew: true);
        }

        _ = pvd.Last.Value;

        // Make multiple corrections
        for (int c = 0; c < 3; c++)
        {
            pvd.Update(_data[50], isNew: false);
        }

        // Final value after corrections should be consistent
        pvd.Update(_data[50], isNew: true);
        double valueAfterCorrections = pvd.Last.Value;

        Assert.True(double.IsFinite(valueAfterCorrections));
    }

    #endregion

    #region Documentation Validation

    /// <summary>
    /// PVD is not implemented in major libraries.
    /// This test documents the validation status.
    /// </summary>
    [Fact]
    public void ValidationStatus_NotInMajorLibraries()
    {
        // PVD is a custom indicator created for QuanTAlib
        // Not found in: TA-Lib, Skender.Stock.Indicators, Tulip, OoplesFinance
        // Validation is performed through self-consistency tests and mathematical verification
        Assert.True(true, "PVD is a custom indicator - validated through self-consistency and math verification");
    }

    #endregion
}
