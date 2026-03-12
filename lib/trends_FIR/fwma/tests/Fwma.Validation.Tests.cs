namespace QuanTAlib.Tests;

public class FwmaValidationTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < count; i++)
        {
            series.Add(gbm.Next());
        }
        return series;
    }

    // === Self-consistency validation (no external library implements FWMA) ===

    [Fact]
    public void Batch_Matches_Streaming()
    {
        int period = 10;
        var src = MakeSeries(200);

        var batchResult = Fwma.Batch(src, period);

        var streaming = new Fwma(period);
        for (int i = 0; i < src.Count; i++)
        {
            streaming.Update(src[i]);
        }

        // Compare last value
        Assert.Equal(streaming.Last.Value, batchResult.Values[^1], 1e-10);

        // Compare all values after warmup
        var streaming2 = new Fwma(period);
        for (int i = 0; i < src.Count; i++)
        {
            double streamVal = streaming2.Update(src[i]).Value;
            Assert.Equal(streamVal, batchResult.Values[i], 1e-10);
        }
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        int period = 10;
        var src = MakeSeries(200);

        double[] spanOutput = new double[src.Count];
        Fwma.Batch(src.Values, spanOutput.AsSpan(), period);

        var streaming = new Fwma(period);
        for (int i = 0; i < src.Count; i++)
        {
            double streamVal = streaming.Update(src[i]).Value;
            Assert.Equal(streamVal, spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void Calculate_Matches_Batch()
    {
        int period = 10;
        var src = MakeSeries(200);

        var batchResult = Fwma.Batch(src, period);
        var (calcResult, _) = Fwma.Calculate(src, period);

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], calcResult.Values[i], 1e-10);
        }
    }

    [Fact]
    public void OutputBounded_ByInputRange()
    {
        // FIR with all-positive weights: output must be within [min, max] of input window
        int period = 10;
        var src = MakeSeries(200);
        var result = Fwma.Batch(src, period);

        for (int i = period - 1; i < src.Count; i++)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int k = 0; k < period; k++)
            {
                double v = src.Values[i - k];
                if (v < min)
                {
                    min = v;
                }
                if (v > max)
                {
                    max = v;
                }
            }
            Assert.True(result.Values[i] >= min - 1e-10, $"Output at {i} below min");
            Assert.True(result.Values[i] <= max + 1e-10, $"Output at {i} above max");
        }
    }

    [Fact]
    public void FWMA_MoreResponsive_ThanSMA()
    {
        // FWMA should have less lag than SMA (lower center of gravity)
        // Test with a step function: FWMA should reach the step faster
        int period = 10;
        var fwma = new Fwma(period);
        var sma = new Sma(period);

        // Feed constant 100 to fill buffers
        for (int i = 0; i < period; i++)
        {
            fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            sma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Step to 200
        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(period), 200.0));
        sma.Update(new TValue(DateTime.UtcNow.AddSeconds(period), 200.0));

        // FWMA should be closer to 200 than SMA (more responsive)
        double fwmaVal = fwma.Last.Value;
        double smaVal = sma.Last.Value;

        Assert.True(fwmaVal > smaVal, $"FWMA ({fwmaVal}) should be more responsive than SMA ({smaVal})");
    }

    [Fact]
    public void FWMA_MoreResponsive_ThanWMA()
    {
        // FWMA (Fibonacci weights) should be more responsive than WMA (linear weights)
        int period = 10;
        var fwma = new Fwma(period);
        var wma = new Wma(period);

        for (int i = 0; i < period; i++)
        {
            fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            wma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        fwma.Update(new TValue(DateTime.UtcNow.AddSeconds(period), 200.0));
        wma.Update(new TValue(DateTime.UtcNow.AddSeconds(period), 200.0));

        double fwmaVal = fwma.Last.Value;
        double wmaVal = wma.Last.Value;

        Assert.True(fwmaVal > wmaVal, $"FWMA ({fwmaVal}) should be more responsive than WMA ({wmaVal})");
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var src = MakeSeries(100);

        var r5 = Fwma.Batch(src, 5);
        var r10 = Fwma.Batch(src, 10);

        // After both are hot, at least some values should differ
        bool anyDifferent = false;
        for (int i = 10; i < src.Count; i++)
        {
            if (Math.Abs(r5.Values[i] - r10.Values[i]) > 1e-10)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void LargePeriod_Handles()
    {
        int period = 50;
        var src = MakeSeries(200);
        var result = Fwma.Batch(src, period);

        Assert.Equal(src.Count, result.Count);
        Assert.True(double.IsFinite(result.Values[^1]));
    }

    [Fact]
    public void AllNaN_Input_ReturnsNaN()
    {
        double[] source = [double.NaN, double.NaN, double.NaN];
        double[] output = new double[3];

        Fwma.Batch(source.AsSpan(), output.AsSpan(), period: 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsNaN(output[i]), $"All-NaN input should produce NaN at index {i}");
        }
    }

    [Fact]
    public void BarCorrection_MultipleCorrections_Stable()
    {
        // Apply multiple corrections and verify stability
        int period = 5;
        var fwma = new Fwma(period);
        var series = MakeSeries(20);

        for (int i = 0; i < series.Count; i++)
        {
            fwma.Update(series[i], isNew: true);
        }

        double baseValue = fwma.Last.Value;

        // Apply 10 corrections with the same value
        for (int c = 0; c < 10; c++)
        {
            _ = fwma.Update(series[^1], isNew: false);
        }

        Assert.Equal(baseValue, fwma.Last.Value, 1e-10);
    }
}
