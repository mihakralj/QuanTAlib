namespace QuanTAlib.Tests;

public class NwTests
{
    private readonly GBM _gbm = new(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
    private readonly TSeries _data;

    public NwTests()
    {
        _data = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    // ── A) Constructor validation ────────────────────────────────────────

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Nw(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Nw(-5));
    }

    [Fact]
    public void Constructor_ZeroBandwidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Nw(10, 0.0));
    }

    [Fact]
    public void Constructor_NegativeBandwidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Nw(10, -1.0));
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var nw = new Nw(20, 5.0);
        Assert.Equal(20, nw.Period);
        Assert.Equal(5.0, nw.Bandwidth);
        Assert.Contains("Nw(", nw.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var nw = new Nw();
        Assert.Equal(64, nw.Period);
        Assert.Equal(8.0, nw.Bandwidth);
    }

    [Fact]
    public void Constructor_PeriodOne_Succeeds()
    {
        var nw = new Nw(1, 1.0);
        Assert.Equal(1, nw.Period);
    }

    // ── B) Basic calculation ─────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValue()
    {
        var nw = new Nw(10, 3.0);
        TValue result = nw.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.False(double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_ConstantInput_ReturnsConstant()
    {
        var nw = new Nw(10, 3.0);
        for (int i = 0; i < 50; i++)
        {
            nw.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, nw.Last.Value, 10);
    }

    [Fact]
    public void Update_FirstBar_ReturnsInput()
    {
        var nw = new Nw(10, 3.0);
        TValue result = nw.Update(new TValue(DateTime.UtcNow, 50.0));
        // First bar: only one sample, weight is 1.0, so result = input
        Assert.Equal(50.0, result.Value, 10);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var nw = new Nw(10, 3.0);
        nw.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(nw.Update(new TValue(DateTime.UtcNow, 200.0)).Value, nw.Last.Value);
    }

    // ── C) State + bar correction ────────────────────────────────────────

    [Fact]
    public void Update_IsNew_Advances()
    {
        var nw = new Nw(10, 3.0);
        nw.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        nw.Update(new TValue(DateTime.UtcNow, 200.0), isNew: true);
        double after2 = nw.Last.Value;
        // Two distinct bars processed, result should differ from single bar
        Assert.NotEqual(100.0, after2);
    }

    [Fact]
    public void Update_IsNewFalse_Rewrites()
    {
        var nw = new Nw(10, 3.0);
        nw.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        nw.Update(new TValue(DateTime.UtcNow, 200.0), isNew: true);
        double before = nw.Last.Value;
        nw.Update(new TValue(DateTime.UtcNow, 150.0), isNew: false);
        double after = nw.Last.Value;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Update_IterativeCorrections_Restore()
    {
        var nw = new Nw(10, 3.0);
        for (int i = 0; i < 20; i++)
        {
            nw.Update(new TValue(DateTime.UtcNow, 100.0 + i), isNew: true);
        }
        double baseline = nw.Last.Value;

        // Correct last bar multiple times, then restore original value
        nw.Update(new TValue(DateTime.UtcNow, 999.0), isNew: false);
        nw.Update(new TValue(DateTime.UtcNow, 119.0), isNew: false); // original value
        double restored = nw.Last.Value;

        Assert.Equal(baseline, restored, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var nw = new Nw(10, 3.0);
        for (int i = 0; i < 20; i++)
        {
            nw.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(nw.IsHot);
        nw.Reset();
        Assert.False(nw.IsHot);
    }

    // ── D) Warmup / convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        int period = 10;
        var nw = new Nw(period, 3.0);
        for (int i = 0; i < period; i++)
        {
            if (i < period - 1)
            {
                Assert.False(nw.IsHot);
            }
            nw.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(nw.IsHot);
    }

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var nw = new Nw(20, 5.0);
        for (int i = 0; i < 5; i++)
        {
            nw.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        Assert.False(nw.IsHot);
    }

    // ── E) Robustness ────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var nw = new Nw(10, 3.0);
        nw.Update(new TValue(DateTime.UtcNow, 100.0));
        nw.Update(new TValue(DateTime.UtcNow, 100.0));
        TValue result = nw.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.False(double.IsNaN(result.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var nw = new Nw(10, 3.0);
        nw.Update(new TValue(DateTime.UtcNow, 100.0));
        TValue result = nw.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.False(double.IsInfinity(result.Value));
    }

    [Fact]
    public void Update_BatchNaN_Safe()
    {
        var nw = new Nw(10, 3.0);
        for (int i = 0; i < 5; i++)
        {
            nw.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        for (int i = 0; i < 5; i++)
        {
            TValue r = nw.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.False(double.IsNaN(r.Value));
        }
    }

    // ── F) Consistency (4 API modes) ─────────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        double bw = 3.0;
        var series = _data;

        // Mode 1: Static Batch(TSeries)
        var batchResult = Nw.Batch(series, period, bw);

        // Mode 2: Static Batch(Span)
        double[] spanResult = new double[series.Count];
        Nw.Batch(series.Values, spanResult.AsSpan(), period, bw);

        // Mode 3: Instance Update(TSeries)
        var instance = new Nw(period, bw);
        var tseriesResult = instance.Update(series);

        // Mode 4: Streaming Update(TValue)
        var streamingInstance = new Nw(period, bw);
        double[] streamResult = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResult[i] = streamingInstance.Update(new TValue(series.Times[i], series.Values[i])).Value;
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanResult[i], 10);
            Assert.Equal(batchResult[i].Value, tseriesResult[i].Value, 10);
            Assert.Equal(batchResult[i].Value, streamResult[i], 10);
        }
    }

    // ── G) Span API tests ────────────────────────────────────────────────

    [Fact]
    public void Batch_LengthMismatch_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[5];
        Assert.Throws<ArgumentException>(() => Nw.Batch(src, dst, 5, 2.0));
    }

    [Fact]
    public void Batch_InvalidPeriod_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[10];
        Assert.Throws<ArgumentOutOfRangeException>(() => Nw.Batch(src, dst, 0, 2.0));
    }

    [Fact]
    public void Batch_InvalidBandwidth_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[10];
        Assert.Throws<ArgumentOutOfRangeException>(() => Nw.Batch(src, dst, 5, 0.0));
    }

    [Fact]
    public void Batch_Empty_Succeeds()
    {
        double[] src = Array.Empty<double>();
        double[] dst = Array.Empty<double>();
        Nw.Batch(src, dst, 5, 2.0);
        Assert.Empty(dst);
    }

    [Fact]
    public void Batch_MatchesTSeries()
    {
        int period = 10;
        double bw = 3.0;
        var series = _data;

        var tseriesResult = Nw.Batch(series, period, bw);

        double[] spanOut = new double[series.Count];
        Nw.Batch(series.Values, spanOut.AsSpan(), period, bw);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanOut[i], 10);
        }
    }

    [Fact]
    public void Batch_NaN_Safe()
    {
        double[] src = { 1.0, 2.0, double.NaN, 4.0, 5.0 };
        double[] dst = new double[5];
        Nw.Batch(src, dst, 3, 1.0);
        for (int i = 0; i < 5; i++)
        {
            Assert.False(double.IsNaN(dst[i]));
        }
    }

    [Fact]
    public void Batch_LargeData_NoStackOverflow()
    {
        int len = 10000;
        double[] src = new double[len];
        double[] dst = new double[len];
        for (int i = 0; i < len; i++)
        {
            src[i] = 100.0 + i * 0.01;
        }
        Nw.Batch(src, dst, 500, 50.0); // period > StackallocThreshold
        Assert.False(double.IsNaN(dst[len - 1]));
    }

    // ── H) Chainability ──────────────────────────────────────────────────

    [Fact]
    public void Pub_Fires()
    {
        var nw = new Nw(10, 3.0);
        int count = 0;
        nw.Pub += (object? _, in TValueEventArgs _) => count++;
        nw.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Constructor_WithPublisher_Subscribes()
    {
        var source = new TSeries();
        var nw = new Nw(source, 10, 3.0);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.False(double.IsNaN(nw.Last.Value));
    }

    [Fact]
    public void Dispose_WithPublisher_Unsubscribes()
    {
        var source = new TSeries();
        var nw = new Nw(source, 10, 3.0);
        double lastBefore = nw.Last.Value;
        nw.Dispose();
        source.Add(new TValue(DateTime.UtcNow, 200.0));
        Assert.Equal(lastBefore, nw.Last.Value);
    }

    [Fact]
    public void Dispose_WithoutPublisher_DoesNotThrow()
    {
        var nw = new Nw(10, 3.0);
        double lastBefore = nw.Last.Value;
        nw.Dispose();
        Assert.Equal(lastBefore, nw.Last.Value);
    }

    // ── Additional: Filter behavior ──────────────────────────────────────

    [Fact]
    public void SmallBandwidth_TracksPrice()
    {
        // Small h → tight kernel → output close to most recent value
        var nw = new Nw(20, 0.5);
        for (int i = 0; i < 30; i++)
        {
            nw.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        nw.Update(new TValue(DateTime.UtcNow, 200.0));
        // With h=0.5 and period=20, weights decay fast — output dominated by newest bar
        Assert.True(nw.Last.Value > 150.0);
    }

    [Fact]
    public void LargeBandwidth_Smooths()
    {
        // Large h → wide kernel → output close to average
        var nw = new Nw(20, 100.0);
        for (int i = 0; i < 20; i++)
        {
            nw.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        nw.Update(new TValue(DateTime.UtcNow, 200.0), isNew: true);
        // With h=100 and period=20, all weights nearly equal → nearly SMA
        double expected = (100.0 * 19 + 200.0) / 20.0; // ~105
        Assert.True(Math.Abs(nw.Last.Value - expected) < 5.0);
    }

    [Fact]
    public void DifferentBandwidths_ProduceDifferentOutputs()
    {
        var narrow = new Nw(20, 1.0);
        var wide = new Nw(20, 50.0);

        for (int i = 0; i < 20; i++)
        {
            double v = 100.0 + i;
            narrow.Update(new TValue(DateTime.UtcNow, v));
            wide.Update(new TValue(DateTime.UtcNow, v));
        }

        Assert.NotEqual(narrow.Last.Value, wide.Last.Value, 5);
    }

    [Fact]
    public void TwoInstances_SameInput_SameOutput()
    {
        var n1 = new Nw(10, 3.0);
        var n2 = new Nw(10, 3.0);
        for (int i = 0; i < 30; i++)
        {
            double v = 100.0 + Math.Sin(i * 0.3) * 10.0;
            n1.Update(new TValue(DateTime.UtcNow, v));
            n2.Update(new TValue(DateTime.UtcNow, v));
        }
        Assert.Equal(n1.Last.Value, n2.Last.Value, 12);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var (results, indicator) = Nw.Calculate(_data, 10, 3.0);
        Assert.Equal(_data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var nw = new Nw(10, 3.0);
        var empty = new TSeries();
        var result = nw.Update(empty);
        Assert.Empty(result);
    }

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var nw = new Nw(10, 3.0);
        double[] data = new double[15];
        for (int i = 0; i < 15; i++)
        {
            data[i] = 100.0 + i;
        }
        nw.Prime(data);
        Assert.True(nw.IsHot);
    }
}
