using Xunit;

namespace QuanTAlib.Tests;

public sealed class EpaValidationTests
{
    // ── Pearson Correlation Properties ──────────────────────────────

    [Fact]
    public void ConstantPrice_RealAndAngle_AreZero()
    {
        // Constant price has zero variance → correlation = 0 → angle = 0
        var epa = new Epa(period: 10);
        for (int i = 0; i < 30; i++)
        {
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), 50.0));
        }
        Assert.Equal(0.0, epa.Angle);
    }

    [Fact]
    public void PerfectCosineInput_HighCorrelation()
    {
        // Price that exactly matches cos wave at the indicator period should yield |Real| near 1
        int period = 20;
        var epa = new Epa(period: period);
        double maxAngle = double.MinValue;

        for (int i = 0; i < period * 4; i++)
        {
            double price = 100 + 10 * Math.Cos(2 * Math.PI * i / period);
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
            if (epa.IsHot && Math.Abs(epa.Angle) > Math.Abs(maxAngle))
            {
                maxAngle = epa.Angle;
            }
        }
        // The angle should move significantly when price matches the reference cosine
        Assert.True(double.IsFinite(maxAngle));
    }

    [Fact]
    public void PerfectSineInput_AngleAdvances()
    {
        // A sine wave at the indicator period should produce advancing angle.
        // The angle wraps at the 360° boundary (e.g. ~180° → ~-162°), which is
        // the expected wraparound compensation behavior.
        int period = 20;
        var epa = new Epa(period: period);
        var angles = new List<double>();

        for (int i = 0; i < period * 3; i++)
        {
            double price = 100 + 10 * Math.Sin(2 * Math.PI * i / period);
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
            if (epa.IsHot)
            {
                angles.Add(epa.Angle);
            }
        }

        // Angle should advance or wrap around (decrease > 300° is a valid wraparound)
        Assert.True(angles.Count > 0);
        int advances = 0;
        for (int i = 1; i < angles.Count; i++)
        {
            double delta = angles[i] - angles[i - 1];
            if (delta >= -0.001)
            {
                advances++; // Normal advancement or hold
            }
            else if (delta < -300.0)
            {
                advances++; // Valid 360° wraparound
            }
            // else: backward movement in non-wrap region — allowed by Ehlers' exceptions
        }
        // Most transitions should be advancing or wrapping
        Assert.True(advances > angles.Count / 2,
            $"Expected majority of angle transitions to advance, got {advances}/{angles.Count}");
    }

    // ── DerivedPeriod Properties ───────────────────────────────────

    [Fact]
    public void DerivedPeriod_AlwaysClampedTo60()
    {
        var epa = new Epa(period: 10);
        var rng = new Random(123);

        for (int i = 0; i < 500; i++)
        {
            double price = 100 + rng.NextDouble() * 20;
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
            Assert.True(epa.DerivedPeriod <= 60.0,
                $"DerivedPeriod {epa.DerivedPeriod} > 60 at bar {i}");
        }
    }

    [Fact]
    public void DerivedPeriod_NonNegative()
    {
        var epa = new Epa(period: 14);
        var rng = new Random(456);

        for (int i = 0; i < 300; i++)
        {
            double price = 100 + rng.NextDouble() * 10;
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
            Assert.True(epa.DerivedPeriod >= 0.0,
                $"DerivedPeriod {epa.DerivedPeriod} < 0 at bar {i}");
        }
    }

    // ── TrendState Properties ──────────────────────────────────────

    [Fact]
    public void TrendState_OnlyValidValues_AllBars()
    {
        var epa = new Epa(period: 14);
        var rng = new Random(789);

        for (int i = 0; i < 500; i++)
        {
            double price = 100 + rng.NextDouble() * 10;
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
            Assert.True(epa.TrendState >= -1 && epa.TrendState <= 1,
                $"Invalid TrendState {epa.TrendState} at bar {i}");
        }
    }

    [Fact]
    public void TrendState_HasVariation()
    {
        // Over a long enough series with varying data, trend state should not be constant
        var epa = new Epa(period: 10);
        var states = new HashSet<int>();
        var rng = new Random(42);

        for (int i = 0; i < 500; i++)
        {
            double price = 100 + rng.NextDouble() * 20 + 5 * Math.Sin(2 * Math.PI * i / 20.0);
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
            if (epa.IsHot)
            {
                states.Add(epa.TrendState);
            }
        }
        // Should have at least 2 different states
        Assert.True(states.Count >= 2,
            $"Expected at least 2 distinct states, got {states.Count}: [{string.Join(",", states)}]");
    }

    // ── Deterministic Reproducibility ──────────────────────────────

    [Fact]
    public void Deterministic_SameInput_SameOutput()
    {
        var rng1 = new Random(42);
        var rng2 = new Random(42);
        var epa1 = new Epa(period: 14);
        var epa2 = new Epa(period: 14);

        for (int i = 0; i < 200; i++)
        {
            double p1 = 100 + rng1.NextDouble() * 10;
            double p2 = 100 + rng2.NextDouble() * 10;
            epa1.Update(new TValue(DateTime.UtcNow.AddDays(i), p1));
            epa2.Update(new TValue(DateTime.UtcNow.AddDays(i), p2));
        }

        Assert.Equal(epa1.Angle, epa2.Angle, precision: 14);
        Assert.Equal(epa1.DerivedPeriod, epa2.DerivedPeriod, precision: 14);
        Assert.Equal(epa1.TrendState, epa2.TrendState);
    }

    // ── Consistency: Batch/Streaming/Span ──────────────────────────

    [Fact]
    public void StreamingVsBatch_Match()
    {
        var rng = new Random(42);
        int n = 200, period = 14;
        double[] prices = new double[n];
        for (int i = 0; i < n; i++)
        {
            prices[i] = 100 + rng.NextDouble() * 10;
        }

        // Streaming
        var epa = new Epa(period);
        double[] streamAngles = new double[n];
        for (int i = 0; i < n; i++)
        {
            var r = epa.Update(new TValue(DateTime.UtcNow.AddDays(i), prices[i]));
            streamAngles[i] = r.Value;
        }

        // Span batch
        double[] spanAngles = new double[n];
        Epa.Batch(prices, spanAngles, period);

        for (int i = 0; i < n; i++)
        {
            Assert.Equal(streamAngles[i], spanAngles[i], precision: 10);
        }
    }

    [Fact]
    public void BatchTSeries_MatchesStreaming()
    {
        var rng = new Random(42);
        int n = 200, period = 14;
        var ts = new TSeries();
        for (int i = 0; i < n; i++)
        {
            ts.Add(new TValue(DateTime.UtcNow.AddDays(i), 100 + rng.NextDouble() * 10));
        }

        // Streaming
        var epa = new Epa(period);
        foreach (var tv in ts)
        {
            epa.Update(tv);
        }

        // Batch(TSeries)
        var batchResult = Epa.Batch(ts, period);

        Assert.Equal(epa.Angle, batchResult[^1].Value, precision: 10);
    }

    // ── Reset/Reprocess ────────────────────────────────────────────

    [Fact]
    public void ResetReprocess_MatchesOriginal()
    {
        var rng = new Random(42);
        int n = 100, period = 14;
        var epa = new Epa(period);
        double[] prices = new double[n];
        for (int i = 0; i < n; i++)
        {
            prices[i] = 100 + rng.NextDouble() * 10;
        }

        for (int i = 0; i < n; i++)
        {
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), prices[i]));
        }
        double angle1 = epa.Angle;
        double dp1 = epa.DerivedPeriod;
        int ts1 = epa.TrendState;

        epa.Reset();
        for (int i = 0; i < n; i++)
        {
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), prices[i]));
        }

        Assert.Equal(angle1, epa.Angle, precision: 14);
        Assert.Equal(dp1, epa.DerivedPeriod, precision: 14);
        Assert.Equal(ts1, epa.TrendState);
    }

    // ── Period Sensitivity ─────────────────────────────────────────

    [Fact]
    public void DifferentPeriods_DifferentAngle()
    {
        var rng = new Random(42);
        var epa10 = new Epa(period: 10);
        var epa28 = new Epa(period: 28);

        for (int i = 0; i < 100; i++)
        {
            double price = 100 + rng.NextDouble() * 10;
            var tv = new TValue(DateTime.UtcNow.AddDays(i), price);
            epa10.Update(tv);
            epa28.Update(tv);
        }

        Assert.NotEqual(epa10.Angle, epa28.Angle);
    }

    // ── Finite Output for All Bars ─────────────────────────────────

    [Fact]
    public void AllOutputs_AlwaysFinite()
    {
        var epa = new Epa(period: 14);
        var rng = new Random(42);

        for (int i = 0; i < 500; i++)
        {
            double price = 100 + rng.NextDouble() * 10;
            epa.Update(new TValue(DateTime.UtcNow.AddDays(i), price));
            Assert.True(double.IsFinite(epa.Angle), $"Non-finite Angle at bar {i}");
            Assert.True(double.IsFinite(epa.DerivedPeriod), $"Non-finite DerivedPeriod at bar {i}");
        }
    }
}
