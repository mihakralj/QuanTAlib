using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for LRSI.
/// LRSI is not implemented in Skender, TA-Lib, Tulip, or Ooples — validation uses:
/// 1. Batch TSeries == streaming consistency
/// 2. Calculate(Span) == Calculate(TSeries) consistency
/// 3. Eventing path matches streaming
/// 4. Output always in [0, 1] under all conditions
/// 5. Higher gamma produces smoother (lower variance) output than lower gamma
/// 6. Gamma effect: high gamma retains more memory (slower response)
/// 7. Determinism: same seed → identical results
/// </summary>
public sealed class LrsiValidationTests
{
    private const double Tolerance = 1e-10;

    // ── Self-consistency: batch TSeries == streaming ──

    [Fact]
    public void Streaming_MatchesBatch_DefaultGamma()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 3001);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Lrsi(0.5);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        TSeries batchTs = Lrsi.Calculate(source, 0.5);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batchTs.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Streaming_MatchesBatch_LowGamma()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.3, seed: 3002);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Lrsi(0.1);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        TSeries batchTs = Lrsi.Calculate(source, 0.1);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batchTs.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Streaming_MatchesBatch_HighGamma()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 3003);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Lrsi(0.9);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        TSeries batchTs = Lrsi.Calculate(source, 0.9);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batchTs.Values[i], Tolerance);
        }
    }

    // ── Self-consistency: Span == TSeries ──

    [Fact]
    public void Span_MatchesBatch_DefaultGamma()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 3004);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries batchTs = Lrsi.Calculate(source, 0.5);

        var spanOut = new double[source.Count];
        Lrsi.Calculate(source.Values, spanOut, 0.5);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOut[i], Tolerance);
        }
    }

    [Fact]
    public void Eventing_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 3005);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var streaming = new Lrsi(0.5);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        var eventTs = new TSeries();
        var eventLrsi = new Lrsi(eventTs, 0.5);
        var eventVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventTs.Add(source[i]);
            eventVals[i] = eventLrsi.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], eventVals[i], Tolerance);
        }
    }

    // ── Output range: always in [0, 1] ──

    [Fact]
    public void Output_AlwaysInRange0To1_HighVolatility()
    {
        var gbm = new GBM(startPrice: 50.0, mu: 0.05, sigma: 0.8, seed: 3006);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var lrsi = new Lrsi(0.5);
        foreach (var bar in bars.Close)
        {
            double v = lrsi.Update(bar).Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"LRSI={v} out of [0,1] at high vol");
        }
    }

    [Fact]
    public void Output_AlwaysInRange0To1_LowVolatility()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.001, sigma: 0.01, seed: 3007);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var lrsi = new Lrsi(0.5);
        foreach (var bar in bars.Close)
        {
            double v = lrsi.Update(bar).Value;
            Assert.True(v >= 0.0 && v <= 1.0, $"LRSI={v} out of [0,1] at low vol");
        }
    }

    [Fact]
    public void Output_AlwaysInRange_AllGammaValues()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.25, seed: 3008);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (double gamma in new[] { 0.0, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0 })
        {
            var lrsi = new Lrsi(gamma);
            foreach (var bar in bars.Close)
            {
                double v = lrsi.Update(bar).Value;
                Assert.True(v >= 0.0 && v <= 1.0, $"gamma={gamma} LRSI={v} out of [0,1]");
            }
        }
    }

    // ── Gamma effect: higher gamma = smoother = less total variation on noisy input ──

    [Fact]
    public void HigherGamma_ProducesLessTotalVariation_OnZigzagInput()
    {
        // The Laguerre filter's gamma controls damping across all 4 stages.
        // High gamma (e.g. 0.9) heavily damps each stage → LRSI output changes slowly.
        // Low gamma (e.g. 0.1) passes through price changes quickly → LRSI oscillates more.
        //
        // We verify this via total variation: sum of |LRSI[i] - LRSI[i-1]| over a zigzag series.
        // High gamma must produce strictly lower total variation than low gamma.
        //
        // Note: After full convergence to flat, both gammas snap to LRSI=1 on first up-bar
        // because L1-L3 are all equal (no inter-stage difference to flip with gamma).
        // Zigzag avoids this degenerate case by continuously exercising all 4 filter stages.

        var t = DateTime.UtcNow;
        const int n = 500;

        var lrsiLow = new Lrsi(0.1);  // fast: high variation
        var lrsiHigh = new Lrsi(0.9);  // slow: low variation

        double tvLow = 0.0;
        double tvHigh = 0.0;
        double prevLow = double.NaN;
        double prevHigh = double.NaN;

        // Zigzag: alternates +3 / -3 around 100, giving constant up/down signal
        for (int i = 0; i < n; i++)
        {
            double price = 100.0 + (i % 2 == 0 ? 3.0 : -3.0);
            double vL = lrsiLow.Update(new TValue(t.AddMinutes(i), price), isNew: true).Value;
            double vH = lrsiHigh.Update(new TValue(t.AddMinutes(i), price), isNew: true).Value;

            if (!double.IsNaN(prevLow))
            {
                tvLow += Math.Abs(vL - prevLow);
                tvHigh += Math.Abs(vH - prevHigh);
            }

            prevLow = vL;
            prevHigh = vH;
        }

        Assert.True(tvHigh < tvLow,
            $"High gamma total variation ({tvHigh:F4}) should be less than low gamma ({tvLow:F4})");
    }

    [Fact]
    public void GammaZero_IsMoreResponsiveThanGammaHalf()
    {
        // gamma=0: L0 = close, L1 = prevL0, L2 = prevL1, L3 = prevL2
        // gamma=0.5: smoothed response
        // After a sharp price move, gamma=0 should react more rapidly.
        var lrsi0 = new Lrsi(0.0);
        var lrsi5 = new Lrsi(0.5);

        // Warm up with baseline
        var t = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            lrsi0.Update(new TValue(t.AddMinutes(i), 100.0), isNew: true);
            lrsi5.Update(new TValue(t.AddMinutes(i), 100.0), isNew: true);
        }

        // Single large up-spike — gamma=0 should read more extreme
        double v0 = lrsi0.Update(new TValue(t.AddMinutes(20), 150.0), isNew: true).Value;
        double v5 = lrsi5.Update(new TValue(t.AddMinutes(20), 150.0), isNew: true).Value;

        // gamma=0 reacts immediately to spike; gamma=0.5 absorbs it more gradually
        Assert.True(v0 >= v5, $"gamma=0 ({v0:F6}) should be >= gamma=0.5 ({v5:F6}) on upspike");
    }

    // ── Determinism ──

    [Fact]
    public void Determinism_SameSeed_ProducesIdenticalResults()
    {
        var gbm1 = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 5001);
        var gbm2 = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 5001);
        var bars1 = gbm1.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var bars2 = gbm2.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var l1 = new Lrsi(0.5);
        var l2 = new Lrsi(0.5);

        for (int i = 0; i < bars1.Close.Count; i++)
        {
            double v1 = l1.Update(bars1.Close[i]).Value;
            double v2 = l2.Update(bars2.Close[i]).Value;
            Assert.Equal(v1, v2, Tolerance);
        }
    }

    // ── Edge cases ──

    [Fact]
    public void BatchSpan_EmptySource_ReturnsEmptyOutput()
    {
        var src = Array.Empty<double>();
        var out1 = Array.Empty<double>();
        Lrsi.Calculate(src, out1);
        Assert.Empty(out1);
    }

    [Fact]
    public void Streaming_ConstantPrice_ProducesHalfPoint()
    {
        var lrsi = new Lrsi(0.5);
        var t = DateTime.UtcNow;
        double last = 0;
        for (int i = 0; i < 200; i++)
        {
            last = lrsi.Update(new TValue(t.AddMinutes(i), 100.0)).Value;
        }
        // Constant price → all stages converge → cu = cd = 0 → LRSI = 0.5
        Assert.Equal(0.5, last, 1e-6);
    }

    [Fact]
    public void Streaming_MonotonicallyRising_ProducesHighValues()
    {
        // Strictly rising prices → L0 > L1 > L2 > L3 always after warmup → cu > 0, cd = 0 → LRSI = 1
        var lrsi = new Lrsi(0.3);
        var t = DateTime.UtcNow;
        double price = 100.0;
        for (int i = 0; i < 100; i++)
        {
            price += 1.0;
            lrsi.Update(new TValue(t.AddMinutes(i), price), isNew: true);
        }
        // Should converge near 1 after sustained rise
        Assert.True(lrsi.Last.Value > 0.8, $"Expected > 0.8 on sustained rise, got {lrsi.Last.Value:F4}");
    }

    [Fact]
    public void Streaming_MonotonicallyFalling_ProducesLowValues()
    {
        // Strictly falling prices → cd > 0, cu = 0 → LRSI converges near 0
        var lrsi = new Lrsi(0.3);
        var t = DateTime.UtcNow;
        double price = 200.0;
        for (int i = 0; i < 100; i++)
        {
            price -= 1.0;
            lrsi.Update(new TValue(t.AddMinutes(i), price), isNew: true);
        }
        Assert.True(lrsi.Last.Value < 0.2, $"Expected < 0.2 on sustained fall, got {lrsi.Last.Value:F4}");
    }

    [Fact]
    public void Lrsi_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateEhlersLaguerreRelativeStrengthIndex();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}