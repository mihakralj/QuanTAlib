namespace QuanTAlib.Validation;

public sealed class TheilValidationTests
{
    [Fact]
    public void EqualValues_PerfectEquality_ReturnsZero()
    {
        // When all values are identical, Theil T must be exactly 0
        var t = new Theil(10);
        for (int i = 0; i < 10; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 42.0));
        }
        Assert.Equal(0.0, t.Last.Value, 1e-12);
    }

    [Fact]
    public void ScaleInvariance_Property()
    {
        // T(c*x) = T(x) for any positive constant c
        int period = 10;
        var gbm = new GBM(100, 0.05, 0.2, seed: 123);
        double[] prices = new double[period];
        for (int i = 0; i < period; i++)
        {
            prices[i] = gbm.Next().Close;
        }

        var t1 = new Theil(period);
        var t2 = new Theil(period);
        for (int i = 0; i < period; i++)
        {
            t1.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
            t2.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i] * 1000.0));
        }

        Assert.Equal(t1.Last.Value, t2.Last.Value, 1e-10);
    }

    [Fact]
    public void NonNegativity_Property()
    {
        // Theil T Index is always >= 0
        var gbm = new GBM(100, 0.05, 0.2, seed: 456);
        var t = new Theil(20);
        for (int i = 0; i < 100; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), gbm.Next().Close));
            if (t.IsHot)
            {
                Assert.True(t.Last.Value >= -1e-12, $"Theil should be non-negative, got {t.Last.Value}");
            }
        }
    }

    [Fact]
    public void StreamingMatchesBatch()
    {
        int period = 10;
        int dataLen = 50;
        var gbm = new GBM(100, 0.05, 0.2, seed: 789);
        var series = new TSeries();
        for (int i = 0; i < dataLen; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddSeconds(i), gbm.Next().Close));
        }

        // Batch
        var batchResult = Theil.Batch(series, period);

        // Streaming
        var streaming = new Theil(period);
        for (int i = 0; i < dataLen; i++)
        {
            streaming.Update(series[i]);
            Assert.Equal(batchResult[i].Value, streaming.Last.Value, 1e-10);
        }
    }

    [Fact]
    public void SpanMatchesStreaming()
    {
        int period = 10;
        int dataLen = 50;
        var gbm = new GBM(100, 0.05, 0.2, seed: 101);
        double[] values = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            values[i] = gbm.Next().Close;
        }

        double[] spanOut = new double[dataLen];
        Theil.Batch(values.AsSpan(), spanOut.AsSpan(), period);

        var streaming = new Theil(period);
        for (int i = 0; i < dataLen; i++)
        {
            streaming.Update(new TValue(DateTime.UtcNow.AddSeconds(i), values[i]));
            Assert.Equal(spanOut[i], streaming.Last.Value, 1e-10);
        }
    }

    [Fact]
    public void HigherInequality_ProducesHigherTheil()
    {
        // A more concentrated distribution should produce a higher Theil T
        var tUniform = new Theil(5);
        double[] uniform = [10, 11, 12, 13, 14]; // roughly equal
        for (int i = 0; i < 5; i++)
        {
            tUniform.Update(new TValue(DateTime.UtcNow.AddSeconds(i), uniform[i]));
        }

        var tConcentrated = new Theil(5);
        double[] concentrated = [1, 1, 1, 1, 100]; // highly unequal
        for (int i = 0; i < 5; i++)
        {
            tConcentrated.Update(new TValue(DateTime.UtcNow.AddSeconds(i), concentrated[i]));
        }

        Assert.True(tConcentrated.Last.Value > tUniform.Last.Value);
    }

    [Fact]
    public void ManualComputation_FourValues()
    {
        // x = [2, 4, 6, 8], mean = 5
        // ratios: 0.4, 0.8, 1.2, 1.6
        // T = (1/4)[0.4*ln(0.4) + 0.8*ln(0.8) + 1.2*ln(1.2) + 1.6*ln(1.6)]
        double mean = 5.0;
        double[] x = [2, 4, 6, 8];
        double theilSum = 0;
        for (int i = 0; i < 4; i++)
        {
            double ratio = x[i] / mean;
            theilSum += ratio * Math.Log(ratio);
        }
        double expected = theilSum / 4.0;

        var t = new Theil(4);
        for (int i = 0; i < 4; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), x[i]));
        }

        Assert.Equal(expected, t.Last.Value, 1e-10);
    }
}
