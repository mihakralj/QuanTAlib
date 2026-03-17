using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class BwMfiTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.2, seed: 42);
    private const double Tolerance = 1e-10;

    // ── A) Constructor validation ─────────────────────────────────────────────

    [Fact]
    public void Constructor_Default_SetsName()
    {
        var m = new BwMfi();
        Assert.Equal("BwMfi", m.Name);
    }

    [Fact]
    public void Constructor_Default_WarmupPeriodIsTwo()
    {
        Assert.Equal(2, BwMfi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_Default_NotHotBeforeFirstBar()
    {
        var m = new BwMfi();
        Assert.False(m.IsHot);
    }

    [Fact]
    public void Constructor_Default_ZoneIsZero()
    {
        var m = new BwMfi();
        Assert.Equal(0, m.Zone);
    }

    // ── B) Basic MFI calculation ──────────────────────────────────────────────

    [Fact]
    public void Update_BasicBar_CorrectMfi()
    {
        var m = new BwMfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 105.0, 95.0, 102.0, 1000.0);
        var result = m.Update(bar);
        // MFI = (105 - 95) / 1000 = 0.01
        Assert.Equal(0.01, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ZeroVolume_ReturnsZero()
    {
        var m = new BwMfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 110.0, 90.0, 100.0, 0.0);
        var result = m.Update(bar);
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ZeroRange_ReturnsZero()
    {
        var m = new BwMfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 100.0, 100.0, 100.0, 1000.0);
        var result = m.Update(bar);
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_LastMatchesReturnValue()
    {
        var m = new BwMfi();
        var bar = new TBar(DateTime.UtcNow, 100.0, 120.0, 80.0, 100.0, 200.0);
        var result = m.Update(bar);
        Assert.Equal(result.Value, m.Last.Value, Tolerance);
    }

    // ── C) Zone classification ────────────────────────────────────────────────

    [Fact]
    public void Zone_FirstBar_IsZero()
    {
        var m = new BwMfi();
        m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.Equal(0, m.Zone);
    }

    [Fact]
    public void Zone_Green_MfiUpVolumeUp()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        // Bar 1: MFI = (110-90)/1000 = 0.02, Vol = 1000
        m.Update(new TBar(t, 100, 110, 90, 100, 1000));
        // Bar 2: MFI = (120-80)/2000 = 0.02... need MFI up too
        // Bar 2: MFI = (130-70)/1500 = 0.04, Vol = 1500 (both up)
        m.Update(new TBar(t.AddMinutes(1), 100, 130, 70, 100, 1500));
        Assert.Equal(1, m.Zone); // Green
    }

    [Fact]
    public void Zone_Fade_MfiDownVolumeDown()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        // Bar 1: MFI = (120-80)/1000 = 0.04, Vol = 1000
        m.Update(new TBar(t, 100, 120, 80, 100, 1000));
        // Bar 2: MFI = (105-95)/500 = 0.02, Vol = 500 (both down)
        m.Update(new TBar(t.AddMinutes(1), 100, 105, 95, 100, 500));
        Assert.Equal(2, m.Zone); // Fade
    }

    [Fact]
    public void Zone_Fake_MfiUpVolumeDown()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        // Bar 1: MFI = (110-90)/1000 = 0.02, Vol = 1000
        m.Update(new TBar(t, 100, 110, 90, 100, 1000));
        // Bar 2: MFI = (130-70)/500 = 0.12, Vol = 500 (MFI up, Vol down)
        m.Update(new TBar(t.AddMinutes(1), 100, 130, 70, 100, 500));
        Assert.Equal(3, m.Zone); // Fake
    }

    [Fact]
    public void Zone_Squat_MfiDownVolumeUp()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        // Bar 1: MFI = (120-80)/500 = 0.08, Vol = 500
        m.Update(new TBar(t, 100, 120, 80, 100, 500));
        // Bar 2: MFI = (105-95)/2000 = 0.005, Vol = 2000 (MFI down, Vol up)
        m.Update(new TBar(t.AddMinutes(1), 100, 105, 95, 100, 2000));
        Assert.Equal(4, m.Zone); // Squat
    }

    [Fact]
    public void Zone_Range_IsValid()
    {
        var m = new BwMfi();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 77);
        for (int i = 0; i < 200; i++)
        {
            m.Update(gbm.Next(isNew: true));
            Assert.InRange(m.Zone, 0, 4);
        }
    }

    // ── D) State + bar correction ─────────────────────────────────────────────

    [Fact]
    public void Update_IsNewFalse_RewritesLastBar()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000), isNew: true);

        m.Update(new TBar(t.AddMinutes(1), 100, 112, 88, 100, 800), isNew: true);
        m.Update(new TBar(t.AddMinutes(1), 100, 120, 80, 100, 400), isNew: false);

        Assert.Equal(0.1, m.Last.Value, Tolerance); // (120-80)/400
    }

    [Fact]
    public void Update_BarCorrection_ZoneUpdates()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        // Bar 1: MFI = 0.02, Vol = 1000
        m.Update(new TBar(t, 100, 110, 90, 100, 1000), isNew: true);

        // Bar 2: MFI = 0.04, Vol = 1500 → Green (both up)
        m.Update(new TBar(t.AddMinutes(1), 100, 130, 70, 100, 1500), isNew: true);
        Assert.Equal(1, m.Zone);

        // Correct Bar 2: MFI = 0.005, Vol = 2000 → Squat (MFI down, Vol up)
        m.Update(new TBar(t.AddMinutes(1), 100, 105, 95, 100, 2000), isNew: false);
        Assert.Equal(4, m.Zone);
    }

    // ── E) Warmup / convergence ───────────────────────────────────────────────

    [Fact]
    public void IsHot_FalseForFirstBar_TrueForSecond()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 500));
        Assert.False(m.IsHot);
        m.Update(new TBar(t.AddMinutes(1), 100, 115, 85, 100, 600));
        Assert.True(m.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000));
        m.Update(new TBar(t.AddMinutes(1), 100, 115, 85, 100, 1200));
        Assert.True(m.IsHot);
        Assert.NotEqual(0, m.Zone);

        m.Reset();
        Assert.False(m.IsHot);
        Assert.Equal(0, m.Zone);
        Assert.Equal(default, m.Last);
    }

    // ── F) Robustness — NaN / Infinity ────────────────────────────────────────

    [Fact]
    public void Update_NaNVolume_ReturnsZero()
    {
        var m = new BwMfi();
        var r = m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, double.NaN));
        Assert.Equal(0.0, r.Value, Tolerance);
    }

    [Fact]
    public void Update_InfinityVolume_ReturnsZero()
    {
        var m = new BwMfi();
        var r = m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, double.PositiveInfinity));
        Assert.Equal(0.0, r.Value, Tolerance);
    }

    [Fact]
    public void Update_NaNHigh_ResultIsFinite()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        m.Update(new TBar(t, 100, 110, 90, 100, 1000));
        var r = m.Update(new TBar(t.AddMinutes(1), 100, double.NaN, 90, 100, 500), isNew: true);
        Assert.True(double.IsFinite(r.Value));
    }

    [Fact]
    public void Update_BatchNaN_NoPropagation()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            m.Update(new TBar(t.AddMinutes(i), 100, 110, 90, 100, 1000));
        }
        m.Update(new TBar(t.AddMinutes(5), 100, double.NaN, double.NaN, 100, 500));
        Assert.True(double.IsFinite(m.Last.Value));
    }

    // ── G) Consistency — streaming matches batch ──────────────────────────────

    [Fact]
    public void Consistency_StreamingMatchesBatch_MfiValues()
    {
        const int N = 100;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 42);

        double[] hi = new double[N], lo = new double[N], vol = new double[N];
        double streamResult;

        var mStream = new BwMfi();
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
        BwMfi.Batch(hi, lo, vol, output);
        double batchResult = output[N - 1];

        Assert.Equal(streamResult, batchResult, Tolerance);
    }

    [Fact]
    public void Consistency_StreamingMatchesBatch_Zones()
    {
        const int N = 100;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 42);

        double[] hi = new double[N], lo = new double[N], vol = new double[N];
        int[] streamZones = new int[N];

        var mStream = new BwMfi();
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            hi[i] = bar.High;
            lo[i] = bar.Low;
            vol[i] = bar.Volume;
            mStream.Update(bar, isNew: true);
            streamZones[i] = mStream.Zone;
        }

        var mfiOutput = new double[N];
        var zoneOutput = new int[N];
        BwMfi.Batch(hi, lo, vol, mfiOutput, zoneOutput);

        for (int i = 0; i < N; i++)
        {
            Assert.Equal(streamZones[i], zoneOutput[i]);
        }
    }

    [Fact]
    public void Consistency_EventBasedMatchesStreaming()
    {
        const int N = 50;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 7);

        var sourceStream = new TBarSeries();
        var mStream = new BwMfi();

        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            sourceStream.Add(bar);
            mStream.Update(bar, isNew: true);
        }

        var mEvent = new BwMfi(sourceStream);
        Assert.Equal(mStream.Last.Value, mEvent.Last.Value, Tolerance);
        Assert.Equal(mStream.Zone, mEvent.Zone);
    }

    // ── H) Span / Batch API ───────────────────────────────────────────────────

    [Fact]
    public void Batch_MismatchedLowLength_Throws()
    {
        double[] hi = [100, 110], lo = [90], vol = [1000, 800];
        var mfiOut = new double[2];
        var zoneOut = new int[2];
        var ex = Assert.Throws<ArgumentException>(() => BwMfi.Batch(hi, lo, vol, mfiOut, zoneOut));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedVolumeLength_Throws()
    {
        double[] hi = [100, 110], lo = [90, 85], vol = [1000];
        var mfiOut = new double[2];
        var zoneOut = new int[2];
        var ex = Assert.Throws<ArgumentException>(() => BwMfi.Batch(hi, lo, vol, mfiOut, zoneOut));
        Assert.Equal("volume", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedMfiOutputLength_Throws()
    {
        double[] hi = [100, 110], lo = [90, 85], vol = [1000, 800];
        var mfiOut = new double[3];
        var zoneOut = new int[2];
        var ex = Assert.Throws<ArgumentException>(() => BwMfi.Batch(hi, lo, vol, mfiOut, zoneOut));
        Assert.Equal("mfiOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_MismatchedZoneOutputLength_Throws()
    {
        double[] hi = [100, 110], lo = [90, 85], vol = [1000, 800];
        var mfiOut = new double[2];
        var zoneOut = new int[3];
        var ex = Assert.Throws<ArgumentException>(() => BwMfi.Batch(hi, lo, vol, mfiOut, zoneOut));
        Assert.Equal("zoneOutput", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptySpans_NoThrow()
    {
        double[] hi = [], lo = [], vol = [];
        var mfiOut = Array.Empty<double>();
        var zoneOut = Array.Empty<int>();
        BwMfi.Batch(hi, lo, vol, mfiOut, zoneOut);
        Assert.Empty(mfiOut);
    }

    [Fact]
    public void Batch_KnownValues_Correct()
    {
        double[] hi = [110, 120, 115];
        double[] lo = [90, 80, 95];
        double[] vol = [1000, 500, 200];
        var mfiOutput = new double[3];
        var zoneOutput = new int[3];
        BwMfi.Batch(hi, lo, vol, mfiOutput, zoneOutput);

        Assert.Equal(0.02, mfiOutput[0], Tolerance);  // 20/1000
        Assert.Equal(0.08, mfiOutput[1], Tolerance);  // 40/500
        Assert.Equal(0.10, mfiOutput[2], Tolerance);  // 20/200

        Assert.Equal(0, zoneOutput[0]);  // first bar
        Assert.Equal(3, zoneOutput[1]);  // MFI up (0.02→0.08), Vol down (1000→500) = Fake
        Assert.Equal(3, zoneOutput[2]);  // MFI up (0.08→0.10), Vol down (500→200) = Fake
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        const int N = 100_000;
        var hi = new double[N];
        var lo = new double[N];
        var vol = new double[N];
        var mfiOutput = new double[N];
        var zoneOutput = new int[N];
        for (int i = 0; i < N; i++) { hi[i] = 110; lo[i] = 90; vol[i] = 1000; }
        BwMfi.Batch(hi, lo, vol, mfiOutput, zoneOutput);
        Assert.Equal(0.02, mfiOutput[N - 1], Tolerance);
    }

    // ── I) Chainability ──────────────────────────────────────────────────────

    [Fact]
    public void PubEvent_Fires_OnUpdate()
    {
        var m = new BwMfi();
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
        var m = new BwMfi(source);
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 55);
        for (int i = 0; i < 20; i++)
        {
            source.Add(gbm.Next(isNew: true));
        }
        Assert.True(double.IsFinite(m.Last.Value));
        Assert.True(m.IsHot);
    }
}
