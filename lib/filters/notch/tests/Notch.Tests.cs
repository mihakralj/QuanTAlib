using System;
using System.Linq;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class NotchTests
{
    private readonly GBM _gbm;

    public NotchTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Notch(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Notch(10, -0.5));
    }

    [Fact]
    public void Constructor_Period1_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Notch(1));
    }

    [Fact]
    public void Constructor_ZeroQ_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Notch(10, 0));
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var notch = new Notch(10, 2.0);
        Assert.Equal(10, notch.NotchFreq);
        Assert.Equal(2.0, notch.Bandwidth);
        Assert.Equal("Notch(10,2)", notch.Name);
        Assert.Equal(10, notch.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultQ()
    {
        var notch = new Notch(10);
        Assert.Equal(1.0, notch.Bandwidth);
    }

    [Fact]
    public void Constructor_WithPublisher_Subscribes()
    {
        var source = new TSeries();
        var notch = new Notch(source, 10, 1.0);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(notch.Last.Value));
    }

    // ── IsHot ───────────────────────────────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var notch = new Notch(10, 1.0);
        Assert.False(notch.IsHot);

        for (int i = 0; i < 10; i++)
        {
            notch.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.True(notch.IsHot);
    }

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var notch = new Notch(10, 1.0);
        for (int i = 0; i < 9; i++)
        {
            notch.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.False(notch.IsHot);
    }

    // ── Update ──────────────────────────────────────────────────────────

    [Fact]
    public void Calc_ReturnsValue()
    {
        var notch = new Notch(period: 10);
        var result = notch.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_BarCorrection_Works()
    {
        var notch = new Notch(10, 1.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            notch.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // New bar
        var result1 = notch.Update(new TValue(now.AddMinutes(15), 200), isNew: true);

        // Correction
        notch.Update(new TValue(now.AddMinutes(15), 150), isNew: false);

        // Restore to original value
        notch.Update(new TValue(now.AddMinutes(15), 200), isNew: false);
        var restored = notch.Last;

        Assert.Equal(result1.Value, restored.Value, 1e-10);
    }

    // ── NaN handling ────────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var notch = new Notch(10, 1.0);
        notch.Update(new TValue(DateTime.UtcNow, 100));

        var result = notch.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var notch = new Notch(10, 1.0);
        notch.Update(new TValue(DateTime.UtcNow, 100));

        var result = notch.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── Reset ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var notch = new Notch(10, 1.0);
        for (int i = 0; i < 15; i++)
        {
            notch.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(notch.IsHot);

        notch.Reset();
        Assert.False(notch.IsHot);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var notch = new Notch(10, 1.0);
        for (int i = 0; i < 20; i++)
        {
            notch.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        var firstResult = notch.Last.Value;

        notch.Reset();
        for (int i = 0; i < 20; i++)
        {
            notch.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.Equal(firstResult, notch.Last.Value, 1e-10);
    }

    // ── Filter behavior ─────────────────────────────────────────────────

    [Fact]
    public void Notch_Passes_DC()
    {
        // DC input (constant value) should pass through with Gain 1
        var notch = new Notch(period: 10, q: 1.0);
        double input = 100.0;
        double output = 0;

        // Warmup to stabilize (IIR transient)
        for (int i = 0; i < 100; i++)
        {
            output = notch.Update(new TValue(DateTime.UtcNow, input)).Value;
        }

        Assert.Equal(input, output, precision: 6);
    }

    [Fact]
    public void Notch_Attenuates_CenterFrequency()
    {
        // Period 10 means frequency is 1/10 cycles per sample.
        int period = 10;
        double q = 5.0; // High Q for sharp notch
        var notch = new Notch(period, q);

        double omega = 2.0 * Math.PI / period;

        double maxAmp = 0;
        for (int i = 0; i < 200; i++)
        {
            double val = Math.Sin(omega * i); // Input amplitude 1
            double outVal = notch.Update(new TValue(DateTime.UtcNow, val)).Value;

            if (i > 50) // ignore transient
            {
                maxAmp = Math.Max(maxAmp, Math.Abs(outVal));
            }
        }

        // At exact notch frequency, ideal is 0.
        Assert.True(maxAmp < 0.1, $"Amplitude {maxAmp} should be attenuated ( < 0.1 )");
    }

    [Fact]
    public void Notch_PassesNonNotchFrequency()
    {
        // Non-notch frequency should pass through with ~unity gain
        int notchPeriod = 10;
        var notch = new Notch(notchPeriod, 1.0);

        // Use a frequency far from the notch (period 50 instead of 10)
        double omega = 2.0 * Math.PI / 50.0;
        double maxAmp = 0;

        for (int i = 0; i < 300; i++)
        {
            double val = Math.Sin(omega * i);
            double outVal = notch.Update(new TValue(DateTime.UtcNow, val)).Value;

            if (i > 100) // ignore transient
            {
                maxAmp = Math.Max(maxAmp, Math.Abs(outVal));
            }
        }

        // At non-notch frequency, output should be close to input amplitude (1.0)
        Assert.True(maxAmp > 0.7, $"Non-notch amplitude {maxAmp} should be high (> 0.7)");
    }

    // ── Batch ───────────────────────────────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int period = 10;
        double q = 1.0;
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Static Batch(TSeries)
        var staticResult = Notch.Batch(series, period, q);

        // 2. Static Batch(Span)
        double[] spanResult = new double[series.Count];
        Notch.Batch(series.Values, spanResult.AsSpan(), period, q);

        // 3. Instance Update (TSeries)
        var instance = new Notch(period, q);
        var instanceSeriesResult = instance.Update(series);

        // 4. Instance Update (Streaming)
        var streamingInstance = new Notch(period, q);
        double[] streamingResult = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamingResult[i] = streamingInstance.Update(series[i]).Value;
        }

        // Assert
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(staticResult[i].Value, spanResult[i], precision: 9);
            Assert.Equal(staticResult[i].Value, instanceSeriesResult[i].Value, precision: 9);
            Assert.Equal(staticResult[i].Value, streamingResult[i], precision: 9);
        }
    }

    [Fact]
    public void Batch_TSeries_Static()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var result = Notch.Batch(data.Close, 10, 1.0);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var notch = new Notch(10, 1.0);
        var result = notch.Update(new TSeries());
        Assert.Empty(result);
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Notch.Calculate(data.Close, 10, 1.0);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var notch = new Notch(10, 1.0);
        var values = new double[15];
        for (int i = 0; i < 15; i++)
        {
            values[i] = 100 + i;
        }

        notch.Prime(values);
        Assert.True(notch.IsHot);
    }

    // ── Dispose ─────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_WithPublisher_Unsubscribes()
    {
        var source = new TSeries();
        var notch = new Notch(source, 10, 1.0);
        source.Add(new TValue(DateTime.UtcNow, 100));

        notch.Dispose();
        var lastBefore = notch.Last;
        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(lastBefore, notch.Last);
    }

    [Fact]
    public void Dispose_WithoutPublisher_DoesNotThrow()
    {
        var notch = new Notch(10, 1.0);
        notch.Update(new TValue(DateTime.UtcNow, 100));
        notch.Dispose();
        Assert.True(true); // S2699: explicit assertion for dispose-only test
    }

    // ── Determinism ─────────────────────────────────────────────────────

    [Fact]
    public void TwoInstances_SameInput_SameOutput()
    {
        var n1 = new Notch(10, 1.0);
        var n2 = new Notch(10, 1.0);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            var r1 = n1.Update(tv);
            var r2 = n2.Update(tv);
            Assert.Equal(r1.Value, r2.Value);
        }
    }

    // ── Q Factor ────────────────────────────────────────────────────────

    [Fact]
    public void DifferentQ_ProduceDifferentOutputs()
    {
        var narrowNotch = new Notch(10, 0.5);
        var wideNotch = new Notch(10, 5.0);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double lastNarrow = 0, lastWide = 0;
        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            lastNarrow = narrowNotch.Update(tv).Value;
            lastWide = wideNotch.Update(tv).Value;
        }

        Assert.NotEqual(lastNarrow, lastWide, 1e-6);
    }
}
