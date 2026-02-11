using System;
using System.Collections.Generic;

namespace QuanTAlib.Tests;

public class ZldemaTests
{
    [Fact]
    public void Zldema_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zldema(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zldema(-1));
        Assert.Throws<ArgumentException>(() => new Zldema(0.0));

        var zldema = new Zldema(1);
        Assert.Equal("Zldema(1)", zldema.Name);
    }

    [Fact]
    public void Zldema_BasicCalculation_ReturnsFinite()
    {
        var zldema = new Zldema(12);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int iterations = zldema.WarmupPeriod + 2;

        TValue result = default;
        for (int i = 0; i < iterations; i++)
        {
            var bar = gbm.Next(isNew: true);
            result = zldema.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(result.Value));
        Assert.True(zldema.IsHot);
    }

    [Fact]
    public void Zldema_IsNewFalse_RestoresState()
    {
        var zldema = new Zldema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);

        TValue lastInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            zldema.Update(lastInput, isNew: true);
        }

        double original = zldema.Last.Value;
        var corrected = new TValue(lastInput.Time, lastInput.Value * 1.1);

        zldema.Update(corrected, isNew: false);
        zldema.Update(lastInput, isNew: false);

        Assert.Equal(original, zldema.Last.Value, precision: 10);
    }

    [Fact]
    public void Zldema_Reset_ClearsState()
    {
        var zldema = new Zldema(10);
        zldema.Update(new TValue(DateTime.UtcNow, 100.0));

        zldema.Reset();

        Assert.Equal(default, zldema.Last);
        Assert.False(zldema.IsHot);
    }

    [Fact]
    public void Zldema_Robustness_NaNAndInfinity_UsesLastValid()
    {
        var zldema = new Zldema(10);
        zldema.Update(new TValue(DateTime.UtcNow, 100.0));
        zldema.Update(new TValue(DateTime.UtcNow, 110.0));

        TValue nanResult = zldema.Update(new TValue(DateTime.UtcNow, double.NaN));
        TValue posInfResult = zldema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        TValue negInfResult = zldema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(nanResult.Value));
        Assert.True(double.IsFinite(posInfResult.Value));
        Assert.True(double.IsFinite(negInfResult.Value));
    }

    [Fact]
    public void Zldema_BatchMatchesStreaming()
    {
        int period = 12;
        TSeries series = BuildSeries(120, seed: 11);

        TSeries batch = Zldema.Batch(series, period);
        var zldema = new Zldema(period);

        var streamValues = new List<double>(series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            streamValues.Add(zldema.Update(series[i]).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batch[i].Value, streamValues[i], precision: 10);
        }
    }

    [Fact]
    public void Zldema_SpanMatchesBatch()
    {
        int period = 16;
        TSeries series = BuildSeries(200, seed: 21);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];

        Zldema.Batch(values, output, period);
        TSeries batch = Zldema.Batch(series, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batch[i].Value, output[i], precision: 10);
        }
    }

    [Fact]
    public void Zldema_EventingMatchesStreaming()
    {
        int period = 8;
        var source = new TSeries();
        var zldema = new Zldema(source, period);

        var eventValues = new List<double>();
        zldema.Pub += (object? sender, in TValueEventArgs args) => eventValues.Add(args.Value.Value);

        TSeries series = BuildSeries(60, seed: 32);
        for (int i = 0; i < series.Count; i++)
        {
            source.Add(series[i]);
        }

        var stream = new Zldema(period);
        for (int i = 0; i < series.Count; i++)
        {
            double expected = stream.Update(series[i]).Value;
            Assert.Equal(expected, eventValues[i], precision: 10);
        }
    }

    [Fact]
    public void Zldema_SpanValidatesOutputLength()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Zldema.Batch(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Zldema_WarmupPeriod_TransitionsIsHot()
    {
        var zldema = new Zldema(20);
        int warmup = zldema.WarmupPeriod;

        for (int i = 0; i < warmup - 1; i++)
        {
            zldema.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.False(zldema.IsHot);
        }

        zldema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(zldema.IsHot);
    }

    [Fact]
    public void Zldema_Prime_PopulatesState()
    {
        var zldema = new Zldema(10);
        TSeries series = BuildSeries(50, seed: 100);
        double[] values = series.Values.ToArray();

        zldema.Prime(values);

        Assert.True(double.IsFinite(zldema.Last.Value));
        Assert.True(zldema.IsHot);
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