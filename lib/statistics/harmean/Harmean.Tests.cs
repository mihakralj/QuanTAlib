namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
// A) Constructor validation
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanConstructorTests
{
    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var h = new Harmean(14);
        Assert.Equal("Harmean(14)", h.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsWarmupPeriod()
    {
        var h = new Harmean(20);
        Assert.Equal(20, h.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Harmean(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Harmean(-5));
        Assert.Equal("period", ex.ParamName);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// B) Basic calculation
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var h = new Harmean(5);
        TValue result = h.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var h = new Harmean(5);
        h.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, h.Last.Value, 10);
    }

    [Fact]
    public void IsHot_IsAccessible()
    {
        var h = new Harmean(5);
        Assert.False(h.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var h = new Harmean(14);
        Assert.Equal("Harmean(14)", h.Name);
    }

    [Fact]
    public void KnownValues_HarmeanOf2_8()
    {
        // HM(2, 8) = 2 / (1/2 + 1/8) = 2 / (5/8) = 16/5 = 3.2
        var h = new Harmean(2);
        h.Update(new TValue(DateTime.UtcNow, 2.0));
        h.Update(new TValue(DateTime.UtcNow, 8.0));
        Assert.Equal(16.0 / 5.0, h.Last.Value, 10);
    }

    [Fact]
    public void KnownValues_HarmeanOf2_4_8()
    {
        // HM(2, 4, 8) = 3 / (1/2 + 1/4 + 1/8) = 3 / (7/8) = 24/7 ≈ 3.4286
        var h = new Harmean(3);
        h.Update(new TValue(DateTime.UtcNow, 2.0));
        h.Update(new TValue(DateTime.UtcNow, 4.0));
        h.Update(new TValue(DateTime.UtcNow, 8.0));
        Assert.Equal(24.0 / 7.0, h.Last.Value, 10);
    }

    [Fact]
    public void KnownValues_AllEqual()
    {
        // HM of identical values = that value
        var h = new Harmean(5);
        for (int i = 0; i < 5; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, h.Last.Value, 10);
    }

    [Fact]
    public void HarmeanAlwaysLessOrEqualGeometricMean()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var h = new Harmean(20);
        var g = new Geomean(20);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            h.Update(tv);
            g.Update(tv);
            if (h.IsHot && g.IsHot)
            {
                Assert.True(h.Last.Value <= g.Last.Value + 1e-10,
                    $"HM {h.Last.Value} > GM {g.Last.Value} at bar {i}");
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// C) State + bar correction
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanStateCorrectionTests
{
    [Fact]
    public void IsNewTrue_AdvancesState()
    {
        var h = new Harmean(5);
        h.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        double v1 = h.Last.Value;
        h.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        double v2 = h.Last.Value;
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IsNewFalse_RewritesLastBar()
    {
        var h = new Harmean(5);
        h.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        h.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        double v1 = h.Last.Value;
        h.Update(new TValue(DateTime.UtcNow, 30.0), isNew: false);
        double v2 = h.Last.Value;
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginal()
    {
        var h = new Harmean(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
        }

        // Push a new bar
        var newBar = gbm.Next(isNew: true);
        var newTv = new TValue(newBar.Time, newBar.Close);
        h.Update(newTv);
        double original = h.Last.Value;

        // Overwrite 5 times
        for (int c = 0; c < 5; c++)
        {
            h.Update(new TValue(DateTime.UtcNow, 100.0 + c), isNew: false);
        }

        // Rewrite back to original value
        h.Update(newTv, isNew: false);
        Assert.Equal(original, h.Last.Value, 8);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var h = new Harmean(5);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
        }
        Assert.True(h.IsHot);

        h.Reset();
        Assert.False(h.IsHot);
        Assert.Equal(default, h.Last);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// D) Warmup / convergence
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanWarmupTests
{
    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        int period = 10;
        var h = new Harmean(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < period - 1; i++)
        {
            var bar = gbm.Next(isNew: true);
            h.Update(new TValue(bar.Time, bar.Close));
            Assert.False(h.IsHot, $"Should not be hot at bar {i}");
        }

        var lastBar = gbm.Next(isNew: true);
        h.Update(new TValue(lastBar.Time, lastBar.Close));
        Assert.True(h.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesConstructor()
    {
        var h = new Harmean(14);
        Assert.Equal(14, h.WarmupPeriod);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// E) Robustness
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValid()
    {
        var h = new Harmean(5);
        for (int i = 0; i < 5; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = h.Last.Value;

        h.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(before, h.Last.Value, 10);
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var h = new Harmean(5);
        for (int i = 0; i < 5; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = h.Last.Value;

        h.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.Equal(before, h.Last.Value, 10);
    }

    [Fact]
    public void NegativeValue_UsesLastValid()
    {
        var h = new Harmean(5);
        for (int i = 0; i < 5; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = h.Last.Value;

        h.Update(new TValue(DateTime.UtcNow, -5.0));
        Assert.Equal(before, h.Last.Value, 10);
    }

    [Fact]
    public void ZeroValue_UsesLastValid()
    {
        var h = new Harmean(5);
        for (int i = 0; i < 5; i++)
        {
            h.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = h.Last.Value;

        h.Update(new TValue(DateTime.UtcNow, 0.0));
        Assert.Equal(before, h.Last.Value, 10);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// F) Consistency (batch == streaming == span == eventing)
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanConsistencyTests
{
    [Fact]
    public void BatchCalc_MatchesStreaming()
    {
        int period = 14;
        int dataLen = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        var times = new List<long>(dataLen);
        var values = new List<double>(dataLen);

        for (int i = 0; i < dataLen; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }

        var series = new TSeries(times, values);

        // Streaming
        var hStream = new Harmean(period);
        for (int i = 0; i < series.Count; i++)
        {
            hStream.Update(series[i]);
        }

        // Batch
        var batchResult = Harmean.Batch(series, period);
        Assert.Equal(hStream.Last.Value, batchResult[^1].Value, 8);
    }

    [Fact]
    public void SpanCalc_MatchesTSeries()
    {
        int period = 14;
        int dataLen = 200;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);

        var times = new List<long>(dataLen);
        var values = new List<double>(dataLen);

        for (int i = 0; i < dataLen; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }

        var series = new TSeries(times, values);
        var batchResult = Harmean.Batch(series, period);

        var src = series.Values;
        Span<double> output = new double[dataLen];
        Harmean.Batch(src, output, period);

        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 8);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// G) Span API tests
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanSpanTests
{
    [Fact]
    public void Batch_MismatchedLengths_Throws()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Harmean.Batch(src, output, 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ZeroPeriod_Throws()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Harmean.Batch(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_NaN_HandledGracefully()
    {
        var src = new double[] { 10, 20, double.NaN, 30, 40 };
        var output = new double[5];
        Harmean.Batch(src, output, 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] is not finite: {output[i]}");
        }
    }

    [Fact]
    public void Batch_LargeData_NoStackOverflow()
    {
        int len = 10_000;
        var src = new double[len];
        var output = new double[len];
        for (int i = 0; i < len; i++)
        {
            src[i] = 100.0 + (i % 50);
        }
        Harmean.Batch(src, output, 300);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Batch_KnownValues()
    {
        // HM(2) = 2, HM(2,8) = 16/5 = 3.2, HM(8,4) = 2/(1/8+1/4) = 2/(3/8) = 16/3, HM(4,16) = 2/(1/4+1/16) = 2/(5/16) = 32/5
        var src = new double[] { 2, 8, 4, 16 };
        var output = new double[4];
        Harmean.Batch(src, output, 2);

        Assert.Equal(2.0, output[0], 10);                // only 1 value → HM = 2
        Assert.Equal(16.0 / 5.0, output[1], 10);          // HM(2,8) = 3.2
        Assert.Equal(16.0 / 3.0, output[2], 10);          // HM(8,4) = 16/3
        Assert.Equal(32.0 / 5.0, output[3], 10);          // HM(4,16) = 6.4
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// H) Chainability / Events
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var h = new Harmean(5);
        int fireCount = 0;
        h.Pub += (object? sender, in TValueEventArgs args) => { fireCount++; };
        h.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var h1 = new Harmean(source, 5);
        int fireCount = 0;
        h1.Pub += (object? sender, in TValueEventArgs args) => { fireCount++; };

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 10.0 + i));
        }

        Assert.Equal(10, fireCount);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// I) Calculate() returns hot indicator
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class HarmeanCalculateTests
{
    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var times = new List<long>(50);
        var values = new List<double>(50);
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            times.Add(bar.Time);
            values.Add(bar.Close);
        }

        var series = new TSeries(times, values);
        var (results, indicator) = Harmean.Calculate(series, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(50, results.Count);
    }
}
