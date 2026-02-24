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
        TSeries batch = Hema.Batch(series, period);

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
        Hema.Batch(values, output, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(reference[i], output[i], precision: 10);
        }
    }

    private static void ReferenceHema(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int n = Math.Max(period, 2);
        int halfN = n / 2;                        // integer floor, same as HMA
        int sqrtN = Math.Max((int)Math.Sqrt(n), 1); // integer floor, same as HMA

        double aS = AlphaFromWmaLag(n);
        double aF = AlphaFromWmaLag(Math.Max(halfN, 1));
        double aM = AlphaFromWmaLag(Math.Max(sqrtN, 1));

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

            eSraw = Math.FusedMultiplyAdd(eSraw, bS, aS * val);
            eFraw = Math.FusedMultiplyAdd(eFraw, bF, aF * val);

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
                double deLag = Math.FusedMultiplyAdd(-ratio, eS, eF) * invOneMinusRatio;

                eMraw = Math.FusedMultiplyAdd(eMraw, bM, aM * deLag);
                output[i] = eMraw * invM;

                double maxDecay = Math.Max(decayS, Math.Max(decayF, decayM));
                warmup = maxDecay > 1e-10;
            }
            else
            {
                double deLag = Math.FusedMultiplyAdd(-ratio, eSraw, eFraw) * invOneMinusRatio;
                eMraw = Math.FusedMultiplyAdd(eMraw, bM, aM * deLag);
                output[i] = eMraw;
            }
        }
    }

    private static double AlphaFromWmaLag(int p)
    {
        // WMA-lag-matched alpha: EMA lag = (1-α)/α = (P-1)/3
        // Solving: α = 3/(P+2)
        return 3.0 / (Math.Max(p, 1) + 2.0);
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
