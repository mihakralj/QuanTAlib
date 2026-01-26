namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for HANMA (Hanning Moving Average).
/// Note: HANMA is not available in most external libraries (TA-Lib, Skender, etc.),
/// so we validate against our own PineScript reference implementation and mathematical properties.
/// </summary>
public class HanmaValidationTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Hanma_MatchesPineScriptReference()
    {
        // Test that our implementation matches the PineScript reference
        // hanma.pine: w[i] = 0.5 * (1.0 - cos(2π*i/(p-1)))
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        int period = 10;
        var hanma = new Hanma(period);
        var results = hanma.Update(series);

        // Verify against manual Hanning window calculation
        double[] weights = new double[period];
        double twoPiOverPm1 = 2.0 * Math.PI / (period - 1);
        double weightSum = 0;
        for (int i = 0; i < period; i++)
        {
            weights[i] = 0.5 * (1.0 - Math.Cos(twoPiOverPm1 * i));
            weightSum += weights[i];
        }

        // Verify last value
        double manualSum = 0;
        for (int i = 0; i < period; i++)
        {
            manualSum += series[series.Count - period + i].Value * weights[i];
        }
        double expected = manualSum / weightSum;

        Assert.Equal(expected, results.Last.Value, Tolerance);
    }

    [Fact]
    public void Hanma_HanningWindowProperties()
    {
        // Verify key properties of Hanning window:
        // 1. Symmetric around center
        // 2. Edge values are exactly 0
        // 3. Center value is maximum (1.0)
        int period = 11; // Odd for exact center
        double[] weights = new double[period];
        double twoPiOverPm1 = 2.0 * Math.PI / (period - 1);

        for (int i = 0; i < period; i++)
        {
            weights[i] = 0.5 * (1.0 - Math.Cos(twoPiOverPm1 * i));
        }

        // 1. Edge values should be 0
        Assert.Equal(0.0, weights[0], Tolerance);
        Assert.Equal(0.0, weights[period - 1], Tolerance);

        // 2. Center value should be 1.0
        Assert.Equal(1.0, weights[period / 2], Tolerance);

        // 3. Symmetric: w[i] = w[period-1-i]
        for (int i = 0; i < period / 2; i++)
        {
            Assert.Equal(weights[i], weights[period - 1 - i], Tolerance);
        }
    }

    [Fact]
    public void Hanma_ConsistentAcrossModes()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        int period = 10;

        // Batch
        var batchResults = Hanma.Batch(series, period);

        // Streaming
        var streaming = new Hanma(period);
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        // Span
        double[] input = series.Values.ToArray();
        double[] spanOutput = new double[input.Length];
        Hanma.Calculate(input.AsSpan(), spanOutput.AsSpan(), period);

        // All should match
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, streamingResults[i].Value, Tolerance);
            Assert.Equal(batchResults[i].Value, spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Hanma_DifferentPeriodsProduceDifferentResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var hanma5 = new Hanma(5);
        var hanma10 = new Hanma(10);
        var hanma20 = new Hanma(20);

        var results5 = hanma5.Update(series);
        var results10 = hanma10.Update(series);
        var results20 = hanma20.Update(series);

        // Different periods should produce different results
        Assert.NotEqual(results5.Last.Value, results10.Last.Value);
        Assert.NotEqual(results10.Last.Value, results20.Last.Value);
    }

    [Fact]
    public void Hanma_ConstantInput_ReturnsConstant()
    {
        var hanma = new Hanma(10);
        const double constantValue = 100.0;

        for (int i = 0; i < 20; i++)
        {
            var result = hanma.Update(new TValue(DateTime.UtcNow, constantValue));
            Assert.Equal(constantValue, result.Value, Tolerance);
        }
    }

    [Fact]
    public void Hanma_VsHamma_DifferentResults()
    {
        // Hanning (0.5*(1-cos)) vs Hamming (0.54-0.46*cos) should differ
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        var hanma = new Hanma(10);
        var hamma = new Hamma(10);

        var hanmaResults = hanma.Update(series);
        var hammaResults = hamma.Update(series);

        // Should be different (different window coefficients)
        Assert.NotEqual(hanmaResults.Last.Value, hammaResults.Last.Value);
    }
}
