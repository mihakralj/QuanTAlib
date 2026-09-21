using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for BW_MFI.
/// No direct Tulip cross-validation available (Tulip has marketfi but not zone classification).
/// MFI value validation delegates to MARKETFI Tulip tests; zones are self-validated.
/// </summary>
public sealed class BwMfiValidationTests
{
    private const double Tolerance = 1e-10;

    // ── Identity: MFI = Range / Volume ───────────────────────────────────────

    [Theory]
    [InlineData(110, 90, 1000, 0.02)]
    [InlineData(115, 85, 500, 0.06)]
    [InlineData(100, 80, 200, 0.10)]
    [InlineData(105, 100, 50, 0.10)]
    [InlineData(100, 100, 1000, 0.0)]   // zero range
    [InlineData(110, 90, 0, 0.0)]       // zero volume guard
    public void Identity_Formula_MatchesDirectComputation(
        double high, double low, double volume, double expected)
    {
        var m = new BwMfi();
        var result = m.Update(new TBar(DateTime.UtcNow, 100, high, low, 100, volume));
        Assert.Equal(expected, result.Value, Tolerance);
    }

    // ── Zone classification exhaustive ────────────────────────────────────────

    [Theory]
    [InlineData(0.02, 1000, 0.04, 1500, 1)]  // MFI↑ Vol↑ = Green
    [InlineData(0.04, 1000, 0.02, 500, 2)]   // MFI↓ Vol↓ = Fade
    [InlineData(0.02, 1000, 0.04, 500, 3)]   // MFI↑ Vol↓ = Fake
    [InlineData(0.04, 500, 0.02, 1000, 4)]   // MFI↓ Vol↑ = Squat
    public void Zone_ClassificationMatrix(
        double mfi1, double vol1, double mfi2, double vol2, int expectedZone)
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;

        // Construct bars to produce desired MFI values
        // MFI = (H-L)/V → H-L = MFI * V
        double range1 = mfi1 * vol1;
        double range2 = mfi2 * vol2;

        m.Update(new TBar(t, 100, 100 + (range1 / 2), 100 - (range1 / 2), 100, vol1));
        m.Update(new TBar(t.AddMinutes(1), 100, 100 + (range2 / 2), 100 - (range2 / 2), 100, vol2));

        Assert.Equal(expectedZone, m.Zone);
    }

    // ── MFI matches MARKETFI ─────────────────────────────────────────────────

    [Fact]
    public void MfiValue_MatchesMarketfi()
    {
        const int N = 200;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 17);

        var bwMfi = new BwMfi();
        var marketfi = new Marketfi();

        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            bwMfi.Update(bar, isNew: true);
            marketfi.Update(bar, isNew: true);
            Assert.Equal(marketfi.Last.Value, bwMfi.Last.Value, Tolerance);
        }
    }

    // ── Batch == Streaming ───────────────────────────────────────────────────

    [Fact]
    public void BatchStreaming_AgreeOnAllBars_MfiAndZones()
    {
        const int N = 200;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 17);

        double[] hi = new double[N], lo = new double[N], vol = new double[N];
        double[] streamMfi = new double[N];
        int[] streamZones = new int[N];

        var m = new BwMfi();
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            hi[i] = bar.High;
            lo[i] = bar.Low;
            vol[i] = bar.Volume;
            m.Update(bar, isNew: true);
            streamMfi[i] = m.Last.Value;
            streamZones[i] = m.Zone;
        }

        var batchMfi = new double[N];
        var batchZones = new int[N];
        BwMfi.Batch(hi, lo, vol, batchMfi, batchZones);

        for (int i = 0; i < N; i++)
        {
            Assert.Equal(streamMfi[i], batchMfi[i], Tolerance);
            Assert.Equal(streamZones[i], batchZones[i]);
        }
    }

    // ── Determinism ──────────────────────────────────────────────────────────

    [Fact]
    public void Determinism_SameInputSameOutput()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 99);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 99);

        var m1 = new BwMfi();
        var m2 = new BwMfi();

        for (int i = 0; i < 100; i++)
        {
            var bar1 = gbm1.Next(isNew: true);
            var bar2 = gbm2.Next(isNew: true);
            m1.Update(bar1, isNew: true);
            m2.Update(bar2, isNew: true);
            Assert.Equal(m1.Last.Value, m2.Last.Value, Tolerance);
            Assert.Equal(m1.Zone, m2.Zone);
        }
    }

    // ── Non-negativity ───────────────────────────────────────────────────────

    [Fact]
    public void Output_AlwaysNonNegative()
    {
        var gbm = new GBM(100.0, 0.05, 0.3, seed: 123);
        var m = new BwMfi();
        for (int i = 0; i < 500; i++)
        {
            var result = m.Update(gbm.Next(isNew: true));
            Assert.True(result.Value >= 0.0, $"MFI negative at bar {i}: {result.Value}");
        }
    }

    // ── Zero volume → zero output ─────────────────────────────────────────────

    [Fact]
    public void ZeroVolume_AlwaysZero()
    {
        var m = new BwMfi();
        var t = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            var result = m.Update(new TBar(t.AddMinutes(i), 100, 110 + i, 90 - i, 100, 0.0));
            Assert.Equal(0.0, result.Value, Tolerance);
        }
    }

    // ── Scaling: double volume halves MFI ────────────────────────────────────

    [Fact]
    public void Scaling_DoubleVolume_HalvesMfi()
    {
        var m1 = new BwMfi();
        var m2 = new BwMfi();

        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000.0);
        var bar2 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 2000.0);

        double mfi1 = m1.Update(bar1).Value;
        double mfi2 = m2.Update(bar2).Value;

        Assert.Equal(mfi1 / 2.0, mfi2, Tolerance);
    }

    // ── NaN safety ───────────────────────────────────────────────────────────

    [Fact]
    public void NaN_InputDoesNotProduceNaN()
    {
        var m = new BwMfi();
        m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));

        var nanBar = new TBar(DateTime.UtcNow.AddMinutes(1), 100, double.NaN, double.NaN, 100, double.NaN);
        var result = m.Update(nanBar);
        Assert.True(double.IsFinite(result.Value));
        Assert.InRange(m.Zone, 0, 4);
    }

    // ── Zone coverage: all 4 zones reachable ─────────────────────────────────

    [Fact]
    public void AllFourZones_Reachable()
    {
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 42);
        var m = new BwMfi();
        var zonesHit = new HashSet<int>();

        for (int i = 0; i < 1000 && zonesHit.Count < 4; i++)
        {
            m.Update(gbm.Next(isNew: true));
            if (m.Zone >= 1 && m.Zone <= 4)
            {
                zonesHit.Add(m.Zone);
            }
        }

        Assert.Contains(1, zonesHit); // Green
        Assert.Contains(2, zonesHit); // Fade
        Assert.Contains(3, zonesHit); // Fake
        Assert.Contains(4, zonesHit); // Squat
    }
}
