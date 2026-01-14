using System;
using System.Collections.Generic;

namespace QuanTAlib.Tests;

public class RgmaTests
{
    [Fact]
    public void Rgma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rgma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rgma(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rgma(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rgma(10, -1));

        var rgma = new Rgma(10, 3);
        Assert.Equal("Rgma(10,3)", rgma.Name);
    }

    [Fact]
    public void Rgma_BasicCalculation_ReturnsFinite()
    {
        var rgma = new Rgma(10, passes: 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int iterations = rgma.WarmupPeriod + 2;

        TValue result = default;
        for (int i = 0; i < iterations; i++)
        {
            var bar = gbm.Next(isNew: true);
            result = rgma.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(result.Value));
        Assert.True(rgma.IsHot);
    }

    [Fact]
    public void Rgma_IsNewFalse_RestoresState()
    {
        var rgma = new Rgma(10, passes: 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);

        TValue lastInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            rgma.Update(lastInput, isNew: true);
        }

        double original = rgma.Last.Value;
        var corrected = new TValue(lastInput.Time, lastInput.Value * 1.1);

        rgma.Update(corrected, isNew: false);
        rgma.Update(lastInput, isNew: false);

        Assert.Equal(original, rgma.Last.Value, precision: 10);
    }

    [Fact]
    public void Rgma_Reset_ClearsState()
    {
        var rgma = new Rgma(10, passes: 3);
        rgma.Update(new TValue(DateTime.UtcNow, 100.0));

        rgma.Reset();

        Assert.Equal(default, rgma.Last);
        Assert.False(rgma.IsHot);
    }

    [Fact]
    public void Rgma_Robustness_NaNAndInfinity_UsesLastValid()
    {
        var rgma = new Rgma(10, passes: 3);
        rgma.Update(new TValue(DateTime.UtcNow, 100.0));
        rgma.Update(new TValue(DateTime.UtcNow, 110.0));

        TValue nanResult = rgma.Update(new TValue(DateTime.UtcNow, double.NaN));
        TValue posInfResult = rgma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        TValue negInfResult = rgma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(nanResult.Value));
        Assert.True(double.IsFinite(posInfResult.Value));
        Assert.True(double.IsFinite(negInfResult.Value));
    }

    [Fact]
    public void Rgma_BatchMatchesStreaming()
    {
        int period = 12;
        int passes = 4;
        TSeries series = BuildSeries(250, seed: 11);

        TSeries batch = Rgma.Batch(series, period, passes);
        var rgma = new Rgma(period, passes);

        var streamValues = new List<double>(series.Count);
        for (int i = 0; i < series.Count; i++)
            streamValues.Add(rgma.Update(series[i]).Value);

        for (int i = 0; i < series.Count; i++)
            Assert.Equal(batch[i].Value, streamValues[i], precision: 10);
    }

    [Fact]
    public void Rgma_SpanMatchesBatch()
    {
        int period = 16;
        int passes = 5;
        TSeries series = BuildSeries(200, seed: 21);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];

        Rgma.Batch(values.AsSpan(), output.AsSpan(), period, passes);
        TSeries batch = Rgma.Batch(series, period, passes);

        for (int i = 0; i < values.Length; i++)
            Assert.Equal(batch[i].Value, output[i], precision: 10);
    }

    private static TSeries BuildSeries(int count, int seed)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: seed);
        var t = new List<long>(count);
        var v = new List<double>(count);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            t.Add(bar.Time);
            v.Add(bar.Close);
        }

        return new TSeries(t, v);
    }
}

