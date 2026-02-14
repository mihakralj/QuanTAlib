namespace QuanTAlib.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
// A) Constructor validation
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanConstructorTests
{
    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var g = new Geomean(14);
        Assert.Equal("Geomean(14)", g.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsWarmupPeriod()
    {
        var g = new Geomean(20);
        Assert.Equal(20, g.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Geomean(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Geomean(-5));
        Assert.Equal("period", ex.ParamName);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// B) Basic calculation
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var g = new Geomean(5);
        TValue result = g.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var g = new Geomean(5);
        g.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, g.Last.Value, 10);
    }

    [Fact]
    public void IsHot_IsAccessible()
    {
        var g = new Geomean(5);
        Assert.False(g.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var g = new Geomean(14);
        Assert.Equal("Geomean(14)", g.Name);
    }

    [Fact]
    public void KnownValues_GeomeanOf2_8()
    {
        // GM(2, 8) = sqrt(16) = 4
        var g = new Geomean(2);
        g.Update(new TValue(DateTime.UtcNow, 2.0));
        g.Update(new TValue(DateTime.UtcNow, 8.0));
        Assert.Equal(4.0, g.Last.Value, 10);
    }

    [Fact]
    public void KnownValues_GeomeanOf2_8_4_16()
    {
        // GM(2, 8, 4, 16) = (2*8*4*16)^(1/4) = 1024^(1/4) = 4*sqrt(2) ≈ 5.6569
        var g = new Geomean(4);
        g.Update(new TValue(DateTime.UtcNow, 2.0));
        g.Update(new TValue(DateTime.UtcNow, 8.0));
        g.Update(new TValue(DateTime.UtcNow, 4.0));
        g.Update(new TValue(DateTime.UtcNow, 16.0));
        Assert.Equal(4.0 * Math.Sqrt(2.0), g.Last.Value, 10);
    }

    [Fact]
    public void KnownValues_AllEqual()
    {
        // GM of identical values = that value
        var g = new Geomean(5);
        for (int i = 0; i < 5; i++)
        {
            g.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, g.Last.Value, 10);
    }

    [Fact]
    public void GeomeanAlwaysLessOrEqualArithmeticMean()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var g = new Geomean(20);
        var sma = new Sma(20);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);
            g.Update(tv);
            sma.Update(tv);
            if (g.IsHot)
            {
                Assert.True(g.Last.Value <= sma.Last.Value + 1e-10,
                    $"GM {g.Last.Value} > AM {sma.Last.Value} at bar {i}");
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// C) State + bar correction
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanStateCorrectionTests
{
    [Fact]
    public void IsNewTrue_AdvancesState()
    {
        var g = new Geomean(5);
        g.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        double v1 = g.Last.Value;
        g.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        double v2 = g.Last.Value;
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IsNewFalse_RewritesLastBar()
    {
        var g = new Geomean(5);
        g.Update(new TValue(DateTime.UtcNow, 10.0), isNew: true);
        g.Update(new TValue(DateTime.UtcNow, 20.0), isNew: true);
        double v1 = g.Last.Value;
        g.Update(new TValue(DateTime.UtcNow, 30.0), isNew: false);
        double v2 = g.Last.Value;
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginal()
    {
        var g = new Geomean(10);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            g.Update(new TValue(bar.Time, bar.Close));
        }

        // Push a new bar
        var newBar = gbm.Next(isNew: true);
        var newTv = new TValue(newBar.Time, newBar.Close);
        g.Update(newTv);
        double original = g.Last.Value;

        // Overwrite 5 times
        for (int c = 0; c < 5; c++)
        {
            g.Update(new TValue(DateTime.UtcNow, 100.0 + c), isNew: false);
        }

        // Rewrite back to original value
        g.Update(newTv, isNew: false);
        Assert.Equal(original, g.Last.Value, 8);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var g = new Geomean(5);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            g.Update(new TValue(bar.Time, bar.Close));
        }
        Assert.True(g.IsHot);

        g.Reset();
        Assert.False(g.IsHot);
        Assert.Equal(default, g.Last);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// D) Warmup / convergence
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanWarmupTests
{
    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        int period = 10;
        var g = new Geomean(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < period - 1; i++)
        {
            var bar = gbm.Next(isNew: true);
            g.Update(new TValue(bar.Time, bar.Close));
            Assert.False(g.IsHot, $"Should not be hot at bar {i}");
        }

        var lastBar = gbm.Next(isNew: true);
        g.Update(new TValue(lastBar.Time, lastBar.Close));
        Assert.True(g.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesConstructor()
    {
        var g = new Geomean(14);
        Assert.Equal(14, g.WarmupPeriod);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// E) Robustness
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValid()
    {
        var g = new Geomean(5);
        for (int i = 0; i < 5; i++)
        {
            g.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = g.Last.Value;

        g.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(before, g.Last.Value, 10);
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var g = new Geomean(5);
        for (int i = 0; i < 5; i++)
        {
            g.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = g.Last.Value;

        g.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.Equal(before, g.Last.Value, 10);
    }

    [Fact]
    public void NegativeValue_UsesLastValid()
    {
        var g = new Geomean(5);
        for (int i = 0; i < 5; i++)
        {
            g.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = g.Last.Value;

        g.Update(new TValue(DateTime.UtcNow, -5.0));
        Assert.Equal(before, g.Last.Value, 10);
    }

    [Fact]
    public void ZeroValue_UsesLastValid()
    {
        var g = new Geomean(5);
        for (int i = 0; i < 5; i++)
        {
            g.Update(new TValue(DateTime.UtcNow, 10.0));
        }
        double before = g.Last.Value;

        g.Update(new TValue(DateTime.UtcNow, 0.0));
        Assert.Equal(before, g.Last.Value, 10);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// F) Consistency (batch == streaming == span == eventing)
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanConsistencyTests
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
        var gStream = new Geomean(period);
        for (int i = 0; i < series.Count; i++)
        {
            gStream.Update(series[i]);
        }

        // Batch
        var batchResult = Geomean.Batch(series, period);
        Assert.Equal(gStream.Last.Value, batchResult[^1].Value, 8);
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
        var batchResult = Geomean.Batch(series, period);

        var src = series.Values;
        Span<double> output = new double[dataLen];
        Geomean.Batch(src, output, period);

        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 8);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// G) Span API tests
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanSpanTests
{
    [Fact]
    public void Batch_MismatchedLengths_Throws()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Geomean.Batch(src, output, 2));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ZeroPeriod_Throws()
    {
        var src = new double[] { 1, 2, 3 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Geomean.Batch(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_NaN_HandledGracefully()
    {
        var src = new double[] { 10, 20, double.NaN, 30, 40 };
        var output = new double[5];
        Geomean.Batch(src, output, 3);

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
        Geomean.Batch(src, output, 300);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Batch_KnownValues()
    {
        // GM(2, 8) = 4, GM(8, 4) = sqrt(32) ≈ 5.6569, GM(4, 16) = 8
        var src = new double[] { 2, 8, 4, 16 };
        var output = new double[4];
        Geomean.Batch(src, output, 2);

        Assert.Equal(2.0, output[0], 10);             // only 1 value → GM = 2
        Assert.Equal(4.0, output[1], 10);              // GM(2,8) = 4
        Assert.Equal(Math.Sqrt(32.0), output[2], 10);  // GM(8,4) = sqrt(32)
        Assert.Equal(8.0, output[3], 10);              // GM(4,16) = 8
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// H) Chainability / Events
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class GeomeanEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var g = new Geomean(5);
        int fireCount = 0;
        g.Pub += (object? sender, in TValueEventArgs args) => { fireCount++; };
        g.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var g1 = new Geomean(source, 5);
        int fireCount = 0;
        g1.Pub += (object? sender, in TValueEventArgs args) => { fireCount++; };

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

public sealed class GeomeanCalculateTests
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
        var (results, indicator) = Geomean.Calculate(series, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(50, results.Count);
    }
}
