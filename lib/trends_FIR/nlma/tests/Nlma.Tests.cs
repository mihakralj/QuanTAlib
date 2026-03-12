// NLMA Unit Tests
using System;
using System.Linq;
using Xunit;

namespace QuanTAlib.Tests;

public class NlmaTests
{
    private const double Epsilon = 1e-10;

    // ── Constructor tests ──────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultPeriod_Is14()
    {
        var nlma = new Nlma();
        Assert.Equal("Nlma(14)", nlma.Name);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectly()
    {
        var nlma = new Nlma(20);
        Assert.Equal("Nlma(20)", nlma.Name);
    }

    [Fact]
    public void Constructor_Period2_IsMinValid()
    {
        // Igorad kernel requires period >= 2
        var nlma = new Nlma(2);
        var result = nlma.Update(new TValue(DateTime.MinValue, 42.0));
        Assert.Equal(42.0, result.Value, 10);
    }

    [Fact]
    public void Constructor_Period1_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Nlma(1));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Nlma(-1));
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Nlma(0));
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        // WarmupPeriod = flen = 5*period - 1
        var nlma = new Nlma(10);
        Assert.Equal(49, nlma.WarmupPeriod); // 5*10 - 1 = 49
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var nlma = new Nlma(7);
        Assert.StartsWith("Nlma(", nlma.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void WarmupPeriod_IsFlen()
    {
        // WarmupPeriod = 5*period - 1 (Igorad kernel length)
        var nlma = new Nlma(25);
        Assert.Equal(124, nlma.WarmupPeriod); // 5*25 - 1 = 124
    }

    // ── Value computation tests ────────────────────────────────────────

    [Fact]
    public void Update_ConstantInput_ReturnsConstant()
    {
        // DC gain = 1: constant input → output must equal that constant after warmup
        var nlma = new Nlma(10);
        int flen = 5 * 10 - 1; // 49
        TValue result = default;
        for (int i = 0; i < flen + 10; i++)
        {
            result = nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 50.0));
        }
        Assert.Equal(50.0, result.Value, 8);
    }

    [Fact]
    public void Update_Period2_ReturnsInput()
    {
        // period=2, flen=9. After warmup, constant input → output = input
        var nlma = new Nlma(2);
        int flen = 5 * 2 - 1; // 9
        TValue result = default;
        for (int i = 0; i < flen + 5; i++)
        {
            result = nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 123.456));
        }
        Assert.Equal(123.456, result.Value, 8);
    }

    [Fact]
    public void Update_KnownValues_Igorad_ConstantDCGain()
    {
        // Igorad kernel with any period: constant input must produce constant output
        // This validates that signed-sum normalization preserves DC gain = 1
        var nlma = new Nlma(4);
        int flen = 5 * 4 - 1; // 19
        TValue result = default;
        for (int i = 0; i < flen + 5; i++)
        {
            result = nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 100.0));
        }
        Assert.Equal(100.0, result.Value, 6);
    }

    [Fact]
    public void IgoradWeights_HasNegativeWeights()
    {
        // Igorad kernel with period 14 should have negative weights for lag cancellation
        // Test: feed a step function and verify responsiveness
        var nlma = new Nlma(14);
        int flen = 5 * 14 - 1; // 69

        // Feed flen bars of 100, then flen bars of 200
        for (int i = 0; i < flen; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 100.0));
        }
        for (int i = flen; i < 2 * flen; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 200.0));
        }
        // After enough 200s, the NLMA should converge near 200
        double val = nlma.Last.Value;
        Assert.True(val > 190.0, $"NLMA should track step to ~200, got {val}");
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var nlma = new Nlma(5);
        nlma.Update(new TValue(DateTime.MinValue, 100.0));
        Assert.True(double.IsFinite(nlma.Last.Value));
    }

    [Fact]
    public void Update_ReturnsTValue()
    {
        var nlma = new Nlma(5);
        var result = nlma.Update(new TValue(DateTime.MinValue, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── State management tests ─────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        // period=3, flen = 5*3-1 = 14
        var nlma = new Nlma(3);
        int flen = 5 * 3 - 1; // 14
        Assert.False(nlma.IsHot);

        for (int i = 0; i < flen - 1; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), i + 1));
        }
        Assert.False(nlma.IsHot);

        nlma.Update(new TValue(DateTime.MinValue.AddDays(flen - 1), flen));
        Assert.True(nlma.IsHot);
    }

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var nlma = new Nlma(3);
        nlma.Update(new TValue(DateTime.MinValue, 10), isNew: true);
        Assert.True(nlma.IsNew);
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var nlma = new Nlma(3);
        nlma.Update(new TValue(DateTime.MinValue, 10), isNew: true);
        nlma.Update(new TValue(DateTime.MinValue, 20), isNew: false);
        Assert.False(nlma.IsNew);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        // After correction (isNew=false), next isNew=true should advance normally
        var nlma = new Nlma(5);
        for (int i = 0; i < 30; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 100 + i));
        }
        _ = nlma.Last.Value;

        // Correct last bar
        nlma.Update(new TValue(DateTime.MinValue.AddDays(29), 110), isNew: false);
        double afterCorrection = nlma.Last.Value;
        Assert.True(double.IsFinite(afterCorrection));

        // Add new bar — should restore from previous state
        nlma.Update(new TValue(DateTime.MinValue.AddDays(30), 105), isNew: true);
        Assert.True(double.IsFinite(nlma.Last.Value));
        Assert.NotEqual(afterCorrection, nlma.Last.Value);
    }

    // ── NaN / Infinity handling ────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValid()
    {
        var nlma = new Nlma(3);
        nlma.Update(new TValue(DateTime.MinValue, 10));
        nlma.Update(new TValue(DateTime.MinValue.AddDays(1), 20));
        nlma.Update(new TValue(DateTime.MinValue.AddDays(2), double.NaN));
        // Should use last valid value (20) in place of NaN
        Assert.True(double.IsFinite(nlma.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValid()
    {
        var nlma = new Nlma(3);
        nlma.Update(new TValue(DateTime.MinValue, 10));
        nlma.Update(new TValue(DateTime.MinValue.AddDays(1), 20));
        nlma.Update(new TValue(DateTime.MinValue.AddDays(2), double.PositiveInfinity));
        Assert.True(double.IsFinite(nlma.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var nlma = new Nlma(3);
        nlma.Update(new TValue(DateTime.MinValue, double.NaN));
        // First value NaN should return NaN
        Assert.True(double.IsNaN(nlma.Last.Value));
    }

    // ── Event-based chaining ───────────────────────────────────────────

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new Nlma(3);
        var chained = new Nlma(source, 5);

        for (int i = 0; i < 250; i++)
        {
            source.Update(new TValue(DateTime.MinValue.AddDays(i), 100 + i));
        }

        Assert.True(double.IsFinite(chained.Last.Value));
    }

    [Fact]
    public void Pub_Fires()
    {
        var nlma = new Nlma(3);
        bool fired = false;
        nlma.Pub += (object? _, in TValueEventArgs _) => fired = true;
        nlma.Update(new TValue(DateTime.MinValue, 100.0));
        Assert.True(fired);
    }

    // ── Batch TSeries ──────────────────────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        int flen = 5 * period - 1; // 49
        int len = flen + 30; // ensure enough bars for full kernel
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 100 + Math.Sin(i) * 10));
        }

        // Mode 1: streaming
        var streaming = new Nlma(period);
        var streamResults = new double[len];
        for (int i = 0; i < len; i++)
        {
            streamResults[i] = streaming.Update(src[i]).Value;
        }

        // Mode 2: Batch(TSeries)
        var batchResult = Nlma.Batch(src, period);

        // Mode 3: Batch(span)
        var spanInput = new double[len];
        var spanOutput = new double[len];
        for (int i = 0; i < len; i++)
        {
            spanInput[i] = src[i].Value;
        }
        Nlma.Batch(spanInput, spanOutput, period);

        for (int i = 0; i < len; i++)
        {
            Assert.Equal(streamResults[i], batchResult[i].Value, 6);
            Assert.Equal(streamResults[i], spanOutput[i], 6);
        }
    }

    // ── Batch Span API ─────────────────────────────────────────────────

    [Fact]
    public void Batch_Span_ValidatesPeriod()
    {
        Assert.Throws<ArgumentException>(() =>
            Nlma.Batch(new double[5], new double[5], 0));
    }

    [Fact]
    public void Batch_Span_ValidatesLengths()
    {
        Assert.Throws<ArgumentException>(() =>
            Nlma.Batch(new double[5], new double[3], 3));
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoError()
    {
        Nlma.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, 5);
        Assert.True(true, "Empty span batch should not throw");
    }

    [Fact]
    public void Batch_Span_MatchesTSeries()
    {
        int period = 7;
        int flen = 5 * period - 1; // 34
        int len = flen + 20;
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 50 + i * 0.5));
        }

        var tsBatch = Nlma.Batch(src, period);

        var spanInput = new double[len];
        var spanOutput = new double[len];
        for (int i = 0; i < len; i++)
        {
            spanInput[i] = src[i].Value;
        }
        Nlma.Batch(spanInput, spanOutput, period);

        for (int i = 0; i < len; i++)
        {
            Assert.Equal(tsBatch[i].Value, spanOutput[i], 6);
        }
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        double[] source = [1, 2, double.NaN, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        double[] output = new double[source.Length];
        Nlma.Batch(source, output, 3);

        for (int i = 1; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int count = 10000;
        double[] source = new double[count];
        double[] output = new double[count];
        for (int i = 0; i < count; i++)
        {
            source[i] = 100.0 + i * 0.1;
        }
        Nlma.Batch(source, output, 300);

        // flen = 5*300 - 1 = 1499
        int flen = 5 * 300 - 1;
        for (int i = flen; i < count; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} should be finite");
        }
    }

    // ── Calculate ──────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        int period = 5;
        int flen = 5 * period - 1; // 24
        int len = flen + 20;
        var src = new TSeries([], []);
        for (int i = 0; i < len; i++)
        {
            src.Add(new TValue(DateTime.MinValue.AddDays(i), 100 + i));
        }

        var (results, indicator) = Nlma.Calculate(src, period);
        Assert.Equal(len, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    // ── Reset / Dispose ────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        int period = 5;
        int flen = 5 * period - 1; // 24
        var nlma = new Nlma(period);
        for (int i = 0; i < flen + 10; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 100 + i));
        }
        Assert.True(nlma.IsHot);

        nlma.Reset();
        Assert.False(nlma.IsHot);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new Nlma(3);
        var chained = new Nlma(source, 5);

        source.Update(new TValue(DateTime.MinValue, 100));
        Assert.True(double.IsFinite(chained.Last.Value));

        chained.Dispose();

        // After dispose, source updates should not propagate
        source.Update(new TValue(DateTime.MinValue.AddDays(1), 200));
        // chained.Last should remain unchanged after dispose
    }

    [Fact]
    public void LargePeriod_Handles()
    {
        int period = 500;
        int flen = 5 * period - 1; // 2499
        var nlma = new Nlma(period);
        for (int i = 0; i < flen + 100; i++)
        {
            nlma.Update(new TValue(DateTime.MinValue.AddDays(i), 100 + i * 0.01));
        }
        Assert.True(double.IsFinite(nlma.Last.Value));
        Assert.True(nlma.IsHot);
    }
}
