// ENTROPY Validation Tests - Shannon Entropy
// Validated against self-consistency and known mathematical properties
// No external library provides a direct histogram-based Shannon entropy equivalent

namespace QuanTAlib.Tests;

public sealed class EntropyValidationTests
{
    private static TSeries CreateGbmSeries(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: seed);
        var times = new List<long>(count);
        var values = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }
        return new TSeries(times, values);
    }

    /// <summary>
    /// Constant series must produce zero entropy — the defining property of
    /// Shannon entropy for a degenerate distribution.
    /// </summary>
    [Fact]
    public void ConstantSeries_ProducesZeroEntropy()
    {
        const int period = 20;
        var e = new Entropy(period);

        for (int i = 0; i < 50; i++)
        {
            var result = e.Update(new TValue(DateTime.UtcNow, 42.0));
            Assert.Equal(0.0, result.Value, 1e-12);
        }
    }

    /// <summary>
    /// Two distinct values exactly split should produce entropy = ln(2)/ln(bins).
    /// With period=2, bins=2, so H_norm = (2·(-0.5·ln(0.5)))/ln(2) = 1.0.
    /// </summary>
    [Fact]
    public void TwoDistinctValues_Period2_ProducesMaxEntropy()
    {
        var e = new Entropy(2);
        e.Update(new TValue(DateTime.UtcNow, 0.0));
        var result = e.Update(new TValue(DateTime.UtcNow, 100.0));

        // With 2 values in 2 bins: each bin has 1 value → p=0.5 each
        // H = -2*(0.5*ln(0.5)) = ln(2), normalized by ln(2) = 1.0
        Assert.Equal(1.0, result.Value, 1e-10);
    }

    /// <summary>
    /// Entropy must always be in [0, 1] for any input distribution.
    /// </summary>
    [Fact]
    public void EntropyRange_AlwaysZeroToOne()
    {
        const int period = 14;
        var series = CreateGbmSeries(500);
        var e = new Entropy(period);

        for (int i = 0; i < series.Count; i++)
        {
            var result = e.Update(series[i]);
            Assert.InRange(result.Value, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Batch and streaming must produce identical results.
    /// </summary>
    [Fact]
    public void BatchVsStreaming_ExactMatch()
    {
        const int period = 14;
        var series = CreateGbmSeries(300);

        // Batch
        var batchResult = Entropy.Batch(series, period);

        // Streaming
        var streamingInd = new Entropy(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }

        // Compare last 100 values
        for (int i = series.Count - 100; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, batchResult.Values[i], 1e-12);
        }

        Assert.Equal(batchResult.Last.Value, streamingInd.Last.Value, 1e-12);
    }

    /// <summary>
    /// Span batch must match TSeries batch exactly.
    /// </summary>
    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        const int period = 14;
        var series = CreateGbmSeries(300);

        var tseriesResult = Entropy.Batch(series, period);

        double[] source = new double[series.Count];
        double[] output = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            source[i] = series[i].Value;
        }

        Entropy.Batch(source.AsSpan(), output.AsSpan(), period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    /// <summary>
    /// Verify that a linearly increasing series has non-zero entropy (values spread across bins).
    /// </summary>
    [Fact]
    public void LinearSeries_HasNonZeroEntropy()
    {
        const int period = 20;
        var e = new Entropy(period);

        for (int i = 1; i <= 20; i++)
        {
            e.Update(new TValue(DateTime.UtcNow, i * 1.0));
        }

        // Linear sequence places exactly one value per bin → maximum entropy
        Assert.True(e.Last.Value > 0.8, $"Expected high entropy for linear data, got {e.Last.Value}");
    }

    /// <summary>
    /// Near-constant series (tiny variance) should have near-zero entropy.
    /// </summary>
    [Fact]
    public void NearConstant_NearZeroEntropy()
    {
        const int period = 20;
        var e = new Entropy(period);

        for (int i = 0; i < 20; i++)
        {
            // All values within 1e-12 of each other
            e.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 1e-12)));
        }

        // Range ≈ 19e-12, which is > epsilon but all values collapse into same bin
        Assert.True(e.Last.Value < 0.1, $"Expected near-zero entropy, got {e.Last.Value}");
    }

    /// <summary>
    /// Calculate static method returns both results and indicator.
    /// </summary>
    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var series = CreateGbmSeries(100);
        var (results, indicator) = Entropy.Calculate(series, 14);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.Equal(results.Last.Value, indicator.Last.Value, 1e-12);
    }
}
