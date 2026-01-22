using System;

namespace QuanTAlib.Tests;

public class ZlemaValidationTests
{
    [Fact]
    public void Zlema_Streaming_MatchesReference()
    {
        const int period = 20;
        TSeries series = BuildSeries(300, seed: 5);
        double[] reference = new double[series.Count];

        ReferenceZlema(series.Values, reference, period);

        var zlema = new Zlema(period);
        for (int i = 0; i < series.Count; i++)
        {
            double actual = zlema.Update(series[i]).Value;
            Assert.Equal(reference[i], actual, precision: 10);
        }
    }

    [Fact]
    public void Zlema_Batch_MatchesReference()
    {
        const int period = 14;
        TSeries series = BuildSeries(250, seed: 9);
        double[] reference = new double[series.Count];

        ReferenceZlema(series.Values, reference, period);
        TSeries batch = Zlema.Calculate(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(reference[i], batch[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Zlema_Span_MatchesReference()
    {
        const int period = 30;
        TSeries series = BuildSeries(200, seed: 12);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];
        var reference = new double[values.Length];

        ReferenceZlema(values, reference, period);
        Zlema.Calculate(values, output, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(reference[i], output[i], precision: 10);
        }
    }

    private static void ReferenceZlema(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        double alpha = 2.0 / (period + 1);
        double beta = 1.0 - alpha;
        int lag = ComputeLag(period);
        int bufferSize = lag + 1;

        double zlemaRaw = 0.0;
        double e = 1.0;
        bool warmup = true;
        double lastValid = double.NaN;

        double[] buffer = new double[bufferSize];
        int head = 0;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

            buffer[head] = val;
            head++;
            if (head == bufferSize)
                head = 0;

            double lagged = buffer[head];
            double signal = Math.FusedMultiplyAdd(2.0, val, -lagged);

            zlemaRaw = Math.FusedMultiplyAdd(zlemaRaw, beta, alpha * signal);

            if (warmup)
            {
                e *= beta;
                if (e <= 1e-10)
                {
                    warmup = false;
                    output[i] = zlemaRaw;
                }
                else
                {
                    output[i] = zlemaRaw / (1.0 - e);
                }
            }
            else
            {
                output[i] = zlemaRaw;
            }
        }
    }

    private static int ComputeLag(double period)
    {
        double lag = (period - 1.0) * 0.5;
        int lagInt = (int)Math.Round(lag, MidpointRounding.AwayFromZero);
        return Math.Max(1, lagInt);
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
