using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class DstochTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.5, seed: 42);

    // ── A. Constructor / defaults ──

    [Fact]
    public void Constructor_Default_SetsName()
    {
        var d = new Dstoch();
        Assert.Equal("Dstoch(21)", d.Name);
    }

    [Fact]
    public void Constructor_Custom_SetsName()
    {
        var d = new Dstoch(10);
        Assert.Equal("Dstoch(10)", d.Name);
    }

    [Fact]
    public void Constructor_Default_WarmupPeriodIsPeriod()
    {
        var d = new Dstoch(10);
        Assert.Equal(10, d.WarmupPeriod);
    }

    [Fact]
    public void Constructor_Default_NotHotBeforeFirstBar()
    {
        var d = new Dstoch();
        Assert.False(d.IsHot);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Dstoch(0));
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Dstoch(-5));
    }

    // ── B. Core update behavior ──

    [Fact]
    public void Update_BasicBar_ProducesFiniteResult()
    {
        var d = new Dstoch(5);
        var bar = new TBar(DateTime.UtcNow, 105, 110, 100, 107, 1000);
        var result = d.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_LastMatchesReturnValue()
    {
        var d = new Dstoch(5);
        var bar = new TBar(DateTime.UtcNow, 105, 110, 100, 107, 1000);
        var result = d.Update(bar);
        Assert.Equal(result.Value, d.Last.Value, 15);
    }

    [Fact]
    public void IsHot_FalseForFirstBar_TrueAfterPeriod()
    {
        var d = new Dstoch(3);
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 7);
        for (int i = 0; i < 10; i++)
        {
            d.Update(gbm.Next(isNew: true));
            if (i < 2) { Assert.False(d.IsHot); }
            else { Assert.True(d.IsHot); }
        }
    }

    // ── C. Boundedness [0, 100] ──

    [Fact]
    public void Output_BoundedZeroToHundred()
    {
        var d = new Dstoch(10);
        var gbm = new GBM(100.0, 0.05, 0.3, seed: 11);
        for (int i = 0; i < 200; i++)
        {
            d.Update(gbm.Next(isNew: true));
            if (d.IsHot)
            {
                Assert.InRange(d.Last.Value, -0.01, 100.01);
            }
        }
    }

    [Fact]
    public void Output_ConstantBars_IsZero()
    {
        var d = new Dstoch(5);
        for (int i = 0; i < 20; i++)
        {
            d.Update(new TBar(DateTime.UtcNow.AddDays(i), 100, 100, 100, 100, 1000));
        }
        Assert.Equal(0.0, d.Last.Value, 10);
    }

    // ── D. NaN / edge cases ──

    [Fact]
    public void Update_NaNHigh_ResultIsFinite()
    {
        var d = new Dstoch(3);
        d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 500));
        d.Update(new TBar(DateTime.UtcNow.AddDays(1), 102, double.NaN, 92, 100, 500));
        Assert.True(double.IsFinite(d.Last.Value));
    }

    [Fact]
    public void Update_NaNVolume_NoImpact()
    {
        var d = new Dstoch(3);
        var result = d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AllNaN_ReturnsNaN()
    {
        var d = new Dstoch(3);
        var result = d.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));
        Assert.True(double.IsNaN(result.Value));
    }

    // ── E. isNew=false bar correction ──

    [Fact]
    public void Update_IsNewFalse_RewritesLastBar()
    {
        var d = new Dstoch(5);
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 99);
        for (int i = 0; i < 8; i++) { d.Update(gbm.Next(isNew: true)); }

        d.Update(gbm.Next(isNew: true));
        double original = d.Last.Value;

        // Correct with a different bar
        var corrected = new TBar(DateTime.UtcNow.AddDays(99), 200, 250, 150, 220, 5000);
        d.Update(corrected, isNew: false);
        double correctedVal = d.Last.Value;

        Assert.NotEqual(original, correctedVal);
    }

    [Fact]
    public void Update_BarCorrection_PreservesCount()
    {
        var d = new Dstoch(3);
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 33);
        for (int i = 0; i < 5; i++) { d.Update(gbm.Next(isNew: true)); }

        bool hotBefore = d.IsHot;
        d.Update(new TBar(DateTime.UtcNow.AddDays(99), 100, 110, 90, 105, 500), isNew: false);
        Assert.Equal(hotBefore, d.IsHot);
    }

    // ── F. Reset ──

    [Fact]
    public void Reset_ClearsState()
    {
        var d = new Dstoch(5);
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 44);
        for (int i = 0; i < 20; i++) { d.Update(gbm.Next(isNew: true)); }
        Assert.True(d.IsHot);

        d.Reset();
        Assert.False(d.IsHot);
        Assert.Equal(0.0, d.Last.Value);
    }

    // ── G. Pub event ──

    [Fact]
    public void PubEvent_Fires_OnUpdate()
    {
        var d = new Dstoch(3);
        int count = 0;
        d.Pub += (object? _, in TValueEventArgs _) => count++;
        d.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 500));
        Assert.Equal(1, count);
    }

    // ── H. TBarSeries chaining ──

    [Fact]
    public void TBarSeries_Chaining_Works()
    {
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 55);
        for (int i = 0; i < 30; i++)
        {
            source.Add(gbm.Next(isNew: true));
        }
        var d = new Dstoch(source, 10);
        Assert.True(d.IsHot);
        Assert.True(double.IsFinite(d.Last.Value));
    }

    // ── I. Batch methods ──

    [Fact]
    public void Batch_EmptySpans_NoThrow()
    {
        Span<double> empty = [];
        Span<double> output = [];
        Dstoch.Batch(empty, empty, empty, output, 5);
        Assert.True(true);
    }

    [Fact]
    public void Batch_MismatchedLength_Throws()
    {
        double[] h = [1, 2, 3];
        double[] l = [1, 2];
        double[] c = [1, 2, 3];
        double[] o = new double[3];
        Assert.Throws<ArgumentException>(() =>
            Dstoch.Batch(h, l, c, o, 5));
    }

    [Fact]
    public void Batch_OutputTooShort_Throws()
    {
        double[] h = [1, 2, 3];
        double[] l = [1, 2, 3];
        double[] c = [1, 2, 3];
        double[] o = new double[2];
        Assert.Throws<ArgumentException>(() =>
            Dstoch.Batch(h, l, c, o, 5));
    }

    [Fact]
    public void Batch_KnownValues_BoundedOutput()
    {
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 66);
        for (int i = 0; i < 50; i++) { source.Add(gbm.Next(isNew: true)); }

        var result = Dstoch.Batch(source, 10);
        for (int i = 10; i < result.Count; i++)
        {
            Assert.InRange(result[i].Value, -0.01, 100.01);
        }
    }

    // ── J. Streaming ↔ Batch consistency ──

    [Fact]
    public void Consistency_StreamingMatchesBatch()
    {
        const int period = 10;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 77);
        for (int i = 0; i < 100; i++) { source.Add(gbm.Next(isNew: true)); }

        var batch = Dstoch.Batch(source, period);

        var streaming = new Dstoch(period);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            Assert.Equal(batch[i].Value, streaming.Last.Value, 10);
        }
    }

    [Fact]
    public void Consistency_EventBasedMatchesStreaming()
    {
        const int period = 7;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 88);

        var d1 = new Dstoch(period);
        var d2 = new Dstoch(period);
        var eventValues = new List<double>();
        d2.Pub += (object? _, in TValueEventArgs e) => eventValues.Add(e.Value.Value);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            d1.Update(bar);
            d2.Update(bar);
        }

        Assert.Equal(50, eventValues.Count);
    }

    // ── K. Large dataset stability ──

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        const int N = 5000;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.3, seed: 123);
        for (int i = 0; i < N; i++) { source.Add(gbm.Next(isNew: true)); }

        var result = Dstoch.Batch(source, 21);
        Assert.Equal(N, result.Count);
    }

    [Fact]
    public void Batch_ZeroPeriod_Throws()
    {
        double[] h = [1, 2, 3];
        double[] l = [1, 2, 3];
        double[] c = [1, 2, 3];
        double[] o = new double[3];
        Assert.Throws<ArgumentException>(() =>
            Dstoch.Batch(h, l, c, o, 0));
    }

    // ── L. Calculate factory ──

    [Fact]
    public void Calculate_ReturnsIndicatorAndResults()
    {
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 99);
        for (int i = 0; i < 50; i++) { source.Add(gbm.Next(isNew: true)); }

        var (results, indicator) = Dstoch.Calculate(source, 10);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }
}
