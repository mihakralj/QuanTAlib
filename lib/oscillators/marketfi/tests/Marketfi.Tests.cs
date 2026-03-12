using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class MarketfiTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.2, seed: 42);
    private const double Tolerance = 1e-10;

    // ── A) Constructor validation ─────────────────────────────────────────────

    [Fact]
    public void Constructor_Default_SetsName()
    {
        var m = new Marketfi();
        Assert.Equal("Marketfi", m.Name);
    }

    [Fact]
    public void Constructor_Default_WarmupPeriodIsOne()
    {
        Assert.Equal(1, Marketfi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_Default_NotHotBeforeFirstBar()
    {
        var m = new Marketfi();
        Assert.False(m.IsHot);
    }

    // ── B) Basic calculation ──────────────────────────────────────────────────

    [Fact]
    public void Update_BasicBar_CorrectMfi()
    {
        var m = new Marketfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 95.0, 102.0, 1000.0);
        var result = m.Update(bar);
        // MFI = (105 - 95) / 1000 = 0.01
        Assert.Equal(0.01, result.Value, Tolerance);
    }

    [Fact]
    public void Update_FirstBar_IsHot()
    {
        var m = new Marketfi();
        m.Update(new TBar(DateTime.UtcNow, 100.0, 110.0, 90.0, 100.0, 500.0));
        Assert.True(m.IsHot);
    }

    [Fact]
    public void Update_LastMatchesReturnValue()
    {
        var m = new Marketfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 120.0, 80.0, 100.0, 200.0);
        var result = m.Update(bar);
        Assert.Equal(result.Value, m.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_ZeroVolume_ReturnsZero()
    {
        var m = new Marketfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 110.0, 90.0, 100.0, 0.0);
        var result = m.Update(bar);
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ZeroRange_ReturnsZero()
    {
        var m = new Marketfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 100.0, 100.0, 100.0, 1000.0);
        var result = m.Update(bar);
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_KnownValues_MultipleBar()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000));               // MFI = 20/1000 = 0.02
        var r2 = m.Update(new TBar(t.AddMinutes(1), 100, 115, 85, 100, 500));  // MFI = 30/500 = 0.06
        Assert.Equal(0.06, r2.Value, Tolerance);
    }

    [Fact]
    public void Update_NonZeroRange_NonZeroVolume_Positive()
    {
        var m = new Marketfi();
        var result = m.Update(new TBar(DateTime.UtcNow, 100, 115, 85, 100, 400));
        // MFI = 30/400 = 0.075
        Assert.Equal(0.075, result.Value, Tolerance);
        Assert.True(result.Value > 0.0);
    }

    // ── C) State + bar correction ─────────────────────────────────────────────

    [Fact]
    public void Update_IsNewFalse_RewritesLastBar()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000), isNew: true);          // MFI = 0.01

        m.Update(new TBar(t.AddMinutes(1), 100, 112, 88, 100, 800), isNew: true);  // bar 2
        m.Update(new TBar(t.AddMinutes(1), 100, 120, 80, 100, 400), isNew: false); // correction → 40/400 = 0.1

        Assert.Equal(0.1, m.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreCorrectly()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000), isNew: true);

        m.Update(new TBar(t.AddMinutes(1), 100, 112, 88, 100, 800), isNew: true);
        m.Update(new TBar(t.AddMinutes(1), 100, 114, 86, 100, 600), isNew: false);
        m.Update(new TBar(t.AddMinutes(1), 100, 116, 84, 100, 400), isNew: false);
        // MFI = 32/400 = 0.08
        Assert.Equal(0.08, m.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_BarCorrection_PreviousBarRestored()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000), isNew: true);  // MFI = 0.01
        double afterBar1 = m.Last.Value;

        m.Update(new TBar(t.AddMinutes(1), 100, 120, 80, 100, 500), isNew: true);  // new bar
        m.Update(new TBar(t, 100, 110, 90, 100, 1000), isNew: false);              // rollback to bar 1 value
        // After rollback the corrected result should match original bar 1 value
        Assert.Equal(afterBar1, m.Last.Value, Tolerance);
    }

    // ── D) Warmup / convergence ───────────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsOnFirstBar()
    {
        var m = new Marketfi();
        Assert.False(m.IsHot);
        m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 500));
        Assert.True(m.IsHot);
    }

    [Fact]
    public void WarmupPeriod_AlwaysOne()
    {
        Assert.Equal(1, Marketfi.WarmupPeriod);
    }

    [Fact]
    public void Reset_ClearsIsHot()
    {
        var m = new Marketfi();
        m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(m.IsHot);
        m.Reset();
        Assert.False(m.IsHot);
    }

    // ── E) Robustness — NaN / Infinity ────────────────────────────────────────

    [Fact]
    public void Update_NaNVolume_ReturnsZero()
    {
        var m = new Marketfi();
        var r = m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, double.NaN));
        // volume NaN → treated as 0 → MFI = 0
        Assert.Equal(0.0, r.Value, Tolerance);
    }

    [Fact]
    public void Update_InfinityVolume_ReturnsZero()
    {
        var m = new Marketfi();
        var r = m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, double.PositiveInfinity));
        Assert.Equal(0.0, r.Value, Tolerance);
    }

    [Fact]
    public void Update_NaNHigh_ResultIsFinite()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000));  // establishes LastValid
        var r = m.Update(new TBar(t.AddMinutes(1), 100, double.NaN, 90, 100, 500), isNew: true);
        Assert.True(double.IsFinite(r.Value));
    }

    [Fact]
    public void Update_NaNLow_ResultIsFinite()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000));
        var r = m.Update(new TBar(t.AddMinutes(1), 100, 110, double.NaN, 100, 500), isNew: true);
        Assert.True(double.IsFinite(r.Value));
    }

    [Fact]
    public void Update_BatchNaN_NoPropagation()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            m.Update(new TBar(t.AddMinutes(i), 100, 110, 90, 100, 1000));
        }
        m.Update(new TBar(t.AddMinutes(5), 100, double.NaN, double.NaN, 100, 500));
        Assert.True(double.IsFinite(m.Last.Value));
    }

    // ── F) Consistency — all modes match ──────────────────────────────────────

    [Fact]
    public void Consistency_StreamingMatchesBatch()
    {
        const int N = 100;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 42);

        double[] hi = new double[N], lo = new double[N], vol = new double[N];
        double streamResult;

        var mStream = new Marketfi();
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            hi[i] = bar.High;
            lo[i] = bar.Low;
            vol[i] = bar.Volume;
            mStream.Update(bar, isNew: true);
        }
        streamResult = mStream.Last.Value;

        var output = new double[N];
        Marketfi.Batch(hi, lo, vol, output);
        double batchResult = output[N - 1];

        Assert.Equal(streamResult, batchResult, Tolerance);
    }

    [Fact]
    public void Consistency_EventBasedMatchesStreaming()
    {
        const int N = 50;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 7);

        var sourceStream = new TBarSeries();
        var mStream = new Marketfi();

        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            sourceStream.Add(bar);
            mStream.Update(bar, isNew: true);
        }

        var mEvent = new Marketfi(sourceStream);
        Assert.Equal(mStream.Last.Value, mEvent.Last.Value, Tolerance);
    }

    // ── G) Span / Batch API ───────────────────────────────────────────────────

    [Fact]
    public void Batch_MismatchedLowLength_Throws()
    {
        double[] hi = [100, 110], lo = [90], vol = [1000, 800], out_ = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Marketfi.Batch(hi, lo, vol, out_));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedVolumeLength_Throws()
    {
        double[] hi = [100, 110], lo = [90, 85], vol = [1000], out_ = new double[2];
        var ex = Assert.Throws<ArgumentException>(() => Marketfi.Batch(hi, lo, vol, out_));
        Assert.Equal("volume", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedOutputLength_Throws()
    {
        double[] hi = [100, 110], lo = [90, 85], vol = [1000, 800], out_ = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Marketfi.Batch(hi, lo, vol, out_));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_KnownValues_Correct()
    {
        double[] hi = [110, 120, 115];
        double[] lo = [90, 80, 95];
        double[] vol = [1000, 500, 200];
        double[] output = new double[3];
        Marketfi.Batch(hi, lo, vol, output);

        Assert.Equal(0.02, output[0], Tolerance);  // 20/1000
        Assert.Equal(0.08, output[1], Tolerance);  // 40/500
        Assert.Equal(0.10, output[2], Tolerance);  // 20/200
    }

    [Fact]
    public void Batch_ZeroVolume_ReturnsZero()
    {
        double[] hi = [110], lo = [90], vol = [0.0], output = new double[1];
        Marketfi.Batch(hi, lo, vol, output);
        Assert.Equal(0.0, output[0], Tolerance);
    }

    [Fact]
    public void Batch_EmptySpans_NoThrow()
    {
        double[] hi = [], lo = [], vol = [], output = [];
        Marketfi.Batch(hi, lo, vol, output); // must not throw
        Assert.Empty(output); // trivially confirms no mutation and no exception
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        const int N = 100_000;
        var hi = new double[N];
        var lo = new double[N];
        var vol = new double[N];
        var output = new double[N];
        for (int i = 0; i < N; i++) { hi[i] = 110; lo[i] = 90; vol[i] = 1000; }
        Marketfi.Batch(hi, lo, vol, output);
        Assert.Equal(0.02, output[N - 1], Tolerance);
    }

    // ── H) Chainability ──────────────────────────────────────────────────────

    [Fact]
    public void PubEvent_Fires_OnUpdate()
    {
        var m = new Marketfi();
        int count = 0;
        m.Pub += (object? _, in TValueEventArgs e) => count++;

        for (int i = 0; i < 10; i++)
        {
            m.Update(_gbm.Next(isNew: true), isNew: true);
        }
        Assert.Equal(10, count);
    }

    [Fact]
    public void TBarSeries_Chaining_Works()
    {
        var source = new TBarSeries();
        var m = new Marketfi(source);
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 55);
        for (int i = 0; i < 20; i++)
        {
            source.Add(gbm.Next(isNew: true));
        }
        Assert.True(double.IsFinite(m.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var m = new Marketfi();
        m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.True(m.IsHot);
        m.Reset();
        Assert.False(m.IsHot);
        Assert.Equal(default, m.Last);
    }
}
