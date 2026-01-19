using System;
using System.Collections.Generic;

namespace QuanTAlib.Tests;

public class ZlemaTests
{
    [Fact]
    public void Zlema_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zlema(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zlema(-1));
        Assert.Throws<ArgumentException>(() => new Zlema(0.0));

        var zlema = new Zlema(1);
        Assert.Equal("Zlema(1)", zlema.Name);
    }

    [Fact]
    public void Zlema_BasicCalculation_ReturnsFinite()
    {
        var zlema = new Zlema(12);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int iterations = zlema.WarmupPeriod + 2;

        TValue result = default;
        for (int i = 0; i < iterations; i++)
        {
            var bar = gbm.Next(isNew: true);
            result = zlema.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(result.Value));
        Assert.True(zlema.IsHot);
    }

    [Fact]
    public void Zlema_IsNewFalse_RestoresState()
    {
        var zlema = new Zlema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);

        TValue lastInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            zlema.Update(lastInput, isNew: true);
        }

        double original = zlema.Last.Value;
        var corrected = new TValue(lastInput.Time, lastInput.Value * 1.1);

        zlema.Update(corrected, isNew: false);
        zlema.Update(lastInput, isNew: false);

        Assert.Equal(original, zlema.Last.Value, precision: 10);
    }

    [Fact]
    public void Zlema_Reset_ClearsState()
    {
        var zlema = new Zlema(10);
        zlema.Update(new TValue(DateTime.UtcNow, 100.0));

        zlema.Reset();

        Assert.Equal(default, zlema.Last);
        Assert.False(zlema.IsHot);
    }

    [Fact]
    public void Zlema_Robustness_NaNAndInfinity_UsesLastValid()
    {
        var zlema = new Zlema(10);
        zlema.Update(new TValue(DateTime.UtcNow, 100.0));
        zlema.Update(new TValue(DateTime.UtcNow, 110.0));

        TValue nanResult = zlema.Update(new TValue(DateTime.UtcNow, double.NaN));
        TValue posInfResult = zlema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        TValue negInfResult = zlema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(nanResult.Value));
        Assert.True(double.IsFinite(posInfResult.Value));
        Assert.True(double.IsFinite(negInfResult.Value));
    }

    [Fact]
    public void Zlema_BatchMatchesStreaming()
    {
        int period = 12;
        TSeries series = BuildSeries(120, seed: 11);

        TSeries batch = Zlema.Calculate(series, period);
        var zlema = new Zlema(period);

        var streamValues = new List<double>(series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            streamValues.Add(zlema.Update(series[i]).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batch[i].Value, streamValues[i], precision: 10);
        }
    }

    [Fact]
    public void Zlema_SpanMatchesBatch()
    {
        int period = 16;
        TSeries series = BuildSeries(200, seed: 21);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];

        Zlema.Calculate(values, output, period);
        TSeries batch = Zlema.Calculate(series, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batch[i].Value, output[i], precision: 10);
        }
    }

    [Fact]
    public void Zlema_EventingMatchesStreaming()
    {
        int period = 8;
        var source = new TSeries();
        var zlema = new Zlema(source, period);

        var eventValues = new List<double>();
        zlema.Pub += (object? sender, in TValueEventArgs args) => eventValues.Add(args.Value.Value);

        TSeries series = BuildSeries(60, seed: 32);
        for (int i = 0; i < series.Count; i++)
        {
            source.Add(series[i]);
        }

        var stream = new Zlema(period);
        for (int i = 0; i < series.Count; i++)
        {
            double expected = stream.Update(series[i]).Value;
            Assert.Equal(expected, eventValues[i], precision: 10);
        }
    }

    [Fact]
    public void Zlema_SpanValidatesOutputLength()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Zlema.Calculate(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Zlema_WarmupPeriod_TransitionsIsHot()
    {
        var zlema = new Zlema(20);
        int warmup = zlema.WarmupPeriod;

        for (int i = 0; i < warmup - 1; i++)
        {
            zlema.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.False(zlema.IsHot);
        }

        zlema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(zlema.IsHot);
    }

    [Fact]
    public void Zlema_Prime_PopulatesState()
    {
        var zlema = new Zlema(10);
        TSeries series = BuildSeries(50, seed: 100);
        double[] values = series.Values.ToArray();

        zlema.Prime(values);

        Assert.True(double.IsFinite(zlema.Last.Value));
        Assert.True(zlema.IsHot);
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
