using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// CWT validation tests — verifies known wavelet responses against analytical results.
/// Since no external reference library implements CWT, we validate against:
/// 1. Zero-input → zero output (linearity)
/// 2. Constant input → near-zero output (wavelets have zero mean, so DC is rejected)
/// 3. Sinusoidal resonance: CWT at matching scale produces larger magnitude than at non-matching scale
/// 4. Output non-negativity (magnitude is always >= 0)
/// 5. Determinism (same input always produces same output)
/// 6. Batch vs streaming consistency
/// </summary>
public class CwtValidationTests
{
    private const double Tolerance = 1e-10;
    private const double LooseTolerance = 1e-6;

    // ─── Zero-mean property (DC rejection) ───────────────────────────────────

    [Fact]
    public void Cwt_ConstantInput_NearZero()
    {
        // Morlet wavelet has zero mean → convolution with constant signal ≈ 0
        // (not exactly 0 due to finite window, but very small relative to signal amplitude)
        double scale = 5.0;
        var indicator = new Cwt(scale);
        int windowSize = indicator.WarmupPeriod;
        var time = DateTime.UtcNow;

        // Feed constant value = 100.0 for full window + extra bars
        for (int i = 0; i < windowSize + 10; i++)
        {
            indicator.Update(new TValue(time.AddSeconds(i), 100.0));
        }

        Assert.True(indicator.IsHot);
        // Output should be very small relative to input amplitude (100.0)
        // Due to finite window truncation, Morlet real part sums are not exactly 0,
        // but the value should be negligible compared to signal energy.
        Assert.True(indicator.Last.Value < 5.0,
            $"Constant input should give near-zero CWT, got {indicator.Last.Value}");
    }

    [Fact]
    public void Cwt_ZeroInput_OutputIsZero()
    {
        // Zero signal → zero output (by linearity)
        double scale = 5.0;
        var indicator = new Cwt(scale);
        int windowSize = indicator.WarmupPeriod;
        var time = DateTime.UtcNow;

        for (int i = 0; i < windowSize + 5; i++)
        {
            indicator.Update(new TValue(time.AddSeconds(i), 0.0));
        }

        Assert.True(indicator.IsHot);
        Assert.Equal(0.0, indicator.Last.Value, LooseTolerance);
    }

    // ─── Resonance: matching scale produces peak response ────────────────────

    [Fact]
    public void Cwt_SinusoidalResonance_MatchingScaleHigher()
    {
        // A pure sine wave with period P should give maximum CWT magnitude at
        // scale s ≈ P*omega0/(2π). With omega0=6: s ≈ P/1.047
        // We test: scale_match gives strictly larger magnitude than scale_mismatch
        // on the same sinusoidal input.

        double omega0 = 6.0;
        double targetPeriod = 10.0; // 10-bar sine wave
        double matchingScale = targetPeriod * omega0 / (2.0 * Math.PI); // ≈ 9.55
        double mismatchScale = 2.0; // very different scale

        int count = 300;
        var time = DateTime.UtcNow;

        var matchIndicator = new Cwt(matchingScale, omega0);
        var mismatchIndicator = new Cwt(mismatchScale, omega0);

        for (int i = 0; i < count; i++)
        {
            double signal = Math.Sin(2.0 * Math.PI * i / targetPeriod);
            var tv = new TValue(time.AddSeconds(i), signal);
            matchIndicator.Update(tv);
            mismatchIndicator.Update(tv);
        }

        Assert.True(matchIndicator.IsHot);
        Assert.True(mismatchIndicator.IsHot);

        // Average magnitude over last half to smooth fluctuations
        // Reset and recompute for clean average
        var matchIndicator2 = new Cwt(matchingScale, omega0);
        var mismatchIndicator2 = new Cwt(mismatchScale, omega0);

        double sumMatch = 0.0, sumMismatch = 0.0;
        int nMatch = 0, nMismatch = 0;
        int halfCount = count / 2;

        for (int i = 0; i < count; i++)
        {
            double signal = Math.Sin(2.0 * Math.PI * i / targetPeriod);
            var tv = new TValue(time.AddSeconds(i), signal);
            matchIndicator2.Update(tv);
            mismatchIndicator2.Update(tv);

            if (i >= halfCount)
            {
                if (matchIndicator2.IsHot)
                {
                    sumMatch += matchIndicator2.Last.Value;
                    nMatch++;
                }

                if (mismatchIndicator2.IsHot)
                {
                    sumMismatch += mismatchIndicator2.Last.Value;
                    nMismatch++;
                }
            }
        }

        double avgMatch = nMatch > 0 ? sumMatch / nMatch : 0.0;
        double avgMismatch = nMismatch > 0 ? sumMismatch / nMismatch : 0.0;

        Assert.True(avgMatch > avgMismatch,
            $"Matching scale ({matchingScale:F2}) avg={avgMatch:F4} should exceed " +
            $"mismatch scale ({mismatchScale:F2}) avg={avgMismatch:F4}");
    }

