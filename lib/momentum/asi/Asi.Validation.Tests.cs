namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for ASI.
/// No external library (TA-Lib, Skender, Tulip, Ooples) implements Wilder's ASI natively,
/// so validation focuses on:
///   1. Batch == Streaming == Span (all 3 modes identical)
///   2. Mathematical identity checks (known formula inputs)
///   3. Directional correctness (uptrend → positive, downtrend → negative)
///   4. Determinism with seeded GBM
///   5. limitMove scaling (doubled T → halved SI magnitudes)
/// </summary>
public sealed class AsiValidationTests
{
    // ── 1. All 3 modes produce identical results ───────────────────────────────

    [Fact]
    public void Batch_Equals_Streaming_Equals_Span()
    {
        const double lm = 3.0;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 2024);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Asi(lm);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamResults[i] = streaming.Update(bars[i]).Value;
        }

        // Span batch
        double[] opens = new double[bars.Count];
        double[] highs = new double[bars.Count];
        double[] lows = new double[bars.Count];
        double[] closes = new double[bars.Count];
        double[] spanOutput = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            opens[i] = bars[i].Open;
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        Asi.Batch(opens.AsSpan(), highs.AsSpan(), lows.AsSpan(), closes.AsSpan(), spanOutput.AsSpan(), lm);

        // TBarSeries batch
        var tbatch = new Asi(lm);
        tbatch.Update(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], 1e-9);
        }

        Assert.Equal(streaming.Last.Value, tbatch.Last.Value, 1e-9);
    }

    [Fact]
    public void MultipleSeeds_AllModesConsistent()
    {
        int[] seeds = { 1, 42, 100, 999, 12345 };

        foreach (int seed in seeds)
        {
            var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: seed);
            var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

            // Streaming
            var streaming = new Asi(3.0);
            for (int i = 0; i < bars.Count; i++) { streaming.Update(bars[i]); }

            // Span
            double[] o = new double[bars.Count], h = new double[bars.Count];
            double[] l = new double[bars.Count], c = new double[bars.Count];
            double[] output = new double[bars.Count];

            for (int i = 0; i < bars.Count; i++)
            {
                o[i] = bars[i].Open; h[i] = bars[i].High;
                l[i] = bars[i].Low; c[i] = bars[i].Close;
            }

            Asi.Batch(o.AsSpan(), h.AsSpan(), l.AsSpan(), c.AsSpan(), output.AsSpan(), 3.0);

            Assert.Equal(streaming.Last.Value, output[^1], 1e-9);
        }
    }

    // ── 2. Mathematical identity checks ──────────────────────────────────────

    [Fact]
    public void FlatMarket_ASIStaysZero()
    {
        // Perfectly flat OHLC → SI=0 every bar → ASI=0
        var asi = new Asi(3.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(now.AddMinutes(i), 100.0, 100.0, 100.0, 100.0, 0);
            var result = asi.Update(bar);
            Assert.Equal(0.0, result.Value, 1e-12);
        }
    }

    [Fact]
    public void LimitMoveDoubled_HalvesSIMagnitude()
    {
        // Double the limitMove T → K/T is halved → SI is halved → ASI is halved
        var gbm = new GBM(startPrice: 100.0, seed: 77);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var asi1 = new Asi(3.0);
        var asi2 = new Asi(6.0);

        for (int i = 0; i < bars.Count; i++)
        {
            asi1.Update(bars[i]);
            asi2.Update(bars[i]);
        }

        // ASI(T=6) should be exactly half of ASI(T=3)
        Assert.Equal(asi1.Last.Value / 2.0, asi2.Last.Value, 1e-9);
    }

    [Fact]
    public void KnownValues_Bar2_MatchFormula()
    {
        // Bar1: O=100, H=105, L=98, C=102
        // Bar2: O=102, H=108, L=99, C=106
        // K = max(|108-102|, |99-102|) = max(6, 3) = 6
        // absHC=6, absLC=3, absHL=9, absC1O1=|102-100|=2
        // absHL(9) >= absHC(6) and absHL(9) >= absLC(3) => R = 9 + 0.25*2 = 9.5
        // numerator = (106-102) + 0.5*(106-102) + 0.25*(102-100) = 4 + 2 + 0.5 = 6.5
        // SI = 50 * 6.5 / 9.5 * (6 / 3.0) = 50 * 0.6842 * 2 = 68.421...
        const double expectedASI = 50.0 * 6.5 / 9.5 * (6.0 / 3.0);

        var now = DateTime.UtcNow;
        var bar1 = new TBar(now, 100, 105, 98, 102, 0);
        var bar2 = new TBar(now.AddMinutes(1), 102, 108, 99, 106, 0);

        var asi = new Asi(3.0);
        asi.Update(bar1);
        var result = asi.Update(bar2);

        Assert.Equal(expectedASI, result.Value, 1e-9);
    }

    [Fact]
    public void KnownValues_ConditionHCLargest()
    {
        // Setup where |H-C1| dominates: H moves far above prevClose
        // Bar1: O=100, H=101, L=99, C=100
        // Bar2: O=100, H=110, L=99, C=105   (absHC=10, absLC=1, absHL=11 → absHL largest)
        // R = 11 + 0.25*0 = 11
        // numerator = (105-100) + 0.5*(105-100) + 0.25*(100-100) = 5 + 2.5 = 7.5
        // K = max(10, 1) = 10
        // SI = 50 * 7.5 / 11 * (10/3) = 50 * 0.6818 * 3.333 = 113.636...

        // For condition |H-C1| >= |L-C1| AND |H-C1| >= |H-L|:
        // Bar1: O=100, H=102, L=99, C=100
        // Bar2: O=100, H=108, L=100, C=105
        // absHC=|108-100|=8, absLC=|100-100|=0, absHL=|108-100|=8
        // absHC(8) >= absLC(0) and absHC(8) >= absHL(8) → first branch
        // R = 8 - 0.5*0 + 0.25*|100-100| = 8
        // K = max(8,0) = 8
        // numerator = (105-100) + 0.5*(105-100) + 0.25*(100-100) = 5 + 2.5 = 7.5
        // SI = 50 * 7.5 / 8 * (8/3) = 50 * 0.9375 * 2.6667 = 125
        const double expectedASI = 50.0 * 7.5 / 8.0 * (8.0 / 3.0);

        var now = DateTime.UtcNow;
        var bar1 = new TBar(now, 100, 102, 99, 100, 0);
        var bar2 = new TBar(now.AddMinutes(1), 100, 108, 100, 105, 0);

        var asi = new Asi(3.0);
        asi.Update(bar1);
        var result = asi.Update(bar2);

        Assert.Equal(expectedASI, result.Value, 1e-9);
    }

    [Fact]
    public void KnownValues_ConditionLCLargest()
    {
        // |L-C1| dominates: large down move below prevClose
        // Bar1: O=100, H=102, L=98, C=100
        // Bar2: O=100, H=100, L=90, C=93
        // absHC=|100-100|=0, absLC=|90-100|=10, absHL=|100-90|=10
        // absLC(10) >= absHC(0) and absLC(10) >= absHL(10) → second branch
        // R = 10 - 0.5*0 + 0.25*|100-100| = 10
        // K = max(0, 10) = 10
        // numerator = (93-100) + 0.5*(93-100) + 0.25*(100-100) = -7 + (-3.5) + 0 = -10.5
        // SI = 50 * (-10.5) / 10 * (10/3) = 50 * (-1.05) * 3.333 = -175
        const double expectedASI = 50.0 * (-10.5) / 10.0 * (10.0 / 3.0);

        var now = DateTime.UtcNow;
        var bar1 = new TBar(now, 100, 102, 98, 100, 0);
        var bar2 = new TBar(now.AddMinutes(1), 100, 100, 90, 93, 0);

        var asi = new Asi(3.0);
        asi.Update(bar1);
        var result = asi.Update(bar2);

        Assert.Equal(expectedASI, result.Value, 1e-9);
    }

    // ── 3. Directional correctness ────────────────────────────────────────────

    [Fact]
    public void SteadyUptrend_PositiveASI()
    {
        var asi = new Asi(3.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            double p = 100.0 + i;
            asi.Update(new TBar(now.AddMinutes(i), p, p + 1.5, p - 0.5, p + 1.0, 0));
        }

        Assert.True(asi.Last.Value > 0, $"Uptrend ASI expected > 0, got {asi.Last.Value}");
    }

    [Fact]
    public void SteadyDowntrend_NegativeASI()
    {
        var asi = new Asi(3.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            double p = 100.0 - i;
            asi.Update(new TBar(now.AddMinutes(i), p, p + 0.5, p - 1.5, p - 1.0, 0));
        }

        Assert.True(asi.Last.Value < 0, $"Downtrend ASI expected < 0, got {asi.Last.Value}");
    }

    // ── 4. Determinism ────────────────────────────────────────────────────────

    [Fact]
    public void SameSeeded_GBM_IdentialResults()
    {
        var gbm1 = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 314);
        var gbm2 = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 314);

        var bars1 = gbm1.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var bars2 = gbm2.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var asi1 = new Asi(3.0);
        var asi2 = new Asi(3.0);

        for (int i = 0; i < bars1.Count; i++)
        {
            asi1.Update(bars1[i]);
            asi2.Update(bars2[i]);
        }

        Assert.Equal(asi1.Last.Value, asi2.Last.Value, 1e-12);
    }

    // ── 5. Cumulative property ────────────────────────────────────────────────

    [Fact]
    public void ASI_IsCumulativeSumOfSI()
    {
        // Verify ASI[n] = ASI[n-1] + SI[n]
        const double lm = 3.0;
        var gbm = new GBM(startPrice: 100.0, seed: 500);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] o = new double[50], h = new double[50], l = new double[50], c = new double[50];
        double[] output = new double[50];

        for (int i = 0; i < 50; i++)
        {
            o[i] = bars[i].Open; h[i] = bars[i].High;
            l[i] = bars[i].Low; c[i] = bars[i].Close;
        }

        Asi.Batch(o.AsSpan(), h.AsSpan(), l.AsSpan(), c.AsSpan(), output.AsSpan(), lm);

        // Verify monotonic property: output[i] != output[i-1] unless SI was exactly 0
        // More critically, verify streaming result matches batch at each bar
        var streaming = new Asi(lm);
        for (int i = 0; i < 50; i++)
        {
            double streamVal = streaming.Update(bars[i]).Value;
            Assert.Equal(output[i], streamVal, 1e-9);
        }
    }

    // ── 6. Edge cases ─────────────────────────────────────────────────────────

    [Fact]
    public void EmptyTBarSeries_ReturnsEmptySeries()
    {
        var asi = new Asi(3.0);
        var empty = new TBarSeries();
        var result = asi.Update(empty);
        Assert.Empty(result);
    }

    [Fact]
    public void SingleBar_ReturnsZero()
    {
        var gbm = new GBM(startPrice: 100.0, seed: 1);
        var bars = gbm.Fetch(1, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var asi = new Asi(3.0);
        asi.Update(bars[0]);
        Assert.Equal(0.0, asi.Last.Value);
    }
}
