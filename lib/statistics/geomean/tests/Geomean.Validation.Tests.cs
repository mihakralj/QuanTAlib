namespace QuanTAlib.Tests;

/// <summary>
/// Geomean Validation Tests - Self-consistency validation.
/// No external TA library implements rolling geometric mean, so we validate
/// against mathematical properties and internal consistency.
/// </summary>
public sealed class GeomeanValidationTests
{
    private static TSeries CreateGbmSeries(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: seed);
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

    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        // GM of identical values = that value
        var g = new Geomean(20);
        for (int i = 0; i < 50; i++)
        {
            g.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, g.Last.Value, 10);
    }

    [Fact]
    public void GeomeanLeqArithmeticMean()
    {
        // AM-GM inequality: GM ≤ AM for all positive values
        var series = CreateGbmSeries();
        int period = 20;
        var g = new Geomean(period);
        var sma = new Sma(period);

        for (int i = 0; i < series.Count; i++)
        {
            g.Update(series[i]);
            sma.Update(series[i]);
            if (g.IsHot)
            {
                Assert.True(g.Last.Value <= sma.Last.Value + 1e-10,
                    $"AM-GM violated at bar {i}: GM={g.Last.Value}, AM={sma.Last.Value}");
            }
        }
    }

    [Fact]
    public void BatchAndStreaming_Match()
    {
        var series = CreateGbmSeries();
        int period = 14;

        // Streaming
        var gStream = new Geomean(period);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            gStream.Update(series[i]);
            streamResults[i] = gStream.Last.Value;
        }

        // Batch
        var batchResult = Geomean.Batch(series, period);
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResult[i].Value, 8);
        }
    }

    [Fact]
    public void OutputIsPositive()
    {
        var series = CreateGbmSeries();
        var g = new Geomean(14);
        for (int i = 0; i < series.Count; i++)
        {
            g.Update(series[i]);
            Assert.True(g.Last.Value > 0, $"Output not positive at bar {i}: {g.Last.Value}");
        }
    }

    [Fact]
    public void Calculate_ReturnsCorrectResults()
    {
        var series = CreateGbmSeries(100);
        var (results, indicator) = Geomean.Calculate(series, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(100, results.Count);
        Assert.True(double.IsFinite(results[^1].Value));
    }

    [Fact]
    public void NearConstant_NearConstant()
    {
        // Values very close together → GM ≈ AM ≈ the value
        var g = new Geomean(10);
        for (int i = 0; i < 20; i++)
        {
            g.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 0.001)));
        }
        Assert.True(Math.Abs(g.Last.Value - 100.01) < 0.1,
            $"Expected near 100.01, got {g.Last.Value}");
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var series = CreateGbmSeries(200);
        int period = 14;

        var batchResult = Geomean.Batch(series, period);

        var src = series.Values;
        Span<double> output = new double[series.Count];
        Geomean.Batch(src, output, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 8);
        }
    }

    [Fact]
    public void MultiplicativeProperty()
    {
        // If all values are scaled by c, GM scales by c
        // GM(c*x1, c*x2, ...) = c * GM(x1, x2, ...)
        double c = 3.0;
        int period = 10;
        var g1 = new Geomean(period);
        var g2 = new Geomean(period);

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            g1.Update(tv);
            g2.Update(new TValue(bar.Time, bar.Close * c));
        }

        Assert.Equal(g1.Last.Value * c, g2.Last.Value, 8);
    }
}
