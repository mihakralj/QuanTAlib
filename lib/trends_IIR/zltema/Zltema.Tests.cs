using System;
using System.Collections.Generic;

namespace QuanTAlib.Tests;

public class ZltemaTests
{
    [Fact]
    public void Zltema_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zltema(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zltema(-1));
        Assert.Throws<ArgumentException>(() => new Zltema(0.0));

        var zltema = new Zltema(1);
        Assert.Equal("Zltema(1)", zltema.Name);
    }

    [Fact]
    public void Zltema_BasicCalculation_ReturnsFinite()
    {
        var zltema = new Zltema(12);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int iterations = zltema.WarmupPeriod + 2;

        TValue result = default;
        for (int i = 0; i < iterations; i++)
        {
            var bar = gbm.Next(isNew: true);
            result = zltema.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(result.Value));
        Assert.True(zltema.IsHot);
    }

    [Fact]
    public void Zltema_IsNewFalse_RestoresState()
    {
        var zltema = new Zltema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);

        TValue lastInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            zltema.Update(lastInput, isNew: true);
        }

        double original = zltema.Last.Value;
        var corrected = new TValue(lastInput.Time, lastInput.Value * 1.1);

        zltema.Update(corrected, isNew: false);
        zltema.Update(lastInput, isNew: false);

        Assert.Equal(original, zltema.Last.Value, precision: 10);
    }

    [Fact]
    public void Zltema_Reset_ClearsState()
    {
        var zltema = new Zltema(10);
        zltema.Update(new TValue(DateTime.UtcNow, 100.0));

        zltema.Reset();

        Assert.Equal(default, zltema.Last);
        Assert.False(zltema.IsHot);
    }

    [Fact]
    public void Zltema_Robustness_NaNAndInfinity_UsesLastValid()
    {
        var zltema = new Zltema(10);
        zltema.Update(new TValue(DateTime.UtcNow, 100.0));
        zltema.Update(new TValue(DateTime.UtcNow, 110.0));

        TValue nanResult = zltema.Update(new TValue(DateTime.UtcNow, double.NaN));
        TValue posInfResult = zltema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        TValue negInfResult = zltema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(nanResult.Value));
        Assert.True(double.IsFinite(posInfResult.Value));
        Assert.True(double.IsFinite(negInfResult.Value));
    }

    [Fact]
    public void Zltema_BatchMatchesStreaming()
    {
        int period = 12;
        TSeries series = BuildSeries(120, seed: 11);

        TSeries batch = Zltema.Calculate(series, period);
        var zltema = new Zltema(period);

        var streamValues = new List<double>(series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            streamValues.Add(zltema.Update(series[i]).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batch[i].Value, streamValues[i], precision: 10);
        }
    }

    [Fact]
    public void Zltema_SpanMatchesBatch()
    {
        int period = 16;
        TSeries series = BuildSeries(200, seed: 21);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];

        Zltema.Calculate(values, output, period);
        TSeries batch = Zltema.Calculate(series, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batch[i].Value, output[i], precision: 10);
        }
    }

    [Fact]
    public void Zltema_EventingMatchesStreaming()
    {
        int period = 8;
        var source = new TSeries();
        var zltema = new Zltema(source, period);

        var eventValues = new List<double>();
        zltema.Pub += (object? sender, in TValueEventArgs args) => eventValues.Add(args.Value.Value);

        TSeries series = BuildSeries(60, seed: 32);
        for (int i = 0; i < series.Count; i++)
        {
            source.Add(series[i]);
        }

        var stream = new Zltema(period);
        for (int i = 0; i < series.Count; i++)
        {
            double expected = stream.Update(series[i]).Value;
            Assert.Equal(expected, eventValues[i], precision: 10);
        }
    }

    [Fact]
    public void Zltema_SpanValidatesOutputLength()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Zltema.Calculate(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Zltema_WarmupPeriod_TransitionsIsHot()
    {
        var zltema = new Zltema(20);
        int warmup = zltema.WarmupPeriod;

        for (int i = 0; i < warmup - 1; i++)
        {
            zltema.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.False(zltema.IsHot);
        }

        zltema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(zltema.IsHot);
    }

    [Fact]
    public void Zltema_Prime_PopulatesState()
    {
        var zltema = new Zltema(10);
        TSeries series = BuildSeries(50, seed: 100);
        double[] values = series.Values.ToArray();

        zltema.Prime(values);

        Assert.True(double.IsFinite(zltema.Last.Value));
        Assert.True(zltema.IsHot);
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