namespace QuanTAlib.Tests;

public class FsiValidationTests
{
    private static readonly Random _rng = new(42);

    private static TSeries MakeSeries(int count = 500)
    {
        var series = new TSeries();
        double price = 100.0;
        for (int i = 0; i < count; i++)
        {
            price += (_rng.NextDouble() - 0.5) * 2.0;
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        return series;
    }

    [Fact]
    public void BatchStreaming_Match()
    {
        var series = MakeSeries(300);
        int period = 20;
        double bw = 0.1;

        // Streaming
        var streaming = new Fsi(period, bw);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Batch
        var batchResult = Fsi.Batch(series, period, bw);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResult[i].Value, 10);
        }
    }

    [Fact]
    public void SpanStreaming_Match()
    {
        var series = MakeSeries(300);
        int period = 20;
        double bw = 0.1;

        // Streaming
        var streaming = new Fsi(period, bw);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Span batch
        var spanResults = new double[series.Count];
        Fsi.Batch(series.Values, spanResults, period, bw);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 10);
        }
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentOutputs()
    {
        var series = MakeSeries(300);

        var result1 = new double[series.Count];
        var result2 = new double[series.Count];
        Fsi.Batch(series.Values, result1, 20, 0.1);
        Fsi.Batch(series.Values, result2, 40, 0.1);

        bool allEqual = true;
        for (int i = 50; i < series.Count; i++)
        {
            if (Math.Abs(result1[i] - result2[i]) > 1e-12)
            {
                allEqual = false;
                break;
            }
        }
        Assert.False(allEqual, "Different periods should produce different outputs");
    }

    [Fact]
    public void ConstantInput_ProducesZero()
    {
        int count = 200;
        var src = new double[count];
        var dst = new double[count];
        Array.Fill(src, 100.0);

        Fsi.Batch(src, dst, 20, 0.1);

        // After warmup, constant input → all-zero bandpass → output = 0
        for (int i = 20; i < count; i++)
        {
            Assert.Equal(0.0, dst[i], 10);
        }
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var series = MakeSeries(200);
        var (results, indicator) = Fsi.Calculate(series, 20, 0.1);
        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        var series = MakeSeries(100);
        var fsi = new Fsi(20, 0.1);

        foreach (var bar in series)
        {
            fsi.Update(bar);
        }

        // New bar
        double v1 = fsi.Update(new TValue(DateTime.UtcNow, 105.0), isNew: true).Value;

        // Corrections
        _ = fsi.Update(new TValue(DateTime.UtcNow, 108.0), isNew: false);
        _ = fsi.Update(new TValue(DateTime.UtcNow, 112.0), isNew: false);
        double v4 = fsi.Update(new TValue(DateTime.UtcNow, 105.0), isNew: false).Value;

        Assert.Equal(v1, v4, 10);
    }

    [Fact]
    public void SubsetStability()
    {
        // Running on a longer series should not change earlier values
        var series = MakeSeries(300);
        int period = 20;
        double bw = 0.1;

        var result200 = new double[200];
        Fsi.Batch(series.Values[..200], result200, period, bw);

        var result300 = new double[300];
        Fsi.Batch(series.Values, result300, period, bw);

        // First 200 bars of both runs must match exactly
        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(result200[i], result300[i], 15);
        }
    }
}
