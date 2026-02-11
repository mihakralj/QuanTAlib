using System;

namespace QuanTAlib.Tests;

public class MmaValidationTests
{
    [Fact]
    public void Mma_Streaming_MatchesReference()
    {
        int period = 20;
        TSeries series = BuildSeries(300, seed: 5);
        double[] reference = new double[series.Count];

        ReferenceMma(series.Values, reference, period);

        var mma = new Mma(period);
        for (int i = 0; i < series.Count; i++)
        {
            double actual = mma.Update(series[i]).Value;
            Assert.Equal(reference[i], actual, precision: 10);
        }
    }

    [Fact]
    public void Mma_Batch_MatchesReference()
    {
        int period = 14;
        TSeries series = BuildSeries(250, seed: 9);
        double[] reference = new double[series.Count];

        ReferenceMma(series.Values, reference, period);
        TSeries batch = Mma.Batch(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(reference[i], batch[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Mma_Span_MatchesReference()
    {
        int period = 30;
        TSeries series = BuildSeries(200, seed: 12);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];
        var reference = new double[values.Length];

        ReferenceMma(values, reference, period);
        Mma.Batch(values, output, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(reference[i], output[i], precision: 10);
        }
    }

    private static void ReferenceMma(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int window = Math.Min(Math.Max(2, period), 4000);
        double[] buffer = new double[window];
        int head = 0;
        int count = 0;
        double sum = 0.0;
        double lastValid = double.NaN;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

            if (count < window)
            {
                count++;
            }
            else
            {
                sum -= buffer[head];
            }

            buffer[head] = val;
            sum += val;

            head++;
            if (head == window)
            {
                head = 0;
            }

            double sma = sum / count;
            double weightedSum = ComputeWeightedSum(buffer, head, count);
            double denom = (count + 1.0) * count;
            output[i] = Math.FusedMultiplyAdd(weightedSum, 6.0 / denom, sma);
        }
    }

    private static double ComputeWeightedSum(double[] buffer, int head, int count)
    {
        int idx = head - 1;
        if (idx < 0)
        {
            idx = count - 1;
        }

        double weightedSum = 0.0;
        for (int i = 0; i < count; i++)
        {
            double weight = (count - ((2 * i) + 1)) * 0.5;
            weightedSum = Math.FusedMultiplyAdd(buffer[idx], weight, weightedSum);

            idx--;
            if (idx < 0)
            {
                idx = count - 1;
            }
        }

        return weightedSum;
    }

    private static TSeries BuildSeries(int count, int seed)
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: seed);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        return series;
    }
}
