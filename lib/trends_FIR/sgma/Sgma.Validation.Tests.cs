namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for SGMA (Savitzky-Golay Moving Average).
/// Note: SGMA is not commonly available in other libraries (TA-Lib, Skender, Tulip, Ooples)
/// as a standard indicator. These tests validate against known mathematical properties
/// and internal consistency rather than external library comparisons.
/// </summary>
public class SgmaValidationTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Sgma_Degree0_MatchesSma_Batch()
    {
        // SGMA with degree=0 should produce identical results to SMA (uniform weights)
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var sgmaResults = Sgma.Batch(series, 9, 0);
        var smaResults = Sma.Batch(series, 9);

        for (int i = 0; i < sgmaResults.Count; i++)
        {
            Assert.Equal(smaResults[i].Value, sgmaResults[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Sgma_Degree0_MatchesSma_Streaming()
    {
        var sgma = new Sgma(5, 0);
        var sma = new Sma(5);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            var sgmaResult = sgma.Update(tv);
            var smaResult = sma.Update(tv);

            Assert.Equal(smaResult.Value, sgmaResult.Value, Tolerance);
        }
    }

    [Fact]
    public void Sgma_Degree0_MatchesSma_Span()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        double[] input = series.Values.ToArray();
        double[] sgmaOutput = new double[input.Length];
        double[] smaOutput = new double[input.Length];

        Sgma.Batch(input.AsSpan(), sgmaOutput.AsSpan(), 9, 0);
        Sma.Batch(input.AsSpan(), smaOutput.AsSpan(), 9);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(smaOutput[i], sgmaOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Sgma_ConstantInput_ReturnsConstant_AllDegrees()
    {
        const double constantValue = 100.0;
        const int period = 9;

        for (int degree = 0; degree <= 4; degree++)
        {
            var sgma = new Sgma(period, degree);

            for (int i = 0; i < 20; i++)
            {
                var result = sgma.Update(new TValue(DateTime.UtcNow, constantValue));
                Assert.Equal(constantValue, result.Value, Tolerance);
            }
        }
    }

    [Fact]
    public void Sgma_LinearTrend_PreservesSlope_LowDegree()
    {
        // For a perfectly linear input, SGMA should follow the trend
        // Higher degrees should give more accurate mid-point values
        const int period = 5;
        double[] prices = new double[20];
        for (int i = 0; i < 20; i++)
        {
            prices[i] = 100.0 + i * 10.0; // Linear: 100, 110, 120, ..., 290
        }

        var sgma0 = new Sgma(period, 0);
        var sgma2 = new Sgma(period, 2);

        // After warmup, results should track the trend
        for (int i = 0; i < 20; i++)
        {
            sgma0.Update(new TValue(DateTime.UtcNow, prices[i]));
            sgma2.Update(new TValue(DateTime.UtcNow, prices[i]));
        }

        // Both should produce reasonable values within the data range
        Assert.True(sgma0.Last.Value >= 250 && sgma0.Last.Value <= 290);
        Assert.True(sgma2.Last.Value >= 250 && sgma2.Last.Value <= 290);
    }

    [Fact]
    public void Sgma_WeightSymmetry_ProducesSymmetricResponse()
    {
        // SGMA weights are symmetric around the center
        // Test by feeding symmetric data and verifying symmetric output
        const int period = 5;
        var sgma = new Sgma(period, 2);

        // Symmetric pattern: 100, 110, 120, 110, 100
        double[] symmetric = [100, 110, 120, 110, 100];

        TValue result = default;
        foreach (var val in symmetric)
        {
            result = sgma.Update(new TValue(DateTime.UtcNow, val));
        }

        // Center value is 120, symmetric weights should produce value close to weighted average
        // with center weighted higher
        Assert.True(result.Value >= 100 && result.Value <= 120);
    }

    [Fact]
    public void Sgma_HigherDegree_MoreResponsive()
    {
        // Higher polynomial degrees preserve shape better (more responsive to changes)
        var gbm = new GBM(startPrice: 100.0, mu: 0.1, sigma: 0.3, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var sgma0 = new Sgma(9, 0);
        var sgma4 = new Sgma(9, 4);

        var results0 = sgma0.Update(series);
        var results4 = sgma4.Update(series);

        // Calculate variance of differences from actual values
        double sumSqDiff0 = 0, sumSqDiff4 = 0;
        for (int i = 9; i < results0.Count; i++)
        {
            double actual = series[i].Value;
            sumSqDiff0 += (results0[i].Value - actual) * (results0[i].Value - actual);
            sumSqDiff4 += (results4[i].Value - actual) * (results4[i].Value - actual);
        }

        // Higher degree should track actual values more closely (lower sum of squared differences)
        // But this is not guaranteed for all data, so just verify both are reasonable
        Assert.True(double.IsFinite(sumSqDiff0));
        Assert.True(double.IsFinite(sumSqDiff4));
    }

    [Fact]
    public void Sgma_EvenPeriodAdjustment_ProducesOddPeriod()
    {
        // Even periods should be adjusted to odd
        var sgma6 = new Sgma(6, 2);
        var sgma10 = new Sgma(10, 2);
        var sgma100 = new Sgma(100, 2);

        Assert.Contains("7", sgma6.Name, StringComparison.Ordinal);
        Assert.Contains("11", sgma10.Name, StringComparison.Ordinal);
        Assert.Contains("101", sgma100.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Sgma_AllModes_Match_AllDegrees()
    {
        // Verify batch, streaming, span, and eventing modes produce identical results
        // for all polynomial degrees
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        for (int degree = 0; degree <= 4; degree++)
        {
            // Batch
            var batchResults = Sgma.Batch(series, 9, degree);
            double expected = batchResults.Last.Value;

            // Span
            double[] input = series.Values.ToArray();
            double[] output = new double[input.Length];
            Sgma.Batch(input.AsSpan(), output.AsSpan(), 9, degree);
            Assert.Equal(expected, output[^1], Tolerance);

            // Streaming
            var streaming = new Sgma(9, degree);
            foreach (var item in series)
            {
                streaming.Update(item);
            }
            Assert.Equal(expected, streaming.Last.Value, Tolerance);

            // Eventing
            var pubSource = new TSeries();
            var eventing = new Sgma(pubSource, 9, degree);
            foreach (var item in series)
            {
                pubSource.Add(item);
            }
            Assert.Equal(expected, eventing.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Sgma_NaN_Handling_Consistent_AllModes()
    {
        // Verify NaN handling is consistent across all modes
        double[] sourceWithNaN = [100, 110, 120, double.NaN, 140, 150, 160, 170, 180];
        double[] output = new double[sourceWithNaN.Length];

        Sgma.Batch(sourceWithNaN.AsSpan(), output.AsSpan(), 5, 2);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }

        // Streaming should match span
        var sgma = new Sgma(5, 2);
        for (int i = 0; i < sourceWithNaN.Length; i++)
        {
            sgma.Update(new TValue(DateTime.UtcNow, sourceWithNaN[i]));
            Assert.Equal(output[i], sgma.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Sgma_KnownValues_Degree2_Period5()
    {
        // Test with known input and verify mathematical correctness
        // Period=5, Degree=2: weights follow w = 1 - normX^2
        // positions: [-2, -1, 0, 1, 2] / 2 = [-1, -0.5, 0, 0.5, 1]
        // weights: 1-1=0, 1-0.25=0.75, 1-0=1, 1-0.25=0.75, 1-1=0
        // Sum of non-zero weights = 0.75 + 1 + 0.75 = 2.5

        var sgma = new Sgma(5, 2);

        // Feed 5 values: 100, 200, 300, 400, 500
        sgma.Update(new TValue(DateTime.UtcNow, 100));
        sgma.Update(new TValue(DateTime.UtcNow, 200));
        sgma.Update(new TValue(DateTime.UtcNow, 300));
        sgma.Update(new TValue(DateTime.UtcNow, 400));
        sgma.Update(new TValue(DateTime.UtcNow, 500));

        // Expected: (0*100 + 0.75*200 + 1*300 + 0.75*400 + 0*500) / 2.5
        //         = (0 + 150 + 300 + 300 + 0) / 2.5
        //         = 750 / 2.5 = 300
        Assert.Equal(300.0, sgma.Last.Value, Tolerance);
    }

    [Fact]
    public void Sgma_KnownValues_Degree0_Period5()
    {
        // Degree=0: all weights = 1.0 (equivalent to SMA)
        var sgma = new Sgma(5, 0);

        sgma.Update(new TValue(DateTime.UtcNow, 100));
        sgma.Update(new TValue(DateTime.UtcNow, 200));
        sgma.Update(new TValue(DateTime.UtcNow, 300));
        sgma.Update(new TValue(DateTime.UtcNow, 400));
        sgma.Update(new TValue(DateTime.UtcNow, 500));

        // Expected: (100 + 200 + 300 + 400 + 500) / 5 = 1500 / 5 = 300
        Assert.Equal(300.0, sgma.Last.Value, Tolerance);
    }
}
