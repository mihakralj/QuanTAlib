namespace QuanTAlib.Tests;

public class GdemaValidationTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < count; i++)
        {
            series.Add(gbm.Next());
        }
        return series;
    }

    [Fact]
    public void Span_And_Streaming_Match()
    {
        const int period = 14;
        const double vfactor = 1.5;
        var source = MakeSeries(500);

        var streaming = new Gdema(period, vfactor);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        double[] srcArr = source.Values.ToArray();
        double[] spanResults = new double[srcArr.Length];
        Gdema.Batch(srcArr.AsSpan(), spanResults.AsSpan(), period, vfactor);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-9);
        }
    }

    [Fact]
    public void Batch_And_Streaming_Match()
    {
        const int period = 10;
        const double vfactor = 1.0;
        var source = MakeSeries(300);

        var streaming = new Gdema(period, vfactor);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        var batchResults = Gdema.Batch(source, period, vfactor);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-9);
        }
    }

    [Theory]
    [InlineData(1, 0.0)]
    [InlineData(3, 0.5)]
    [InlineData(9, 1.0)]
    [InlineData(20, 1.5)]
    [InlineData(50, 2.0)]
    public void DifferentParams_AllFinite(int period, double vfactor)
    {
        var source = MakeSeries(200);
        var gdema = new Gdema(period, vfactor);
        for (int i = 0; i < source.Count; i++)
        {
            double val = gdema.Update(source[i]).Value;
            Assert.True(double.IsFinite(val), $"NaN/Inf at bar {i} with period={period}, vfactor={vfactor}");
        }
    }

    [Fact]
    public void Constant_ConvergesToConstant()
    {
        var gdema = new Gdema(20, vfactor: 1.5);
        double last = 0;
        for (int i = 0; i < 1000; i++)
        {
            last = gdema.Update(new TValue(DateTime.UtcNow, 77.0)).Value;
        }
        Assert.Equal(77.0, last, 1e-6);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        const int period = 10;
        var source = MakeSeries(100);
        var gdema = new Gdema(period);

        for (int i = 0; i < source.Count; i++)
        {
            var first = gdema.Update(source[i], isNew: true);
            // Correct with different values, then restore
            _ = gdema.Update(new TValue(source[i].Time, source[i].Value * 1.1), isNew: false);
            _ = gdema.Update(new TValue(source[i].Time, source[i].Value * 0.9), isNew: false);
            var restored = gdema.Update(source[i], isNew: false);
            Assert.Equal(first.Value, restored.Value, 1e-12);
        }
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var source = MakeSeries(200);
        var (results, indicator) = Gdema.Calculate(source, 10);
        Assert.True(indicator.IsHot);
        Assert.Equal(source.Count, results.Count);
    }

    [Fact]
    public void LargeDataset_NoOverflow()
    {
        var source = MakeSeries(5000);
        var gdema = new Gdema(50, vfactor: 2.0);
        for (int i = 0; i < source.Count; i++)
        {
            double val = gdema.Update(source[i]).Value;
            Assert.True(double.IsFinite(val), $"Overflow at bar {i}");
        }
    }

    [Fact]
    public void SubsetStability()
    {
        var source = MakeSeries(300);

        // Run on first 200
        var gdema1 = new Gdema(10);
        double val200 = 0;
        for (int i = 0; i < 200; i++)
        {
            val200 = gdema1.Update(source[i]).Value;
        }

        // Run on all 300
        var gdema2 = new Gdema(10);
        double val200_full = 0;
        for (int i = 0; i < 300; i++)
        {
            double v = gdema2.Update(source[i]).Value;
            if (i == 199)
            {
                val200_full = v;
            }
        }

        Assert.Equal(val200, val200_full, 1e-12);
    }
}
