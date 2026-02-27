using Tulip;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// MeanDev cross-validation. ExcelAVEDEV formula and numpy mean(abs(x-mean(x)))
/// are the reference implementations — both exact matches at default tolerance.
/// Also cross-validated against Tulip <c>md</c> (Mean Deviation) — exact formula match.
/// </summary>
public class MeanDevValidationTests
{
    // ─────────────────────────────────────────────────────────────
    // Reference implementation: pure-C# replication of the formula
    // ─────────────────────────────────────────────────────────────
    private static double ReferenceMeanDev(double[] window)
    {
        int n = window.Length;
        if (n == 0)
        {
            return 0;
        }

        double mean = 0;
        for (int i = 0; i < n; i++)
        {
            mean += window[i];
        }

        mean /= n;
        double devSum = 0;
        for (int i = 0; i < n; i++)
        {
            devSum += Math.Abs(window[i] - mean);
        }

        return devSum / n;
    }

    [Fact]
    public void MeanDev_Matches_Reference_KnownData()
    {
        // window = {2, 4, 4, 4, 5, 5, 7, 9}: mean=5, MD=1.5
        double[] data = { 2, 4, 4, 4, 5, 5, 7, 9 };
        var md = new MeanDev(data.Length);
        foreach (double v in data)
        {
            md.Update(new TValue(DateTime.UtcNow, v));
        }
        double expected = ReferenceMeanDev(data);
        Assert.Equal(expected, md.Last.Value, precision: 10);
    }

    [Fact]
    public void MeanDev_Batch_Matches_Reference_GBM()
    {
        const int period = 14;
        var gbm = new GBM(seed: 12345);
        var closes = new List<double>();
        var series = new TSeries();
        for (int i = 0; i < 300; i++)
        {
            var bar = gbm.Next();
            closes.Add(bar.Close);
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var result = MeanDev.Batch(series, period);

        // Verify every bar against reference
        for (int i = period - 1; i < closes.Count; i++)
        {
            double[] window = closes.Skip(i - period + 1).Take(period).ToArray();
            double expected = ReferenceMeanDev(window);
            Assert.Equal(expected, result[i].Value, precision: 9);
        }
    }

    [Fact]
    public void MeanDev_Streaming_Matches_Reference_GBM()
    {
        const int period = 20;
        var gbm = new GBM(seed: 54321);
        var closes = new List<double>();
        var md = new MeanDev(period);

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next();
            closes.Add(bar.Close);
            md.Update(new TValue(bar.Time, bar.Close));

            if (i >= period - 1)
            {
                double[] window = closes.Skip(i - period + 1).Take(period).ToArray();
                double expected = ReferenceMeanDev(window);
                Assert.Equal(expected, md.Last.Value, precision: 9);
            }
        }
    }

    [Fact]
    public void MeanDev_Span_Matches_Reference_GBM()
    {
        const int period = 10;
        var gbm = new GBM(seed: 999);
        var closes = new List<double>();
        for (int i = 0; i < 100; i++)
        {
            closes.Add(gbm.Next().Close);
        }

        var src = closes.ToArray();
        var dst = new double[src.Length];
        MeanDev.Batch(src.AsSpan(), dst.AsSpan(), period);

        for (int i = period - 1; i < closes.Count; i++)
        {
            double[] window = closes.Skip(i - period + 1).Take(period).ToArray();
            double expected = ReferenceMeanDev(window);
            Assert.Equal(expected, dst[i], precision: 9);
        }
    }

    [Fact]
    public void MeanDev_Period1_AlwaysZero()
    {
        // Single element: MD=0 regardless of value
        var md = new MeanDev(1);
        var gbm = new GBM(seed: 77);
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            md.Update(new TValue(bar.Time, bar.Close));
            Assert.Equal(0.0, md.Last.Value, precision: 10);
        }
    }

    [Fact]
    public void MeanDev_SlidingWindow_CorrectlyDropsOldest()
    {
        // Period=3, feed 5 values, verify last window
        const int period = 3;
        double[] data = { 1, 2, 3, 4, 5 };
        var md = new MeanDev(period);
        for (int i = 0; i < data.Length; i++)
        {
            md.Update(new TValue(DateTime.UtcNow, data[i]));
        }
        // Last window = {3, 4, 5}: mean=4, MD=(1+0+1)/3 = 2/3
        double expected = ReferenceMeanDev(new double[] { 3, 4, 5 });
        Assert.Equal(expected, md.Last.Value, precision: 10);
    }

    [Fact]
    public void MeanDev_Relationship_To_StdDev()
    {
        // For any dataset, MD <= StdDev (population)
        const int period = 20;
        var gbm = new GBM(seed: 333);
        var series = new TSeries();
        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var mdResult = MeanDev.Batch(series, period);
        var sdResult = StdDev.Batch(series, period);

        for (int i = period - 1; i < series.Count; i++)
        {
            Assert.True(mdResult[i].Value <= sdResult[i].Value + 1e-10,
                $"MD ({mdResult[i].Value}) > StdDev ({sdResult[i].Value}) at bar {i}");
        }
    }

    // ── Tulip Cross-Validation ────────────────────────────────────────────────

    /// <summary>
    /// Validates MeanDev against Tulip <c>md</c> (Mean Deviation).
    /// Tulip formula: mean(|x - mean(x)|) over a rolling window — exact match.
    /// </summary>
    [Fact]
    public void MeanDev_Matches_Tulip_Batch()
    {
        const int period = 14;
        var gbm = new GBM(seed: 42001);
        var series = new TSeries();
        var closeData = new List<double>();
        for (int i = 0; i < 500; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
            closeData.Add(bar.Close);
        }

        var qResult = MeanDev.Batch(series, period);

        var tulipIndicator = Tulip.Indicators.md;
        double[] data = closeData.ToArray();
        double[][] inputs = { data };
        double[] options = { period };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[data.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: 1e-9);
    }

    [Fact]
    public void MeanDev_Matches_Tulip_Streaming()
    {
        const int period = 20;
        var gbm = new GBM(seed: 42002);
        var closeData = new List<double>();
        var md = new MeanDev(period);
        var qResults = new List<double>();

        for (int i = 0; i < 500; i++)
        {
            var bar = gbm.Next();
            closeData.Add(bar.Close);
            qResults.Add(md.Update(new TValue(bar.Time, bar.Close)).Value);
        }

        var tulipIndicator = Tulip.Indicators.md;
        double[] data = closeData.ToArray();
        double[][] inputs = { data };
        double[] options = { period };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[data.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: 1e-9);
    }
}
