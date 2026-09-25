using Xunit;

namespace QuanTAlib.Tests;

public sealed class HhllsValidationTests : IDisposable
{
    private readonly GBM _gbm = new(mu: 0.05, sigma: 0.2, seed: 42);
    private readonly TBarSeries _bars;

    public HhllsValidationTests()
    {
        _bars = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    public void Dispose()
    {
        // no-op
    }

    // ────────── Self-consistency: Streaming vs Batch (span) ──────────

    [Fact]
    public void SelfConsistency_StreamingMatchesBatchSpan()
    {
        const int period = 20;
        var indicator = new Hhlls(period);

        var streamHhs = new double[_bars.Count];
        var streamLls = new double[_bars.Count];

        for (int i = 0; i < _bars.Count; i++)
        {
            indicator.Update(_bars[i], true);
            streamHhs[i] = indicator.Hhs.Value;
            streamLls[i] = indicator.Lls.Value;
        }

        var batchHhs = new double[_bars.Count];
        var batchLls = new double[_bars.Count];
        Hhlls.Batch(_bars.HighValues, _bars.LowValues, batchHhs, batchLls, period);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(streamHhs[i], batchHhs[i], 10);
            Assert.Equal(streamLls[i], batchLls[i], 10);
        }
    }

    // ────────── Self-consistency: Streaming vs TBarSeries Batch ──────────

    [Fact]
    public void SelfConsistency_StreamingMatchesTBarBatch()
    {
        const int period = 14;
        var indicator = new Hhlls(period);

        for (int i = 0; i < _bars.Count; i++)
        {
            indicator.Update(_bars[i], true);
        }

        var (batchHhs, batchLls) = Hhlls.Batch(_bars, period);

        Assert.Equal(indicator.Hhs.Value, batchHhs.Values[^1], 10);
        Assert.Equal(indicator.Lls.Value, batchLls.Values[^1], 10);
    }

    // ────────── Convergence: output must stabilize ──────────

    [Fact]
    public void Convergence_OutputStabilizesOverTime()
    {
        const int period = 20;
        var indicator = new Hhlls(period);

        double lastHhs = double.NaN;
        int stableCount = 0;

        for (int i = 0; i < _bars.Count; i++)
        {
            indicator.Update(_bars[i], true);
            if (indicator.IsHot && double.IsFinite(lastHhs))
            {
                double delta = Math.Abs(indicator.Hhs.Value - lastHhs);
                if (delta < 5.0)
                {
                    stableCount++;
                }
            }
            lastHhs = indicator.Hhs.Value;
        }

        // Most bars should have small delta (EMA smooths aggressively)
        Assert.True(stableCount > 300, $"Only {stableCount} stable bars out of ~480 hot bars");
    }

    // ────────── NaN in data ──────────

    [Fact]
    public void NaN_InMiddle_DoesNotPropagate()
    {
        const int period = 10;
        var indicator = new Hhlls(period);

        for (int i = 0; i < 50; i++)
        {
            indicator.Update(_bars[i], true);
        }

        // Feed NaN bar
        indicator.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0), true);

        Assert.True(double.IsFinite(indicator.Hhs.Value));
        Assert.True(double.IsFinite(indicator.Lls.Value));
    }

    // ────────── Flat line: constant H/L → both outputs near 0 ──────────

    [Fact]
    public void FlatLine_BothOutputsNearZero()
    {
        const int period = 10;
        var indicator = new Hhlls(period);
        DateTime t = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            indicator.Update(new TBar(t.AddMinutes(i), 100, 100, 100, 100, 1000), true);
        }

        Assert.True(indicator.Hhs.Value < 1e-6, $"HHS should be ~0: {indicator.Hhs.Value}");
        Assert.True(indicator.Lls.Value < 1e-6, $"LLS should be ~0: {indicator.Lls.Value}");
    }

    // ────────── Large dataset stability ──────────

    [Fact]
    public void LargeDataset_NoOverflow()
    {
        var bigBars = _gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var indicator = new Hhlls(20);

        for (int i = 0; i < bigBars.Count; i++)
        {
            indicator.Update(bigBars[i], true);
        }

        Assert.True(double.IsFinite(indicator.Hhs.Value));
        Assert.True(double.IsFinite(indicator.Lls.Value));
        Assert.InRange(indicator.Hhs.Value, 0, 100);
        Assert.InRange(indicator.Lls.Value, 0, 100);
    }

    // ────────── Different periods produce different results ──────────

    [Fact]
    public void DifferentPeriods_DifferentResults()
    {
        var h10 = new Hhlls(10);
        var h30 = new Hhlls(30);

        for (int i = 0; i < _bars.Count; i++)
        {
            h10.Update(_bars[i], true);
            h30.Update(_bars[i], true);
        }

        // At least one output should differ
        bool hhsDiffer = Math.Abs(h10.Hhs.Value - h30.Hhs.Value) > 0.01;
        bool llsDiffer = Math.Abs(h10.Lls.Value - h30.Lls.Value) > 0.01;
        Assert.True(hhsDiffer || llsDiffer);
    }
}
