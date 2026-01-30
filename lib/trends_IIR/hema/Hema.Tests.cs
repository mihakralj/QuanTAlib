using System;
using System.Collections.Generic;

namespace QuanTAlib.Tests;

public class HemaTests
{
    [Fact]
    public void Hema_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hema(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hema(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hema(1));

        var hema = new Hema(2);
        Assert.Equal("Hema(2)", hema.Name);
    }

    [Fact]
    public void Hema_BasicCalculation_ReturnsFinite()
    {
        var hema = new Hema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        int iterations = hema.WarmupPeriod + 2;

        TValue result = default;
        for (int i = 0; i < iterations; i++)
        {
            var bar = gbm.Next(isNew: true);
            result = hema.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(result.Value));
        Assert.True(hema.IsHot);
    }

    [Fact]
    public void Hema_IsNewFalse_RestoresState()
    {
        var hema = new Hema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);

        TValue lastInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            lastInput = new TValue(bar.Time, bar.Close);
            hema.Update(lastInput, isNew: true);
        }

        double original = hema.Last.Value;
        var corrected = new TValue(lastInput.Time, lastInput.Value * 1.1);

        hema.Update(corrected, isNew: false);
        hema.Update(lastInput, isNew: false);

        Assert.Equal(original, hema.Last.Value, precision: 10);
    }

    [Fact]
    public void Hema_Reset_ClearsState()
    {
        var hema = new Hema(10);
        hema.Update(new TValue(DateTime.UtcNow, 100.0));

        hema.Reset();

        Assert.Equal(default, hema.Last);
        Assert.False(hema.IsHot);
    }

    [Fact]
    public void Hema_Robustness_NaNAndInfinity_UsesLastValid()
    {
        var hema = new Hema(10);
        hema.Update(new TValue(DateTime.UtcNow, 100.0));
        hema.Update(new TValue(DateTime.UtcNow, 110.0));

        TValue nanResult = hema.Update(new TValue(DateTime.UtcNow, double.NaN));
        TValue posInfResult = hema.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        TValue negInfResult = hema.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.True(double.IsFinite(nanResult.Value));
        Assert.True(double.IsFinite(posInfResult.Value));
        Assert.True(double.IsFinite(negInfResult.Value));
    }

    [Fact]
    public void Hema_BatchMatchesStreaming()
    {
        int period = 12;
        TSeries series = BuildSeries(120, seed: 11);

        TSeries batch = Hema.Calculate(series, period);
        var hema = new Hema(period);

        var streamValues = new List<double>(series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            streamValues.Add(hema.Update(series[i]).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batch[i].Value, streamValues[i], precision: 10);
        }
    }

    [Fact]
    public void Hema_SpanMatchesBatch()
    {
        int period = 16;
        TSeries series = BuildSeries(200, seed: 21);
        double[] values = series.Values.ToArray();
        var output = new double[values.Length];

        Hema.Calculate(values, output, period);
        TSeries batch = Hema.Calculate(series, period);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(batch[i].Value, output[i], precision: 10);
        }
    }

    [Fact]
    public void Hema_EventingMatchesStreaming()
    {
        int period = 8;
        var source = new TSeries();
        var hema = new Hema(source, period);

        var eventValues = new List<double>();
        hema.Pub += (object? sender, in TValueEventArgs args) => eventValues.Add(args.Value.Value);

        TSeries series = BuildSeries(60, seed: 32);
        for (int i = 0; i < series.Count; i++)
        {
            source.Add(series[i]);
        }

        var stream = new Hema(period);
        for (int i = 0; i < series.Count; i++)
        {
            double expected = stream.Update(series[i]).Value;
            Assert.Equal(expected, eventValues[i], precision: 10);
        }
    }

    [Fact]
    public void Hema_SpanValidatesOutputLength()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Hema.Calculate(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Hema_WarmupPeriod_TransitionsIsHot()
    {
        var hema = new Hema(20);
        int warmup = hema.WarmupPeriod;

        for (int i = 0; i < warmup - 1; i++)
        {
            hema.Update(new TValue(DateTime.UtcNow, 100.0));
            Assert.False(hema.IsHot);
        }

        hema.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(hema.IsHot);
    }

    [Fact]
    public void Hema_Prime_PopulatesState()
    {
        var hema = new Hema(10);
        TSeries series = BuildSeries(50, seed: 100);
        double[] values = series.Values.ToArray();

        hema.Prime(values);

        Assert.True(double.IsFinite(hema.Last.Value));
        Assert.True(hema.IsHot);
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
