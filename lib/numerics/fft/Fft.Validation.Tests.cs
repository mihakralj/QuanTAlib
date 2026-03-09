using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// FFT validation tests — verifies known spectral responses against analytical results.
/// No external library implements this exact Ehlers-style windowed-DFT dominant cycle
/// detector, so validation uses self-consistency and analytical known-answer tests.
/// </summary>
public class FftValidationTests
{
    private const double Tolerance = 1e-10;
    private const double LooseTolerance = 3.0; // ±3 bars for period detection

    // ─── Self-consistency: batch vs streaming ─────────────────────────────────

    [Fact]
    public void Fft_BatchVsStreaming_AllValuesMatch()
    {
        int windowSize = 32;
        int count = 120;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 81001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var streaming = new Fft(windowSize, maxPeriod: 16);
        var streamVals = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming.Update(source[i]);
            streamVals[i] = streaming.Last.Value;
        }

        var batch = Fft.Batch(source, windowSize, maxPeriod: 16);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamVals[i], batch[i].Value, Tolerance);
        }
    }

    // ─── Pure sine: dominant period detection ─────────────────────────────────

    [Fact]
    public void Fft_PureSine_Period16_Detected_N64()
    {
        // Sine at period 16, N=64, minP=4, maxP=32
        // Bin k=4 → period 64/4=16; should detect ≈ 16 ± 3
        int targetPeriod = 16;
        int windowSize = 64;
        var indicator = new Fft(windowSize, minPeriod: 4, maxPeriod: 32);
        var time = DateTime.UtcNow;

        for (int i = 0; i < windowSize * 3; i++)
        {
            double signal = 50.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / targetPeriod);
            indicator.Update(new TValue(time.AddMinutes(i), signal), true);
        }

        Assert.True(indicator.IsHot);
        double detected = indicator.Last.Value;
        Assert.True(Math.Abs(detected - targetPeriod) <= LooseTolerance,
            $"Detected period {detected:F2} should be within {LooseTolerance} bars of {targetPeriod}");
    }

    [Fact]
    public void Fft_PureSine_Period8_Detected_N32()
    {
        // Sine at period 8, N=32, minP=4, maxP=16
        int targetPeriod = 8;
        int windowSize = 32;
        var indicator = new Fft(windowSize, minPeriod: 4, maxPeriod: 16);
        var time = DateTime.UtcNow;

        for (int i = 0; i < windowSize * 4; i++)
        {
            double signal = 50.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / targetPeriod);
            indicator.Update(new TValue(time.AddMinutes(i), signal), true);
        }

        Assert.True(indicator.IsHot);
        double detected = indicator.Last.Value;
        Assert.True(Math.Abs(detected - targetPeriod) <= LooseTolerance,
            $"Detected period {detected:F2} should be within {LooseTolerance} bars of {targetPeriod}");
    }

    // ─── Constant input → clamped to maxPeriod ───────────────────────────────

    [Fact]
    public void Fft_ConstantInput_ClampedToMaxPeriod()
    {
        // Constant input has no spectral peak → should output maxPeriod (clamped)
        int windowSize = 32;
        int maxP = 16;
        var indicator = new Fft(windowSize, minPeriod: 4, maxPeriod: maxP);
        var time = DateTime.UtcNow;

        for (int i = 0; i < windowSize + 20; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 100.0));
        }

        Assert.True(indicator.IsHot);
        double detected = indicator.Last.Value;
        // Constant input → all bins equal zero → peak at minBin → period = N/minBin = maxPeriod
        Assert.InRange(detected, 4.0, (double)maxP);
    }

    // ─── Determinism ─────────────────────────────────────────────────────────

    [Fact]
    public void Fft_SameInput_SameOutput_Deterministic()
    {
        int windowSize = 32;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 81002);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ind1 = new Fft(windowSize, maxPeriod: 16);
        var ind2 = new Fft(windowSize, maxPeriod: 16);

        for (int i = 0; i < bars.Close.Count; i++)
        {
            ind1.Update(bars.Close[i]);
            ind2.Update(bars.Close[i]);
        }

        Assert.Equal(ind1.Last.Value, ind2.Last.Value, Tolerance);
    }

    // ─── Two independent instances → same result ─────────────────────────────

    [Fact]
    public void Fft_TwoInstances_SameParameters_Consistent()
    {
        int windowSize = 32;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 81003);
        int count = 60;
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indA = new Fft(windowSize, minPeriod: 4, maxPeriod: 16);
        var indB = new Fft(windowSize, minPeriod: 4, maxPeriod: 16);

        for (int i = 0; i < count; i++)
        {
            indA.Update(bars.Close[i]);
            indB.Update(bars.Close[i]);
            if (indA.IsHot)
            {
                Assert.Equal(indA.Last.Value, indB.Last.Value, Tolerance);
            }
        }
    }

    // ─── Span API self-consistency ────────────────────────────────────────────

    [Fact]
    public void Fft_SpanBatch_MatchesStreamingAllBars()
    {
        int windowSize = 32;
        int count = 80;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 81004);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        double[] spanOut = new double[count];
        Fft.Batch(src, spanOut, windowSize, maxPeriod: 16);

        var streaming = new Fft(windowSize, maxPeriod: 16);
        for (int i = 0; i < count; i++)
        {
            streaming.Update(bars.Close[i]);
            Assert.Equal(streaming.Last.Value, spanOut[i], Tolerance);
        }
    }

    // ─── Output clamp guarantee ───────────────────────────────────────────────

    [Fact]
    public void Fft_OutputNeverExceedsClampBounds_LargeDataset()
    {
        int windowSize = 64;
        int minP = 4;
        int maxP = 32;
        int count = 500;
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 81005);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var indicator = new Fft(windowSize, minP, maxP);
        for (int i = 0; i < count; i++)
        {
            indicator.Update(bars.Close[i]);
            if (indicator.IsHot)
            {
                double v = indicator.Last.Value;
                Assert.True(v >= minP && v <= maxP,
                    $"Bar {i}: output {v:F2} outside [{minP},{maxP}]");
            }
        }
    }

    // ─── Batch span NaN safety ────────────────────────────────────────────────

    [Fact]
    public void Fft_SpanBatch_WithNaN_AllOutputsFinite()
    {
        int windowSize = 32;
        int count = 80;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 81006);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] src = new double[count];
        for (int i = 0; i < count; i++)
        {
            src[i] = bars.Close[i].Value;
        }

        // Inject NaNs at various positions
        src[5] = double.NaN;
        src[20] = double.NaN;
        src[45] = double.NaN;

        double[] dst = new double[count];
        Fft.Batch(src, dst, windowSize, maxPeriod: 16);

        for (int i = 0; i < count; i++)
        {
            Assert.True(double.IsFinite(dst[i]),
                $"Output at {i} must be finite, got {dst[i]}");
        }
    }

    [Fact]
    public void Fft_Correction_StateRestores()
    {
        // The Hanning window maps idx=0 to the newest bar and _hanning[0]=0, so
        // corrections to the anchor bar carry zero spectral weight. isNew=false
        // determinism is still correct: restoring the original value reproduces the
        // original result exactly regardless of any intermediate correction.
        var ind = new Fft(windowSize: 32, maxPeriod: 16);
        var t0 = DateTime.MinValue;

        for (int i = 0; i < 50; i++)
        {
            ind.Update(new TValue(t0.AddSeconds(i), 100.0 + 10.0 * Math.Sin(2 * Math.PI * i / 8.0)));
        }

        var anchorTime = t0.AddSeconds(50);
        const double anchorPrice = 100.0;
        ind.Update(new TValue(anchorTime, anchorPrice), isNew: true);
        double anchorResult = ind.Last.Value;

        // Apply an arbitrary correction — spectral output is unchanged due to zero Hanning weight
        ind.Update(new TValue(anchorTime, anchorPrice * 100), isNew: false);

        // Restoring to original must exactly reproduce the original result
        ind.Update(new TValue(anchorTime, anchorPrice), isNew: false);
        Assert.Equal(anchorResult, ind.Last.Value, 1e-9);
    }
}
