namespace QuanTAlib.Tests;

/// <summary>
/// Harmean Validation Tests - Self-consistency validation.
/// No external TA library implements rolling harmonic mean, so we validate
/// against mathematical properties and internal consistency.
/// </summary>
public sealed class HarmeanValidationTests
{
    private static TSeries CreateGbmSeries(int count = 500, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: seed);
        var times = new List<long>(count);
        var values = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }
        return new TSeries(times, values);
    }

    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        // HM of identical values = that value
        var h = new Harmean(20);
        for (int i = 0; i < 50; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, h.Last.Value, 10);
    }

    [Fact]
    public void HarmeanLeqGeometricMean()
    {
        // HM-GM inequality: HM ≤ GM for all positive values
        var series = CreateGbmSeries();
        int period = 20;
        var h = new Harmean(period);
        var g = new Geomean(period);

        for (int i = 0; i < series.Count; i++)
        {
            h.Update(series[i]);
            g.Update(series[i]);
            if (h.IsHot && g.IsHot)
            {
                Assert.True(h.Last.Value <= g.Last.Value + 1e-10,
                    $"HM-GM violated at bar {i}: HM={h.Last.Value}, GM={g.Last.Value}");
            }
        }
    }

    [Fact]
    public void HarmeanLeqArithmeticMean()
    {
        // HM ≤ AM for all positive values
        var series = CreateGbmSeries();
        int period = 20;
        var h = new Harmean(period);
        var sma = new Sma(period);

        for (int i = 0; i < series.Count; i++)
        {
            h.Update(series[i]);
            sma.Update(series[i]);
            if (h.IsHot)
            {
                Assert.True(h.Last.Value <= sma.Last.Value + 1e-10,
                    $"HM-AM violated at bar {i}: HM={h.Last.Value}, AM={sma.Last.Value}");
            }
        }
    }

    [Fact]
    public void BatchAndStreaming_Match()
    {
        var series = CreateGbmSeries();
        int period = 14;

        // Streaming
        var hStream = new Harmean(period);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            hStream.Update(series[i]);
            streamResults[i] = hStream.Last.Value;
        }

        // Batch
        var batchResult = Harmean.Batch(series, period);
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResult[i].Value, 8);
        }
    }

    [Fact]
    public void OutputIsPositive()
    {
        var series = CreateGbmSeries();
        var h = new Harmean(14);
        for (int i = 0; i < series.Count; i++)
        {
            h.Update(series[i]);
            Assert.True(h.Last.Value > 0, $"Output not positive at bar {i}: {h.Last.Value}");
        }
    }

    [Fact]
    public void Calculate_ReturnsCorrectResults()
    {
        var series = CreateGbmSeries(100);
        var (results, indicator) = Harmean.Calculate(series, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(100, results.Count);
        Assert.True(double.IsFinite(results[^1].Value));
    }

    [Fact]
    public void NearConstant_NearConstant()
    {
        // Values very close together → HM ≈ AM ≈ the value
        var h = new Harmean(10);
        for (int i = 0; i < 20; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 100.0 + (i * 0.001)));
        }
        Assert.True(Math.Abs(h.Last.Value - 100.01) < 0.1,
            $"Expected near 100.01, got {h.Last.Value}");
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var series = CreateGbmSeries(200);
        int period = 14;

        var batchResult = Harmean.Batch(series, period);

        var src = series.Values;
        Span<double> output = new double[200];
        Harmean.Batch(src, output, period);

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 8);
        }
    }

    [Fact]
    public void MeanInequality_HM_LE_GM_LE_AM()
    {
        // Full mean inequality chain: HM ≤ GM ≤ AM
        var series = CreateGbmSeries(200);
        int period = 14;
        var h = new Harmean(period);
        var g = new Geomean(period);
        var sma = new Sma(period);

        for (int i = 0; i < series.Count; i++)
        {
            h.Update(series[i]);
            g.Update(series[i]);
            sma.Update(series[i]);
            if (h.IsHot && g.IsHot)
            {
                double hm = h.Last.Value;
                double gm = g.Last.Value;
                double am = sma.Last.Value;

                Assert.True(hm <= gm + 1e-10,
                    $"HM > GM at bar {i}: HM={hm}, GM={gm}");
                Assert.True(gm <= am + 1e-10,
                    $"GM > AM at bar {i}: GM={gm}, AM={am}");
            }
        }
    }
}
