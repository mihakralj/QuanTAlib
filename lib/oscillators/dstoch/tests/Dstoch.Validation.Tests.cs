using Xunit;

namespace QuanTAlib.Tests;

public sealed class DstochValidationTests
{
    // ── Self-consistency: streaming == batch ──

    [Fact]
    public void StreamingMatchesBatch()
    {
        const int period = 14;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.3, seed: 42);
        for (int i = 0; i < 100; i++) { source.Add(gbm.Next(isNew: true)); }

        var batch = Dstoch.Batch(source, period);

        var streaming = new Dstoch(period);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            Assert.Equal(batch[i].Value, streaming.Last.Value, 10);
        }
    }

    // ── Span matches TBarSeries batch ──

    [Fact]
    public void SpanMatchesTBarSeries()
    {
        const int period = 10;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 55);
        for (int i = 0; i < 80; i++) { source.Add(gbm.Next(isNew: true)); }

        var tbResult = Dstoch.Batch(source, period);

        var spanOut = new double[source.Count];
        Dstoch.Batch(source.HighValues, source.LowValues, source.CloseValues,
            spanOut.AsSpan(), period);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(tbResult[i].Value, spanOut[i], 10);
        }
    }

    // ── Determinism ──

    [Fact]
    public void Deterministic_AcrossRuns()
    {
        const int period = 10;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 77);
        for (int i = 0; i < 60; i++) { source.Add(gbm.Next(isNew: true)); }

        var r1 = Dstoch.Batch(source, period);
        var r2 = Dstoch.Batch(source, period);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(r1[i].Value, r2[i].Value, 15);
        }
    }

    // ── Constant input ──

    [Fact]
    public void ConstantBars_OutputIsZero()
    {
        const int period = 5;
        var bars = new TBarSeries();
        for (int i = 0; i < 30; i++)
        {
            bars.Add(new TBar(DateTime.UtcNow.AddDays(i), 50, 50, 50, 50, 100));
        }

        var result = Dstoch.Batch(bars, period);
        for (int i = period; i < result.Count; i++)
        {
            Assert.Equal(0.0, result[i].Value, 10);
        }
    }

    // ── Boundedness ──

    [Fact]
    public void Output_AlwaysBoundedZeroToHundred()
    {
        const int period = 14;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.3, seed: 88);
        for (int i = 0; i < 200; i++) { source.Add(gbm.Next(isNew: true)); }

        var result = Dstoch.Batch(source, period);

        for (int i = period; i < result.Count; i++)
        {
            Assert.InRange(result[i].Value, -0.01, 100.01);
        }
    }

    // ── Different periods produce different results ──

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.3, seed: 99);
        for (int i = 0; i < 100; i++) { source.Add(gbm.Next(isNew: true)); }

        var r5 = Dstoch.Batch(source, 5);
        var r21 = Dstoch.Batch(source, 21);

        bool anyDifferent = false;
        for (int i = 25; i < source.Count; i++)
        {
            if (Math.Abs(r5[i].Value - r21[i].Value) > 1e-6)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    // ── Monotonic-up → high DSS ──

    [Fact]
    public void MonotonicUp_ConvergesHighDSS()
    {
        var d = new Dstoch(5);
        for (int i = 0; i < 30; i++)
        {
            double price = 100 + i;
            d.Update(new TBar(DateTime.UtcNow.AddDays(i), price, price + 1, price - 1, price, 1000));
        }
        Assert.True(d.Last.Value > 50.0);
    }

    // ── Monotonic-down → low DSS ──

    [Fact]
    public void MonotonicDown_ConvergesLowDSS()
    {
        var d = new Dstoch(5);
        for (int i = 0; i < 30; i++)
        {
            double price = 200 - i;
            d.Update(new TBar(DateTime.UtcNow.AddDays(i), price, price + 1, price - 1, price, 1000));
        }
        Assert.True(d.Last.Value < 50.0);
    }

    // ── Reset+replay matches fresh run ──

    [Fact]
    public void ResetReplay_MatchesFreshRun()
    {
        const int period = 7;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 111);
        var bars = new List<TBar>();
        for (int i = 0; i < 50; i++) { bars.Add(gbm.Next(isNew: true)); }

        var d = new Dstoch(period);
        foreach (var bar in bars) { d.Update(bar); }
        double firstRun = d.Last.Value;

        d.Reset();
        foreach (var bar in bars) { d.Update(bar); }
        Assert.Equal(firstRun, d.Last.Value, 12);
    }

    // ── Primed indicator matches manual feed ──

    [Fact]
    public void PrimedIndicator_MatchesManualFeed()
    {
        const int period = 10;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 222);
        for (int i = 0; i < 60; i++) { source.Add(gbm.Next(isNew: true)); }

        var manual = new Dstoch(period);
        for (int i = 0; i < source.Count; i++) { manual.Update(source[i]); }

        var primed = new Dstoch(period);
        primed.Prime(source);

        Assert.Equal(manual.Last.Value, primed.Last.Value, 12);
    }

    // ── Calculate factory consistency ──

    [Fact]
    public void Calculate_MatchesBatch()
    {
        const int period = 10;
        var source = new TBarSeries();
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 333);
        for (int i = 0; i < 50; i++) { source.Add(gbm.Next(isNew: true)); }

        var batch = Dstoch.Batch(source, period);
        var (calcResult, _) = Dstoch.Calculate(source, period);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batch[i].Value, calcResult[i].Value, 12);
        }
    }

    // ── NaN propagation safety ──

    [Fact]
    public void BatchNaN_NoPropagation()
    {
        var d = new Dstoch(5);
        for (int i = 0; i < 10; i++)
        {
            d.Update(new TBar(DateTime.UtcNow.AddDays(i), 100 + i, 105 + i, 95 + i, 102 + i, 500));
        }

        // Feed a NaN bar
        d.Update(new TBar(DateTime.UtcNow.AddDays(10), double.NaN, double.NaN, double.NaN, double.NaN, 0));
        // Then valid data
        d.Update(new TBar(DateTime.UtcNow.AddDays(11), 112, 117, 107, 114, 500));
        Assert.True(double.IsFinite(d.Last.Value));
    }
}
