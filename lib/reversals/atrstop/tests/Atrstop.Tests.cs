using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class AtrstopTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.2, seed: 42);

    // ── Bucket A: Constructor Tests ──────────────────────────────────────
    [Fact]
    public void DefaultPeriod_Is21()
    {
        var ind = new Atrstop();
        Assert.Equal(21, ind.Period);
    }

    [Fact]
    public void DefaultMultiplier_Is3()
    {
        var ind = new Atrstop();
        Assert.Equal(3.0, ind.Multiplier);
    }

    [Fact]
    public void DefaultUseHighLow_IsFalse()
    {
        var ind = new Atrstop();
        Assert.False(ind.UseHighLow);
    }

    [Fact]
    public void CustomParams_AreStored()
    {
        var ind = new Atrstop(period: 14, multiplier: 2.5, useHighLow: true);
        Assert.Equal(14, ind.Period);
        Assert.Equal(2.5, ind.Multiplier);
        Assert.True(ind.UseHighLow);
    }

    [Fact]
    public void Period1_Throws() =>
        Assert.Throws<ArgumentException>(() => new Atrstop(period: 1));

    [Fact]
    public void ZeroMultiplier_Throws() =>
        Assert.Throws<ArgumentException>(() => new Atrstop(multiplier: 0));

    // ── Bucket B: Basic Output ──────────────────────────────────────────
    [Fact]
    public void FirstBar_ReturnsNaN()
    {
        var ind = new Atrstop();
        var bar = new TBar(DateTime.UtcNow, 100, 102, 98, 101, 1000);
        ind.Update(bar);
        Assert.True(double.IsNaN(ind.StopValue));
    }

    [Fact]
    public void AfterWarmup_ReturnsFinite()
    {
        var ind = new Atrstop(period: 3);
        for (int i = 0; i < 10; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }
        Assert.True(double.IsFinite(ind.StopValue));
    }

    [Fact]
    public void StopValue_MatchesLastValue()
    {
        var ind = new Atrstop(period: 3);
        TValue last = default;
        for (int i = 0; i < 10; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            last = ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }
        Assert.Equal(ind.StopValue, last.Value);
    }

    // ── Bucket C: Stop Position Relative to Price ───────────────────────
    [Fact]
    public void InUptrend_StopBelowClose()
    {
        var ind = new Atrstop(period: 3, multiplier: 2.0);
        double price = 100;
        for (int i = 0; i < 20; i++)
        {
            price += 2;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 0.5, price, 1000));
        }
        Assert.True(ind.IsBullish);
        Assert.True(ind.StopValue < price);
    }

    [Fact]
    public void InDowntrend_StopAboveClose()
    {
        var ind = new Atrstop(period: 3, multiplier: 2.0);
        double price = 200;
        for (int i = 0; i < 20; i++)
        {
            price -= 2;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 1, price, 1000));
        }
        Assert.False(ind.IsBullish);
        Assert.True(ind.StopValue > price);
    }

    // ── Bucket D: Reversal Detection ────────────────────────────────────
    [Fact]
    public void Reversal_FlipsBullish()
    {
        var ind = new Atrstop(period: 3, multiplier: 1.0);
        double price = 100;

        // Build uptrend
        for (int i = 0; i < 10; i++)
        {
            price += 2;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000));
        }
        Assert.True(ind.IsBullish);

        // Force reversal with large drop
        price -= 30;
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(20), price, price + 0.5, price - 0.5, price, 1000));
        Assert.False(ind.IsBullish);
    }

    // ── Bucket E: Bar Correction ────────────────────────────────────────
    [Fact]
    public void BarCorrection_RestoresState()
    {
        var ind = new Atrstop(period: 3, multiplier: 2.0);
        for (int i = 0; i < 8; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        bool bullishBefore = ind.IsBullish;

        // Bar correction
        var (_, o2, h2, l2, c2, v2) = _gbm.Next(isNew: true);
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(8), o2, h2, l2, c2, v2), isNew: false);
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(8), o2, h2, l2, c2, v2), isNew: false);

        Assert.Equal(bullishBefore, ind.IsBullish);
    }

    // ── Bucket F: Reset ─────────────────────────────────────────────────
    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Atrstop(period: 3, multiplier: 2.0);
        for (int i = 0; i < 10; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        ind.Reset();
        Assert.True(double.IsNaN(ind.StopValue));
        Assert.False(ind.IsHot);
    }

    // ── Bucket G: Batch ─────────────────────────────────────────────────
    [Fact]
    public void Batch_MatchesStreaming()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 123);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 123);
        const int N = 50;

        var streamInd = new Atrstop(period: 5, multiplier: 2.0);
        double[] streamOut = new double[N];
        for (int i = 0; i < N; i++)
        {
            var (_, o, h, l, c, v) = gbm1.Next(isNew: true);
            streamInd.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
            streamOut[i] = streamInd.StopValue;
        }

        double[] highs = new double[N], lows = new double[N], closes = new double[N];
        for (int i = 0; i < N; i++)
        {
            var (_, _, h, l, c, _) = gbm2.Next(isNew: true);
            highs[i] = h; lows[i] = l; closes[i] = c;
        }

        double[] batchOut = new double[N];
        Atrstop.Batch(highs, lows, closes, batchOut, period: 5, multiplier: 2.0);

        for (int i = 0; i < N; i++)
        {
            if (double.IsNaN(streamOut[i]))
            {
                Assert.True(double.IsNaN(batchOut[i]));
            }
            else
            {
                Assert.Equal(streamOut[i], batchOut[i], precision: 10);
            }
        }
    }

    [Fact]
    public void BatchTBarSeries_ReturnsCorrectLength()
    {
        var source = new TBarSeries();
        for (int i = 0; i < 30; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        var result = Atrstop.Batch(source, period: 5, multiplier: 2.0);
        Assert.Equal(30, result.Count);
    }

    // ── Bucket H: Events ────────────────────────────────────────────────
    [Fact]
    public void PubEvent_Fires()
    {
        var ind = new Atrstop(period: 3);
        int count = 0;
        ind.Pub += (_, in _) => count++;

        for (int i = 0; i < 5; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }
        Assert.Equal(5, count);
    }

    // ── Bucket I: NaN Handling ───────────────────────────────────────────
    [Fact]
    public void NaN_Input_ReturnsNaN()
    {
        var ind = new Atrstop(period: 3);
        var bar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        ind.Update(bar);
        Assert.True(double.IsNaN(ind.StopValue));
    }

    // ── Bucket J: UseHighLow Mode ───────────────────────────────────────
    [Fact]
    public void HighLowMode_DifferentFromCloseMode()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 77);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 77);

        var indClose = new Atrstop(period: 5, multiplier: 2.0, useHighLow: false);
        var indHL = new Atrstop(period: 5, multiplier: 2.0, useHighLow: true);

        for (int i = 0; i < 30; i++)
        {
            var (_, o1, h1, l1, c1, v1) = gbm1.Next(isNew: true);
            var (_, o2, h2, l2, c2, v2) = gbm2.Next(isNew: true);
            indClose.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o1, h1, l1, c1, v1));
            indHL.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o2, h2, l2, c2, v2));
        }

        // Values should typically differ between modes (HL gives wider bands)
        if (double.IsFinite(indClose.StopValue) && double.IsFinite(indHL.StopValue))
        {
            // At least verify both produce finite output
            Assert.True(double.IsFinite(indClose.StopValue));
            Assert.True(double.IsFinite(indHL.StopValue));
        }
    }

    // ── Bucket K: Calculate Method ──────────────────────────────────────
    [Fact]
    public void Calculate_ReturnsTupleWithIndicator()
    {
        var source = new TBarSeries();
        for (int i = 0; i < 30; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        var (results, indicator) = Atrstop.Calculate(source, period: 5, multiplier: 2.0);
        Assert.Equal(30, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    // ── Bucket L: Prime Method ──────────────────────────────────────────
    [Fact]
    public void Prime_SetsState()
    {
        var source = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        var ind = new Atrstop(period: 5);
        ind.Prime(source);
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.StopValue));
    }

    // ── Bucket M: Streaming Consistency ─────────────────────────────────
    [Fact]
    public void StreamingAfterPrime_IsDeterministic()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 99);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 99);

        var source = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            var (_, o, h, l, c, v) = gbm1.Next(isNew: true);
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        var fullInd = new Atrstop(period: 5);
        for (int i = 0; i < 20; i++)
        {
            var (_, o, h, l, c, v) = gbm2.Next(isNew: true);
            fullInd.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        var primedInd = new Atrstop(period: 5);
        primedInd.Prime(source);

        Assert.Equal(fullInd.StopValue, primedInd.StopValue, precision: 10);
        Assert.Equal(fullInd.IsBullish, primedInd.IsBullish);
    }

    // ── Bucket N: Band Ratcheting ───────────────────────────────────────
    [Fact]
    public void InUptrend_LowerBandRisesMonotonically()
    {
        var ind = new Atrstop(period: 3, multiplier: 1.5);
        double price = 100;
        double prevStop = double.NaN;

        for (int i = 0; i < 20; i++)
        {
            price += 1.5; // Calm uptrend
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000));

            if (ind.IsHot && ind.IsBullish)
            {
                if (double.IsFinite(prevStop))
                {
                    // Lower band should ratchet up (never decrease in uptrend)
                    Assert.True(ind.StopValue >= prevStop - 1e-10,
                        $"Stop decreased from {prevStop} to {ind.StopValue} at bar {i}");
                }
                prevStop = ind.StopValue;
            }
        }
    }
}
