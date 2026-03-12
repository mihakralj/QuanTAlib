namespace QuanTAlib.Validation;

/// <summary>
/// Quantile validation tests — cross-indicator validation against Percentile and Median.
/// Quantile(q) must equal Percentile(q*100) for all q ∈ [0, 1].
/// </summary>
public sealed class QuantileValidationTests
{
    [Fact]
    public void Quantile50_Matches_MedianIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(gbm.Next());
        }
        int period = 14;

        // Quantile at q=0.5
        var quantile = new Quantile(period, 0.5);
        var qResults = new double[source.Count];

        // Median
        var median = new Median(period);
        var mResults = new double[source.Count];

        for (int i = 0; i < source.Count; i++)
        {
            var tv = new TValue(source.Times[i], source.Values[i]);
            qResults[i] = quantile.Update(tv).Value;
            mResults[i] = median.Update(tv).Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(mResults[i], qResults[i], precision: 10);
        }
    }

    [Fact]
    public void Quantile_Matches_Percentile()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 456);
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(gbm.Next());
        }
        int period = 14;

        // Quantile at q=0.25
        var quantile = new Quantile(period, 0.25);
        var qResults = new double[source.Count];

        // Percentile at p=25
        var percentile = new Percentile(period, 25.0);
        var pResults = new double[source.Count];

        for (int i = 0; i < source.Count; i++)
        {
            var tv = new TValue(source.Times[i], source.Values[i]);
            qResults[i] = quantile.Update(tv).Value;
            pResults[i] = percentile.Update(tv).Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(pResults[i], qResults[i], precision: 10);
        }
    }

    [Fact]
    public void Quantile_BatchAndStreaming_Match()
    {
        double[] data = [10, 20, 15, 30, 25, 40, 35, 50, 45, 60, 55, 70, 65, 80, 75];
        int period = 5;
        double quantileLevel = 0.25;

        // Streaming
        var q = new Quantile(period, quantileLevel);
        var streamingResults = new double[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            streamingResults[i] = q.Update(new TValue(DateTime.UtcNow, data[i])).Value;
        }

        // Batch via spans
        var spanOutput = new double[data.Length];
        Quantile.Batch(data.AsSpan(), spanOutput.AsSpan(), period, quantileLevel);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanOutput[i], precision: 10);
        }
    }

    [Fact]
    public void Quantile_KnownValues()
    {
        // {10, 20, 30, 40, 50} sorted, q=0.25 → rank = 0.25*4 = 1.0 → sorted[1] = 20
        var q = new Quantile(5, 0.25);
        q.Update(new TValue(DateTime.UtcNow, 10));
        q.Update(new TValue(DateTime.UtcNow, 20));
        q.Update(new TValue(DateTime.UtcNow, 30));
        q.Update(new TValue(DateTime.UtcNow, 40));
        var result = q.Update(new TValue(DateTime.UtcNow, 50));

        Assert.Equal(20.0, result.Value);
    }

    [Fact]
    public void Quantile_BoundaryValues()
    {
        // q=0 → minimum, q=1 → maximum
        var q0 = new Quantile(5, 0.0);
        var q1 = new Quantile(5, 1.0);

        double[] data = { 30, 10, 50, 20, 40 };
        for (int i = 0; i < data.Length; i++)
        {
            var tv = new TValue(DateTime.UtcNow, data[i]);
            q0.Update(tv);
            q1.Update(tv);
        }

        Assert.Equal(10.0, q0.Last.Value);
        Assert.Equal(50.0, q1.Last.Value);
    }
}
