using System;
using Tulip;

namespace QuanTAlib.Tests;

public class ZlemaValidationTests
{
    // Note: External library validation is not feasible for ZLEMA:
    // - Tulip: Uses SMA-seeded EMA initialization, producing a persistent offset vs QuanTAlib's
    //   debiased warmup (diff ~0.009% at bar 200, does not converge). Algorithm variant.
    // - Skender.Stock.Indicators: Does not have a ZLEMA implementation.
    // - TALib: Does not have a ZLEMA function.
    // - OoplesFinance: Does not have a ZLEMA implementation.
    // Validated against independent reference implementation in tests below.

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
        TSeries batch = Zlema.Batch(series, period);

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
        Zlema.Batch(values, output, period);

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

    // === Tulip Cross-Validation (Structural) ===

    /// <summary>
    /// Structural validation against Tulip <c>zlema</c>.
    /// Algorithm variant: Tulip ZLEMA seeds the EMA with an SMA over the first
    /// <c>period</c> bars, producing a persistent offset vs QuanTAlib's debiased
    /// warmup (~0.009% at bar 200, non-converging). Direct numeric equality is not
    /// asserted; both must produce finite, non-negative output on the same data.
    /// </summary>
    [Fact]
    public void Zlema_Tulip_StructuralVariant_BothFinite()
    {
        const int period = 20;
        var source = BuildSeries(300, seed: 42);
        double[] rawData = new double[source.Count];
        for (int i = 0; i < source.Count; i++) { rawData[i] = source[i].Value; }

        // Tulip zlema
        var tulipIndicator = Tulip.Indicators.zlema;
        double[][] inputs = { rawData };
        double[] options = { period };
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[rawData.Length - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        // QuanTAlib Zlema
        var zlema = new Zlema(period);
        foreach (var v in source) { zlema.Update(v); }

        // Structural: both must be finite and positive (price-scale)
        Assert.True(tResult.Length > 0, "Tulip zlema must produce output");
        foreach (double v in tResult)
        {
            Assert.True(double.IsFinite(v), $"Tulip zlema produced non-finite value: {v}");
            Assert.True(v > 0, $"Tulip zlema must be positive for positive prices, got {v}");
        }

        Assert.True(zlema.IsHot, "QuanTAlib Zlema must be hot after sufficient bars");
        Assert.True(zlema.Last.Value > 0, "QuanTAlib Zlema last value must be positive");
    }
}
