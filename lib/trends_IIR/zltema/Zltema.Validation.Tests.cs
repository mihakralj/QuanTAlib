using System;

namespace QuanTAlib.Tests;

public class ZltemaValidationTests
{
    [Fact]
    public void Zltema_Streaming_MatchesReference()
    {
        const int period = 20;
        TSeries series = BuildSeries(300, seed: 5);
        double[] reference = new double[series.Count];

        ReferenceZltema(series.Values, reference, period);

        var zltema = new Zltema(period);
        for (int i = 0; i < series.Count; i++)
        {
            double actual = zltema.Update(series[i]).Value;
            Assert.Equal(reference[i], actual, precision: 10);
        }
    }

    [Fact]
    public void Zltema_Batch_MatchesReference()
    {
        const int period = 14;
        TSeries series = BuildSeries(250, seed: 9);
        double[] reference = new double[series.Count];

        ReferenceZltema(series.Values, reference, period);
        TSeries batch = Zltema.Calculate(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(reference[i], batch[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Zltema_Span_MatchesReference()
    {
        const int period = 30;
        TSeries series = BuildSeries(200, seed: 12);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];
        var reference = new double[values.Length];

        ReferenceZltema(values, reference, period);
        Zltema.Calculate(values, output, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(reference[i], output[i], precision: 10);
        }
    }

    private static void ReferenceZltema(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        double alpha = 2.0 / (period + 1);
        double beta = 1.0 - alpha;
        int lag = ComputeLag(period);
        int bufferSize = lag + 1;

        double ema1Raw = 0.0;
        double ema2Raw = 0.0;
        double ema3Raw = 0.0;
        double e = 1.0;
        bool warmup = true;
        double lastValid = double.NaN;

        double[] buffer = new double[bufferSize];
        int head = 0;

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

            buffer[head] = val;
            head++;
            if (head == bufferSize)
            {
                head = 0;
            }

            double lagged = buffer[head];
            double signal = Math.FusedMultiplyAdd(2.0, val, -lagged);

            // First EMA stage
            ema1Raw = Math.FusedMultiplyAdd(ema1Raw, beta, alpha * signal);

            double ema1, ema2, ema3;

            if (warmup)
            {
                e *= beta;
                double compensator = 1.0 / (1.0 - e);
                ema1 = ema1Raw * compensator;

                // Second EMA stage
                ema2Raw = Math.FusedMultiplyAdd(ema2Raw, beta, alpha * ema1);
                ema2 = ema2Raw * compensator;

                // Third EMA stage
                ema3Raw = Math.FusedMultiplyAdd(ema3Raw, beta, alpha * ema2);
                ema3 = ema3Raw * compensator;

                if (e <= 1e-10)
                {
                    warmup = false;
                }
            }
            else
            {
                ema1 = ema1Raw;
                ema2Raw = Math.FusedMultiplyAdd(ema2Raw, beta, alpha * ema1);
                ema2 = ema2Raw;
                ema3Raw = Math.FusedMultiplyAdd(ema3Raw, beta, alpha * ema2);
                ema3 = ema3Raw;
            }

            // TEMA formula: 3 * EMA1 - 3 * EMA2 + EMA3
            output[i] = Math.FusedMultiplyAdd(3.0, ema1, Math.FusedMultiplyAdd(-3.0, ema2, ema3));
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