using System;

namespace QuanTAlib.Tests;

public class FramaValidationTests
{
    [Fact]
    public void Frama_Streaming_MatchesReference()
    {
        int period = 16;
        TBarSeries series = BuildSeries(200, seed: 5);
        double[] reference = new double[series.Count];

        ReferenceFrama(series.High.Values, series.Low.Values, period, reference);

        var frama = new Frama(period);
        for (int i = 0; i < series.Count; i++)
        {
            double actual = frama.Update(series[i], isNew: true).Value;
            Assert.Equal(reference[i], actual, precision: 10);
        }
    }

    [Fact]
    public void Frama_Batch_MatchesReference()
    {
        int period = 20;
        TBarSeries series = BuildSeries(180, seed: 7);
        double[] reference = new double[series.Count];

        ReferenceFrama(series.High.Values, series.Low.Values, period, reference);
        TSeries batch = Frama.Batch(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(reference[i], batch[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Frama_Span_MatchesReference()
    {
        int period = 24;
        TBarSeries series = BuildSeries(160, seed: 11);
        double[] output = new double[series.Count];
        double[] reference = new double[series.Count];

        ReferenceFrama(series.High.Values, series.Low.Values, period, reference);
        Frama.Batch(series.High.Values, series.Low.Values, period, output);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(reference[i], output[i], precision: 10);
        }
    }

    private static void ReferenceFrama(ReadOnlySpan<double> high, ReadOnlySpan<double> low, int period, Span<double> output)
    {
        int pe = (period % 2 == 0) ? period : period + 1;
        int h = pe / 2;

        double lastHigh = double.NaN;
        double lastLow = double.NaN;
        double fr = double.NaN;
        bool hasValue = false;

        for (int i = 0; i < high.Length; i++)
        {
            double highVal = high[i];
            double lowVal = low[i];

            if (!double.IsFinite(highVal) || !double.IsFinite(lowVal))
            {
                if (!double.IsFinite(lastHigh) || !double.IsFinite(lastLow))
                {
                    output[i] = double.NaN;
                    continue;
                }

                highVal = lastHigh;
                lowVal = lastLow;
            }

            lastHigh = highVal;
            lastLow = lowVal;

            if (i < pe - 1)
            {
                output[i] = double.NaN;
                continue;
            }

            double maxRecent = double.MinValue;
            double minRecent = double.MaxValue;
            double maxPrev = double.MinValue;
            double minPrev = double.MaxValue;
            double maxFull = double.MinValue;
            double minFull = double.MaxValue;

            int startFull = i - pe + 1;
            int startRecent = i - h + 1;

            for (int j = startFull; j <= i; j++)
            {
                double hv = high[j];
                double lv = low[j];
                if (!double.IsFinite(hv) || !double.IsFinite(lv))
                {
                    hv = lastHigh;
                    lv = lastLow;
                }

                if (hv > maxFull)
                {
                    maxFull = hv;
                }

                if (lv < minFull)
                {
                    minFull = lv;
                }

                if (j >= startRecent)
                {
                    if (hv > maxRecent)
                    {
                        maxRecent = hv;
                    }

                    if (lv < minRecent)
                    {
                        minRecent = lv;
                    }
                }
                else
                {
                    if (hv > maxPrev)
                    {
                        maxPrev = hv;
                    }

                    if (lv < minPrev)
                    {
                        minPrev = lv;
                    }
                }
            }

            double n1 = (maxRecent - minRecent) / h;
            double n2 = (maxPrev - minPrev) / h;
            double n3 = (maxFull - minFull) / pe;

            double alpha = 1.0;
            if (n1 > 0.0 && n2 > 0.0 && n3 > 0.0)
            {
                double dimen = (Math.Log(n1 + n2) - Math.Log(n3)) / 0.693147180559945309417232121458176568;
                alpha = Math.Exp(-4.6 * (dimen - 1.0));
                if (alpha < 0.01)
                {
                    alpha = 0.01;
                }

                if (alpha > 1.0)
                {
                    alpha = 1.0;
                }
            }

            double price = (highVal + lowVal) * 0.5;
            double prev = hasValue && double.IsFinite(fr) ? fr : price;
            fr = Math.FusedMultiplyAdd(prev, 1.0 - alpha, alpha * price);
            hasValue = true;

            output[i] = fr;
        }
    }

    private static TBarSeries BuildSeries(int count, int seed)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }
}