    // ─── Non-negativity invariant ─────────────────────────────────────────────

    [Fact]
    public void Cwt_OutputAlwaysNonNegative_GbmData()
    {
        int count = 300;
        double scale = 8.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 72001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Cwt(scale);
        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            Assert.True(indicator.Last.Value >= 0.0,
                $"CWT magnitude negative at bar {i}: {indicator.Last.Value}");
        }
    }

    [Fact]
    public void Cwt_OutputAlwaysNonNegative_SpanBatch()
    {
        int count = 200;
        double scale = 5.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 72002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] dst = new double[count];
        Cwt.Batch(src, dst, scale);

        foreach (double v in dst)
        {
            Assert.True(v >= 0.0, $"Span CWT magnitude {v} must be >= 0");
        }
    }

    // ─── Determinism ──────────────────────────────────────────────────────────

    [Fact]
    public void Cwt_Deterministic_SameInput_SameOutput()
    {
        int count = 100;
        double scale = 6.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ind1 = new Cwt(scale);
        var ind2 = new Cwt(scale);

        for (int i = 0; i < count; i++)
        {
            ind1.Update(bars.Close[i]);
            ind2.Update(bars.Close[i]);
            Assert.Equal(ind1.Last.Value, ind2.Last.Value, Tolerance);
        }
    }

    // ─── Scale effect: larger scale → lower frequency ─────────────────────────

    [Fact]
    public void Cwt_DifferentScales_DifferentOutput()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ind3 = new Cwt(scale: 3.0);
        var ind10 = new Cwt(scale: 10.0);

        for (int i = 0; i < count; i++)
        {
            ind3.Update(bars.Close[i]);
            ind10.Update(bars.Close[i]);
        }

        // Different scales must produce different outputs (unless degenerate input)
        Assert.NotEqual(ind3.Last.Value, ind10.Last.Value, 1e-6);
    }

    // ─── Batch vs streaming full-array consistency ───────────────────────────

    [Fact]
    public void Cwt_Batch_MatchesStreaming_AllValues()
    {
        int count = 150;
        double scale = 4.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.25, seed: 72005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] rawValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            rawValues[i] = bars.Close[i].Value;
        }

        var tseriesResult = Cwt.Batch(bars.Close, scale);
        double[] spanResult = new double[count];
        Cwt.Batch(rawValues, spanResult, scale);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(tseriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    // ─── Large dataset: stable ────────────────────────────────────────────────

    [Fact]
    public void Cwt_LargeDataset_Stable()
    {
        int count = 2000;
        double scale = 10.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 72006);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Cwt(scale);

        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            double v = indicator.Last.Value;
            Assert.True(double.IsFinite(v) && v >= 0.0,
                $"Invalid output {v} at bar {i}");
        }
    }

    // ─── Period=1 trivial: single sample → zero (warmup) ─────────────────────

    [Fact]
    public void Cwt_SingleSampleBeforeWarmup_OutputZero()
    {
        var indicator = new Cwt(scale: 5.0);
        var time = DateTime.UtcNow;

        // Only one update: should NOT be hot
        indicator.Update(new TValue(time, 100.0));

        Assert.False(indicator.IsHot);
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
    }
}
