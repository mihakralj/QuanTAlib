using System;
using System.Collections.Generic;

namespace QuanTAlib.Tests;

public class MmaTests
{
    [Fact]
    public void Mma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mma(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mma(-2));

        var mma = new Mma(2);
        Assert.Equal("Mma(2)", mma.Name);
    }

    [Fact]
    public void Mma_BasicCalculation_ReturnsFinite()
    {
        var mma = new Mma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int iterations = mma.WarmupPeriod + 2;

        TValue result = default;
        for (int i = 0; i < iterations; i++)
        {
            var bar = gbm.Next(isNew: true);
            result = mma.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(result.Value));
        Assert.True(mma.IsHot);
    }

    [Fact]
    public void Mma_IsNewFalse_RestoresState()
    {
        var mma = new Mma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);

        TValue lastInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            mma.Update(lastInput, isNew: true);
        }

        double original = mma.Last.Value;
        var corrected = new TValue(lastInput.Time, lastInput.Value * 1.1);

        mma.Update(corrected, isNew: false);
        mma.Update(lastInput, isNew: false);

        Assert.Equal(original, mma.Last.Value, precision: 10);
    }

    [Fact]
    public void Mma_Reset_ClearsState()
    {
        var mma = new Mma(10);
        mma.Update(new TValue(DateTime.UtcNow, 100.0));

        mma.Reset();

        Assert.Equal(default, mma.Last);
        Assert.False(mma.IsHot);
    }

    [Fact]
    public void Mma_Robustness_NaNAndInfinity_UsesLastValid()
    {
        var mma = new Mma(10);
        mma.Update(new TValue(DateTime.UtcNow, 100.0));
        mma.Update(new TValue(DateTime.UtcNow, 110.0));

        TValue nanResult = mma.Update(new TValue(DateTime.UtcNow, double.NaN));
        TValue posInfResult = mma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        TValue negInfResult = mma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(nanResult.Value));
        Assert.True(double.IsFinite(posInfResult.Value));
        Assert.True(double.IsFinite(negInfResult.Value));
    }

    [Fact]
    public void Mma_BatchMatchesStreaming()
    {
        int period = 12;
        TSeries series = BuildSeries(120, seed: 11);

        TSeries batch = Mma.Batch(series, period);
        var mma = new Mma(period);

        var streamValues = new List<double>(series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            streamValues.Add(mma.Update(series[i]).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batch[i].Value, streamValues[i], precision: 10);
        }
    }

    [Fact]
    public void Mma_SpanMatchesBatch()
    {
        int period = 16;
        TSeries series = BuildSeries(200, seed: 21);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];

        Mma.Batch(values, output, period);
        TSeries batch = Mma.Batch(series, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batch[i].Value, output[i], precision: 10);
        }
    }

    [Fact]
    public void Mma_EventingMatchesStreaming()
    {
        int period = 8;
        var source = new TSeries();
        var mma = new Mma(source, period);

        var eventValues = new List<double>();
        mma.Pub += (object? sender, in TValueEventArgs args) => eventValues.Add(args.Value.Value);

        TSeries series = BuildSeries(60, seed: 32);
        for (int i = 0; i < series.Count; i++)
        {
            source.Add(series[i]);
        }

        var stream = new Mma(period);
        for (int i = 0; i < series.Count; i++)
        {
            double expected = stream.Update(series[i]).Value;
            Assert.Equal(expected, eventValues[i], precision: 10);
        }
    }

    [Fact]
    public void Mma_SpanValidatesOutputLength()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Mma.Batch(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Mma_WarmupPeriod_TransitionsIsHot()
    {
        var mma = new Mma(20);
        int warmup = mma.WarmupPeriod;

        for (int i = 0; i < warmup - 1; i++)
        {
            mma.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.False(mma.IsHot);
        }

        mma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(mma.IsHot);
    }

    [Fact]
    public void Mma_Prime_PopulatesState()
    {
        var mma = new Mma(10);
        TSeries series = BuildSeries(50, seed: 100);
        double[] values = series.Values.ToArray();

        mma.Prime(values);

        Assert.True(double.IsFinite(mma.Last.Value));
        Assert.True(mma.IsHot);
    }

    private static TSeries BuildSeries(int count, int seed)
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: seed);

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        return series;
    }
}
