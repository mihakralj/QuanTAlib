namespace QuanTAlib.Tests;

public class Sp15ValidationTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }
        return source;
    }

    [Fact]
    public void BatchVsStreaming_Match()
    {
        var source = MakeSeries(100);

        // Streaming
        var sp15 = new Sp15();
        var streaming = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streaming[i] = sp15.Update(source[i]).Value;
        }

        // Batch
        var batchResult = Sp15.Batch(source);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streaming[i], batchResult[i].Value, 1e-10);
        }
    }

    [Fact]
    public void SpanVsStreaming_Match()
    {
        var source = MakeSeries(100);

        // Streaming
        var sp15 = new Sp15();
        var streaming = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streaming[i] = sp15.Update(source[i]).Value;
        }

        // Span
        double[] spanOutput = new double[100];
        Sp15.Batch(source.Values, spanOutput);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streaming[i], spanOutput[i], 1e-10);
        }
    }

    [Fact]
    public void LinearPolynomial_ExactFit()
    {
        var sp15 = new Sp15();
        const int total = 50;
        const double a = 5.0, b = 3.0;

        for (int i = 0; i < total; i++)
        {
            double val = a + (b * i);
            sp15.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        int centerIdx = total - 1 - 7;
        double expected = a + (b * centerIdx);
        Assert.Equal(expected, sp15.Last.Value, 1e-6);
    }

    [Fact]
    public void QuadraticPolynomial_ExactFit()
    {
        var sp15 = new Sp15();
        const int total = 50;
        const double a = 2.0, b = 1.5, c = 0.3;

        for (int i = 0; i < total; i++)
        {
            double val = a + (b * i) + (c * i * i);
            sp15.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        int centerIdx = total - 1 - 7;
        double expected = a + (b * centerIdx) + (c * centerIdx * centerIdx);
        Assert.Equal(expected, sp15.Last.Value, 1e-4);
    }

    [Fact]
    public void CubicPolynomial_ExactFit()
    {
        var sp15 = new Sp15();
        const int total = 50;
        const double a = 1.0, b = 0.5, c = 0.1, d = 0.005;

        for (int i = 0; i < total; i++)
        {
            double val = a + (b * i) + (c * i * i) + (d * i * i * i);
            sp15.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        int centerIdx = total - 1 - 7;
        double expected = a + (b * centerIdx) + (c * centerIdx * centerIdx) + (d * centerIdx * centerIdx * centerIdx);
        Assert.Equal(expected, sp15.Last.Value, 1.0);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var source = MakeSeries(50);

        var (results, indicator) = Sp15.Calculate(source);

        Assert.True(indicator.IsHot);
        Assert.Equal(50, results.Count);
    }

    [Fact]
    public void ConstantPropagation_AllModes()
    {
        const double c = 77.0;
        const int len = 30;

        // Build constant series
        var source = new TSeries();
        for (int i = 0; i < len; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), c));
        }

        // Streaming
        var sp15 = new Sp15();
        for (int i = 0; i < len; i++)
        {
            sp15.Update(source[i]);
        }
        Assert.Equal(c, sp15.Last.Value, 1e-10);

        // Batch
        var batch = Sp15.Batch(source);
        for (int i = 15; i < len; i++)
        {
            Assert.Equal(c, batch[i].Value, 1e-10);
        }

        // Span
        double[] spanOut = new double[len];
        Sp15.Batch(source.Values, spanOut);
        for (int i = 15; i < len; i++)
        {
            Assert.Equal(c, spanOut[i], 1e-10);
        }
    }

    [Fact]
    public void WeightSymmetry_ForwardReverse()
    {
        // Symmetric weights: reversing input gives same center value for linear input
        var sp15Fwd = new Sp15();
        var sp15Rev = new Sp15();

        double[] forward = new double[15];
        double[] reverse = new double[15];
        for (int i = 0; i < 15; i++)
        {
            forward[i] = 10.0 + (2.0 * i);
            reverse[i] = 10.0 + (2.0 * (14 - i));
        }

        TValue fwdResult = default;
        TValue revResult = default;
        for (int i = 0; i < 15; i++)
        {
            fwdResult = sp15Fwd.Update(new TValue(DateTime.UtcNow.AddSeconds(i), forward[i]));
            revResult = sp15Rev.Update(new TValue(DateTime.UtcNow.AddSeconds(i), reverse[i]));
        }

        // For linear input centered at i=7: forward center = 10+14=24, reverse center = 10+14=24
        // Both should give the same result for symmetric weights applied to symmetric-about-center linear data
        double expected = 2.0 * (10.0 + (2.0 * 7.0));
        Assert.Equal(expected, fwdResult.Value + revResult.Value, 1e-6);
    }

    [Fact]
    public void Period4_Sinusoid_Suppressed()
    {
        // Spencer filter zeros out period-4 signals
        var sp15 = new Sp15();
        const int n = 60;
        for (int i = 0; i < n; i++)
        {
            // Pure period-4 sinusoid centered at 100
            double val = 100.0 + (10.0 * Math.Sin(2.0 * Math.PI * i / 4.0));
            sp15.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }
        // After warmup, the output should be ~100 (sinusoid suppressed)
        Assert.Equal(100.0, sp15.Last.Value, 0.5);
    }

    [Fact]
    public void Period5_Sinusoid_Suppressed()
    {
        // Spencer filter zeros out period-5 signals
        var sp15 = new Sp15();
        const int n = 60;
        for (int i = 0; i < n; i++)
        {
            double val = 100.0 + (10.0 * Math.Sin(2.0 * Math.PI * i / 5.0));
            sp15.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }
        Assert.Equal(100.0, sp15.Last.Value, 0.5);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentResults()
    {
        var source1 = new TSeries();
        var gbm1 = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < 30; i++)
        {
            source1.Add(gbm1.Next().C);
        }

        var source2 = new TSeries();
        var gbm2 = new GBM(startPrice: 100, seed: 99);
        for (int i = 0; i < 30; i++)
        {
            source2.Add(gbm2.Next().C);
        }

        var batch1 = Sp15.Batch(source1);
        var batch2 = Sp15.Batch(source2);

        // At least one value should differ
        bool anyDifferent = false;
        for (int i = 15; i < 30; i++)
        {
            if (Math.Abs(batch1[i].Value - batch2[i].Value) > 1e-6)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void LargeDataset_Consistency()
    {
        var source = MakeSeries(1000);
        var sp15 = new Sp15();
        var streaming = new double[1000];
        for (int i = 0; i < 1000; i++)
        {
            streaming[i] = sp15.Update(source[i]).Value;
        }

        double[] spanOut = new double[1000];
        Sp15.Batch(source.Values, spanOut);

        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(streaming[i], spanOut[i], 1e-10);
        }
    }
}
