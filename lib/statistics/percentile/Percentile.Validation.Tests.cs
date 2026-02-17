namespace QuanTAlib.Validation;

/// <summary>
/// Percentile validation tests — self-consistency and cross-indicator validation.
/// Percentile(p=50) must match Median indicator exactly.
/// </summary>
public sealed class PercentileValidationTests
{
    [Fact]
    public void Percentile50_Matches_MedianIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(gbm.Next());
        }
        int period = 14;

        // Percentile at 50%
        var percentile = new Percentile(period, 50.0);
        var pResults = new double[source.Count];

        // Median
        var median = new Median(period);
        var mResults = new double[source.Count];

        for (int i = 0; i < source.Count; i++)
        {
            var tv = new TValue(source.Times[i], source.Values[i]);
            pResults[i] = percentile.Update(tv).Value;
            mResults[i] = median.Update(tv).Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(mResults[i], pResults[i], precision: 10);
        }
    }

    [Fact]
    public void Percentile_BatchAndStreaming_Match()
    {
        double[] data = [10, 20, 15, 30, 25, 40, 35, 50, 45, 60, 55, 70, 65, 80, 75];
        int period = 5;
        double percent = 25.0;

        // Streaming
        var p = new Percentile(period, percent);
        var streamingResults = new double[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            streamingResults[i] = p.Update(new TValue(DateTime.UtcNow, data[i])).Value;
        }

        // Batch via spans
        var spanOutput = new double[data.Length];
        Percentile.Batch(data.AsSpan(), spanOutput.AsSpan(), period, percent);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(streamingResults[i], spanOutput[i], precision: 10);
        }
    }

    [Fact]
    public void Percentile_KnownValues()
    {
        // {10, 20, 30, 40, 50} sorted, p=25 → rank = 0.25*4 = 1.0 → sorted[1] = 20
        var p = new Percentile(5, 25.0);
        p.Update(new TValue(DateTime.UtcNow, 10));
        p.Update(new TValue(DateTime.UtcNow, 20));
        p.Update(new TValue(DateTime.UtcNow, 30));
        p.Update(new TValue(DateTime.UtcNow, 40));
        var result = p.Update(new TValue(DateTime.UtcNow, 50));

        Assert.Equal(20.0, result.Value);
    }

    [Fact]
    public void Percentile_BoundaryValues()
    {
        // p=0 → minimum, p=100 → maximum
        var p0 = new Percentile(5, 0.0);
        var p100 = new Percentile(5, 100.0);

        double[] data = { 30, 10, 50, 20, 40 };
        for (int i = 0; i < data.Length; i++)
        {
            var tv = new TValue(DateTime.UtcNow, data[i]);
            p0.Update(tv);
            p100.Update(tv);
        }

        Assert.Equal(10.0, p0.Last.Value);
        Assert.Equal(50.0, p100.Last.Value);
    }
}
