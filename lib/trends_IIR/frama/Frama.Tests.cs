using System;
using System.Collections.Generic;

namespace QuanTAlib.Tests;

public class FramaTests
{
    [Fact]
    public void Frama_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Frama(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Frama(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Frama(-5));
    }

    [Fact]
    public void Frama_BasicCalculation_ReturnsFinite()
    {
        var frama = new Frama(16);
        var series = BuildSeries(40, seed: 42);

        TValue result = default;
        for (int i = 0; i < series.Count; i++)
        {
            result = frama.Update(series[i], isNew: true);
        }

        Assert.True(double.IsFinite(result.Value));
        Assert.True(frama.IsHot);
    }

    [Fact]
    public void Frama_IsNewFalse_RestoresState()
    {
        var frama = new Frama(16);
        var series = BuildSeries(20, seed: 7);

        TBar lastBar = default;
        for (int i = 0; i < 10; i++)
        {
            lastBar = series[i];
            frama.Update(lastBar, isNew: true);
        }

        double original = frama.Last.Value;

        var corrected = new TBar(lastBar.Time, lastBar.Open, lastBar.High * 1.05, lastBar.Low * 0.95, lastBar.Close, lastBar.Volume);
        frama.Update(corrected, isNew: false);
        frama.Update(lastBar, isNew: false);

        Assert.Equal(original, frama.Last.Value, precision: 10);
    }

    [Fact]
    public void Frama_NaNFirstBar_RecoversOnValidInput()
    {
        var frama = new Frama(10);
        int warmup = frama.WarmupPeriod;
        var nanBar = new TBar(DateTime.UtcNow.Ticks, 1, double.NaN, 1, 1, 0);

        TValue first = frama.Update(nanBar, isNew: true);
        Assert.True(double.IsNaN(first.Value));

        DateTime start = DateTime.UtcNow.AddMinutes(1);
        TValue next = default;
        for (int i = 0; i < warmup; i++)
        {
            var valid = new TBar(start.AddMinutes(i).Ticks, 100, 110, 90, 105, 1000);
            next = frama.Update(valid, isNew: true);
        }

        Assert.True(double.IsFinite(next.Value));
        Assert.True(frama.IsHot);
    }

    [Fact]
    public void Frama_BatchMatchesStreaming()
    {
        int period = 20;
        var series = BuildSeries(80, seed: 11);

        TSeries batch = FramaBatch(series, period);
        var frama = new Frama(period);

        var streamValues = new List<double>(series.Count);
        for (int i = 0; i < series.Count; i++)
        {
            streamValues.Add(frama.Update(series[i]).Value);
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batch[i].Value, streamValues[i], precision: 10);
        }
    }

    [Fact]
    public void Frama_SpanMatchesBatch()
    {
        int period = 18;
        var series = BuildSeries(60, seed: 21);
        double[] output = new double[series.Count];

        Frama.Calculate(series.High.Values, series.Low.Values, period, output);
        TSeries batch = FramaBatch(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batch[i].Value, output[i], precision: 10);
        }
    }

    [Fact]
    public void Frama_Eventing_WorksWithTSeries()
    {
        int period = 12;
        var source = new TSeries();
        var frama = new Frama(source, period);

        int count = 0;
        frama.Pub += (object? sender, in TValueEventArgs args) => count++;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 31);
        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(bar.Time, bar.Close);
        }

        Assert.Equal(25, count);
    }

    [Fact]
    public void Frama_WarmupPeriod_TransitionsIsHot()
    {
        var frama = new Frama(15);
        int warmup = frama.WarmupPeriod;
        var series = BuildSeries(warmup, seed: 100);

        for (int i = 0; i < warmup - 1; i++)
        {
            frama.Update(series[i], isNew: true);
            Assert.False(frama.IsHot);
        }

        frama.Update(series[warmup - 1], isNew: true);
        Assert.True(frama.IsHot);
    }

    private static TBarSeries BuildSeries(int count, int seed)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    private static TSeries FramaBatch(TBarSeries series, int period)
    {
        return Frama.Batch(series, period);
    }
}
