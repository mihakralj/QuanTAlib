using System;

namespace QuanTAlib.Tests;

public class HemaValidationTests
{
    [Fact]
    public void Hema_Streaming_MatchesReference()
    {
        int period = 20;
        TSeries series = BuildSeries(300, seed: 5);
        double[] reference = new double[series.Count];

        ReferenceHema(series.Values, reference, period);

        var hema = new Hema(period);
        for (int i = 0; i < series.Count; i++)
        {
            double actual = hema.Update(series[i]).Value;
            Assert.Equal(reference[i], actual, precision: 10);
        }
    }

    [Fact]
    public void Hema_Batch_MatchesReference()
    {
        int period = 14;
        TSeries series = BuildSeries(250, seed: 9);
        double[] reference = new double[series.Count];

        ReferenceHema(series.Values, reference, period);
        TSeries batch = Hema.Calculate(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(reference[i], batch[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Hema_Span_MatchesReference()
    {
        int period = 30;
        TSeries series = BuildSeries(200, seed: 12);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];
        var reference = new double[values.Length];

        ReferenceHema(values, reference, period);
        Hema.Calculate(values, output, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(reference[i], output[i], precision: 10);
        }
    }

    private static void ReferenceHema(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        double n = Math.Max(period, 2);

        double hlSlow = n;
        double hlFast = Math.Max(1.0, n * 0.5);
        double hlSmooth = Math.Max(1.0, Math.Sqrt(n));

        double aS = AlphaFromHalfLife(hlSlow);
        double aF = AlphaFromHalfLife(hlFast);
        double aM = AlphaFromHalfLife(hlSmooth);

        double bS = 1.0 - aS;
        double bF = 1.0 - aF;
        double bM = 1.0 - aM;

        double lagS = bS / aS;
        double lagF = bF / aF;
        double ratio = Math.Clamp(lagF / lagS, 0.0, 0.999999);
        double invOneMinusRatio = 1.0 / Math.Max(1.0 - ratio, 1e-12);

        bool warmup = true;
        double decayS = 1.0;
        double decayF = 1.0;
        double decayM = 1.0;

        double eSraw = 0.0;
        double eFraw = 0.0;
        double eMraw = 0.0;
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

            eSraw = aS * (val - eSraw) + eSraw;
            eFraw = aF * (val - eFraw) + eFraw;

            if (warmup)
            {
                decayS *= bS;
                decayF *= bF;
                decayM *= bM;

                double invS = 1.0 / Math.Max(1.0 - decayS, 1e-12);
                double invF = 1.0 / Math.Max(1.0 - decayF, 1e-12);
                double invM = 1.0 / Math.Max(1.0 - decayM, 1e-12);

                double eS = eSraw * invS;
                double eF = eFraw * invF;
                double deLag = (eF - ratio * eS) * invOneMinusRatio;

                eMraw = aM * (deLag - eMraw) + eMraw;
                output[i] = eMraw * invM;

                double maxDecay = Math.Max(decayS, Math.Max(decayF, decayM));
                warmup = maxDecay > 1e-10;
            }
            else
            {
                double deLag = (eFraw - ratio * eSraw) * invOneMinusRatio;
                eMraw = aM * (deLag - eMraw) + eMraw;
                output[i] = eMraw;
            }
        }
    }

    private static double AlphaFromHalfLife(double halfLife)
    {
        double hl = Math.Max(1.0, halfLife);
        double x = -0.693147180559945309417232121458176568 / hl;
        return -Expm1(x);
    }

    private static double Expm1(double x)
    {
        double ax = Math.Abs(x);
        if (ax < 1e-5)
        {
            double x2 = x * x;
            return x + (x2 * 0.5) + (x2 * x * (1.0 / 6.0));
        }

        return Math.Exp(x) - 1.0;
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
