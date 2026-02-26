namespace QuanTAlib.Tests;

/// <summary>
/// Trim self-consistency validation.
/// No external library has a built-in trimmed mean moving average,
/// so we validate internal consistency: batch == streaming == span.
/// </summary>
public class TrimValidationTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Trim_Streaming_Equals_SpanBatch()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 1001);
        int n = 200;
        int period = 20;
        double trimPct = 10.0;

        var prices = new double[n];
        var times = new long[n];
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            prices[i] = bar.Close;
            times[i] = t0.AddMinutes(i).Ticks;
        }

        // Streaming
        var streaming = new Trim(period, trimPct);
        var streamValues = new double[n];
        for (int i = 0; i < n; i++)
        {
            streamValues[i] = streaming.Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), prices[i])).Value;
        }

        // Span batch
        var spanValues = new double[n];
        Trim.Batch(prices, spanValues, period, trimPct);

        for (int i = period - 1; i < n; i++)
        {
            Assert.Equal(streamValues[i], spanValues[i], 9);
        }
    }

    [Fact]
    public void Trim_TrimPctZero_EqualsSMA_LongSeries()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 2002);
        int n = 200;
        int period = 14;

        var prices = new double[n];
        var times = new long[n];
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            prices[i] = bar.Close;
            times[i] = t0.AddMinutes(i).Ticks;
        }

        var smaRef = new double[n];
        var trimOut = new double[n];

        // Manual SMA using span for reference (trimZero is redundant — Batch is the span path)
        Trim.Batch(prices, trimOut, period, 0.0);

        // Manual reference: SMA with period
        for (int i = 0; i < n; i++)
        {
            int start = Math.Max(0, i - period + 1);
            double sum = 0;
            int cnt = 0;
            for (int j = start; j <= i; j++)
            {
                sum += prices[j];
                cnt++;
            }

            smaRef[i] = sum / cnt;
        }

        // After warmup, both should match
        for (int i = period - 1; i < n; i++)
        {
            Assert.Equal(smaRef[i], trimOut[i], 9);
        }
    }

    [Fact]
    public void Trim_BatchTSeries_EqualsStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0001, sigma: 0.015, seed: 3003);
        int n = 50;
        int period = 10;
        double trimPct = 15.0;

        var series = new TSeries();
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            series.Add(new TValue(t0.AddMinutes(i), bar.Close));
        }

        var batchResult = Trim.Batch(series, period, trimPct);

        var streaming = new Trim(period, trimPct);
        TValue lastStream = default;
        for (int i = 0; i < n; i++)
        {
            lastStream = streaming.Update(series[i]);
        }

        Assert.Equal(lastStream.Value, batchResult[n - 1].Value, 9);
    }

    [Fact]
    public void Trim_HighTrimPct_ApproachesMedian()
    {
        // With trimPct=49 on period=10, trimCount=4, keepCount=2 (middle 2 values)
        var trim = new Trim(10, 49.0);
        double[] vals = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        foreach (double v in vals)
        {
            trim.Update(new TValue(DateTime.UtcNow, v));
        }

        // keepCount = 10 - 2*4 = 2, trimCount=4
        // middle 2 values of sorted [1..10] = [5,6], mean = 5.5
        Assert.Equal(5.5, trim.Last.Value, 10);
    }
}
