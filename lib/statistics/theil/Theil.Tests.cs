namespace QuanTAlib.Tests;

public class TheilTests
{
    [Fact]
    public void Constructor_DefaultPeriod_SetsName()
    {
        var t = new Theil(14);
        Assert.Equal("Theil(14)", t.Name);
        Assert.Equal(14, t.WarmupPeriod);
    }

    [Fact]
    public void Constructor_PeriodLessThan2_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Theil(1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Theil(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void EqualValues_ReturnsZero()
    {
        var t = new Theil(5);
        for (int i = 0; i < 5; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 10.0));
        }
        Assert.Equal(0.0, t.Last.Value, 1e-10);
    }

    [Fact]
    public void UnequalValues_ReturnsPositive()
    {
        var t = new Theil(5);
        double[] vals = [1, 2, 3, 4, 5];
        for (int i = 0; i < 5; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        Assert.True(t.Last.Value > 0);
    }

    [Fact]
    public void HighInequality_LargerTheil()
    {
        // Uniform values → low Theil; highly skewed → high Theil
        var tLow = new Theil(4);
        double[] uniform = [10, 10, 10, 10];
        for (int i = 0; i < 4; i++)
        {
            tLow.Update(new TValue(DateTime.UtcNow.AddSeconds(i), uniform[i]));
        }

        var tHigh = new Theil(4);
        double[] skewed = [1, 1, 1, 100];
        for (int i = 0; i < 4; i++)
        {
            tHigh.Update(new TValue(DateTime.UtcNow.AddSeconds(i), skewed[i]));
        }

        Assert.True(tHigh.Last.Value > tLow.Last.Value);
    }

    [Fact]
    public void KnownValues_ManualComputation()
    {
        // x = [1, 2, 3], mean = 2
        // ratios: 0.5, 1.0, 1.5
        // contributions: 0.5*ln(0.5) + 1.0*ln(1.0) + 1.5*ln(1.5)
        //              = 0.5*(-0.6931) + 0 + 1.5*(0.4055)
        //              = -0.3466 + 0 + 0.6082 = 0.2616
        // T = 0.2616 / 3 = 0.08720
        var t = new Theil(3);
        t.Update(new TValue(DateTime.UtcNow, 1.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 2.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(2), 3.0));

        double expected = ((0.5 * Math.Log(0.5)) + (1.0 * Math.Log(1.0)) + (1.5 * Math.Log(1.5))) / 3.0;
        Assert.Equal(expected, t.Last.Value, 1e-10);
    }

    [Fact]
    public void ScaleInvariance_SameTheil()
    {
        // Multiplying all values by a constant should not change Theil
        var t1 = new Theil(4);
        var t2 = new Theil(4);
        double[] vals = [1, 2, 3, 4];
        for (int i = 0; i < 4; i++)
        {
            t1.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
            t2.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i] * 100.0));
        }
        Assert.Equal(t1.Last.Value, t2.Last.Value, 1e-10);
    }

    [Fact]
    public void IsHot_BecomesTrue_WhenBufferFull()
    {
        var t = new Theil(3);
        Assert.False(t.IsHot);
        t.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.False(t.IsHot);
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 20.0));
        Assert.False(t.IsHot);
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(2), 30.0));
        Assert.True(t.IsHot);
    }

    [Fact]
    public void IsNewFalse_CorrectsBars()
    {
        var t = new Theil(5);
        for (int i = 1; i <= 5; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), (double)i * 10));
        }

        double original = t.Last.Value;

        // Correct with very different value
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(5), 1000.0), isNew: false);
        double corrected = t.Last.Value;
        Assert.NotEqual(original, corrected);

        // Correct back
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(5), 50.0), isNew: false);
        double restored = t.Last.Value;
        Assert.Equal(original, restored, 1e-10);
    }

    [Fact]
    public void NaN_SubstitutesLastValid()
    {
        var t = new Theil(3);
        t.Update(new TValue(DateTime.UtcNow, 10.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 20.0));

        // Feed NaN — should use last valid value (20.0)
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(2), double.NaN));

        // The result should still be finite
        Assert.True(double.IsFinite(t.Last.Value));
    }

    [Fact]
    public void Infinity_SubstitutesLastValid()
    {
        var t = new Theil(3);
        t.Update(new TValue(DateTime.UtcNow, 10.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 20.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(2), double.PositiveInfinity));
        Assert.True(double.IsFinite(t.Last.Value));
    }

    [Fact]
    public void NegativeValues_SubstitutesLastValid()
    {
        var t = new Theil(3);
        t.Update(new TValue(DateTime.UtcNow, 10.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 20.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(2), -5.0));
        Assert.True(double.IsFinite(t.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var t = new Theil(3);
        for (int i = 1; i <= 3; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), (double)i * 10));
        }
        Assert.True(t.IsHot);

        t.Reset();
        Assert.False(t.IsHot);
        Assert.Equal(default, t.Last);
    }

    [Fact]
    public void BatchTSeries_MatchesStreaming()
    {
        int period = 5;
        int dataLen = 50;
        var gbm = new GBM(100, 0.05, 0.2, seed: 42);
        var series = new TSeries();
        for (int i = 0; i < dataLen; i++)
        {
            var bar = gbm.Next();
            series.Add(new TValue(bar.Time, bar.Close));
        }

        // Batch
        var batchResult = Theil.Batch(series, period);

        // Streaming
        var streaming = new Theil(period);
        var streamResult = new TSeries();
        for (int i = 0; i < dataLen; i++)
        {
            streamResult.Add(streaming.Update(series[i]));
        }

        Assert.Equal(batchResult.Count, streamResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamResult[i].Value, 1e-10);
        }
    }

    [Fact]
    public void BatchSpan_MatchesStreaming()
    {
        int period = 5;
        int dataLen = 50;
        var gbm = new GBM(100, 0.05, 0.2, seed: 42);
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
    public void BatchSpan_LengthMismatch_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Theil.Batch(src.AsSpan(), output.AsSpan(), 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_InvalidPeriod_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Theil.Batch(src.AsSpan(), output.AsSpan(), 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void BatchSpan_NaN_HandledSafely()
    {
        double[] src = [10, 20, double.NaN, 30, 40];
        double[] output = new double[5];
        Theil.Batch(src.AsSpan(), output.AsSpan(), 3);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void EventChaining_Fires()
    {
        var t = new Theil(3);
        int eventCount = 0;
        t.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 1; i <= 5; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), (double)i * 10));
        }

        Assert.Equal(5, eventCount);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 10; i++)
        {
            series.Add(new TValue(DateTime.UtcNow.AddSeconds(i), (double)i * 10));
        }

        var (results, indicator) = Theil.Calculate(series, 5);
        Assert.Equal(10, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var t = new Theil(3);
        double[] data = [10, 20, 30, 40, 50];
        t.Prime(data);
        Assert.True(t.IsHot);
        Assert.True(double.IsFinite(t.Last.Value));
    }

    [Fact]
    public void SingleValue_ReturnsZero()
    {
        var t = new Theil(5);
        t.Update(new TValue(DateTime.UtcNow, 42.0));
        // Only 1 value → should be 0 (can't compute inequality from 1 value)
        Assert.Equal(0.0, t.Last.Value, 1e-10);
    }

    [Fact]
    public void TwoEqualValues_ReturnsZero()
    {
        var t = new Theil(2);
        t.Update(new TValue(DateTime.UtcNow, 50.0));
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 50.0));
        Assert.Equal(0.0, t.Last.Value, 1e-10);
    }

    [Fact]
    public void SlidingWindow_DropsOldValues()
    {
        var t = new Theil(3);
        // Fill: [10, 10, 10] → T=0
        for (int i = 0; i < 3; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 10.0));
        }
        Assert.Equal(0.0, t.Last.Value, 1e-10);

        // Add unequal: [10, 10, 100] → T > 0
        t.Update(new TValue(DateTime.UtcNow.AddSeconds(3), 100.0));
        Assert.True(t.Last.Value > 0);
    }

    [Fact]
    public void LargePeriod_WorksWithArrayPool()
    {
        int period = 300;
        var t = new Theil(period);
        for (int i = 0; i < period; i++)
        {
            t.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(t.IsHot);
        Assert.True(double.IsFinite(t.Last.Value));
    }

    [Fact]
    public void LargePeriod_SpanBatch_Works()
    {
        int period = 300;
        int len = 500;
        double[] src = new double[len];
        double[] output = new double[len];
        for (int i = 0; i < len; i++)
        {
            src[i] = 100.0 + i;
        }
        Theil.Batch(src.AsSpan(), output.AsSpan(), period);
        Assert.True(double.IsFinite(output[len - 1]));
    }
}
