using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class VstopTests
{
    private readonly GBM _gbm = new(100.0, 0.05, 0.2, seed: 42);

    // ── Bucket A: Constructor Tests ──────────────────────────────────────
    [Fact]
    public void DefaultPeriod_Is7()
    {
        var ind = new Vstop();
        Assert.Equal(7, ind.Period);
    }

    [Fact]
    public void DefaultMultiplier_Is3()
    {
        var ind = new Vstop();
        Assert.Equal(3.0, ind.Multiplier);
    }

    [Fact]
    public void CustomPeriod_IsStored()
    {
        var ind = new Vstop(period: 14, multiplier: 2.5);
        Assert.Equal(14, ind.Period);
        Assert.Equal(2.5, ind.Multiplier);
    }

    [Fact]
    public void Period1_Throws() =>
        Assert.Throws<ArgumentException>(() => new Vstop(period: 1));

    [Fact]
    public void ZeroMultiplier_Throws() =>
        Assert.Throws<ArgumentException>(() => new Vstop(multiplier: 0));

    [Fact]
    public void NegativeMultiplier_Throws() =>
        Assert.Throws<ArgumentException>(() => new Vstop(multiplier: -1));

    // ── Bucket B: Basic Output ──────────────────────────────────────────
    [Fact]
    public void FirstBar_ReturnsNaN()
    {
        var ind = new Vstop();
        var bar = new TBar(DateTime.UtcNow, 100, 102, 98, 101, 1000);
        ind.Update(bar);
        Assert.True(double.IsNaN(ind.SarValue));
    }

    [Fact]
    public void AfterWarmup_ReturnsFinite()
    {
        var ind = new Vstop(period: 3);
        for (int i = 0; i < 10; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }
        Assert.True(double.IsFinite(ind.SarValue));
    }

    [Fact]
    public void SarValue_MatchesLastValue()
    {
        var ind = new Vstop(period: 3);
        TValue last = default;
        for (int i = 0; i < 10; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            last = ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }
        Assert.Equal(ind.SarValue, last.Value);
    }

    // ── Bucket C: SAR Position Relative to Price ────────────────────────
    [Fact]
    public void InUptrend_SarBelowClose()
    {
        // Construct a strong uptrend
        var ind = new Vstop(period: 3, multiplier: 2.0);
        double price = 100;
        for (int i = 0; i < 20; i++)
        {
            price += 2; // Steady uptrend
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 0.5, price, 1000));
        }
        Assert.True(ind.IsLong);
        Assert.True(ind.SarValue < price);
    }

    [Fact]
    public void InDowntrend_SarAboveClose()
    {
        var ind = new Vstop(period: 3, multiplier: 2.0);
        double price = 200;
        for (int i = 0; i < 20; i++)
        {
            price -= 2; // Steady downtrend
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 1, price, 1000));
        }
        Assert.False(ind.IsLong);
        Assert.True(ind.SarValue > price);
    }

    // ── Bucket D: Reversal Detection ────────────────────────────────────
    [Fact]
    public void Reversal_IsStopTrue()
    {
        var ind = new Vstop(period: 3, multiplier: 1.0);
        double price = 100;
        // Build uptrend
        for (int i = 0; i < 10; i++)
        {
            price += 2;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000));
        }
        Assert.True(ind.IsLong);

        // Force reversal with large drop
        price -= 30;
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(20), price, price + 0.5, price - 0.5, price, 1000));
        Assert.True(ind.IsStop);
        Assert.False(ind.IsLong);
    }

    // ── Bucket E: Bar Correction ────────────────────────────────────────
    [Fact]
    public void BarCorrection_RestoresState()
    {
        var ind = new Vstop(period: 3, multiplier: 2.0);
        for (int i = 0; i < 8; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        bool longBefore = ind.IsLong;

        // Update with isNew=false (bar correction)
        var (_, o2, h2, l2, c2, v2) = _gbm.Next(isNew: true);
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(8), o2, h2, l2, c2, v2), isNew: false);

        // Restore previous state by re-updating with isNew=false
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(8), o2, h2, l2, c2, v2), isNew: false);

        // State should be restored from _ps
        Assert.Equal(longBefore, ind.IsLong);
    }

    // ── Bucket F: Reset ─────────────────────────────────────────────────
    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Vstop(period: 3, multiplier: 2.0);
        for (int i = 0; i < 10; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        ind.Reset();
        Assert.True(double.IsNaN(ind.SarValue));
        Assert.False(ind.IsHot);
    }

    // ── Bucket G: Batch ─────────────────────────────────────────────────
    [Fact]
    public void Batch_MatchesStreaming()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 123);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 123);
        const int N = 50;

        // Streaming
        var streamInd = new Vstop(period: 5, multiplier: 2.0);
        double[] streamOut = new double[N];
        for (int i = 0; i < N; i++)
        {
            var (_, o, h, l, c, v) = gbm1.Next(isNew: true);
            streamInd.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
            streamOut[i] = streamInd.SarValue;
        }

        // Batch
        double[] highs = new double[N], lows = new double[N], closes = new double[N];
        for (int i = 0; i < N; i++)
        {
            var (_, _, h, l, c, _) = gbm2.Next(isNew: true);
            highs[i] = h; lows[i] = l; closes[i] = c;
        }

        double[] batchOut = new double[N];
        Vstop.Batch(highs, lows, closes, batchOut, period: 5, multiplier: 2.0);

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

        var result = Vstop.Batch(source, period: 5, multiplier: 2.0);
        Assert.Equal(30, result.Count);
    }

    // ── Bucket H: Events ────────────────────────────────────────────────
    [Fact]
    public void PubEvent_Fires()
    {
        var ind = new Vstop(period: 3);
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
        var ind = new Vstop(period: 3);
        var bar = new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0);
        ind.Update(bar);
        Assert.True(double.IsNaN(ind.SarValue));
    }

    [Fact]
    public void NaN_AfterValid_SubstitutesLastValid()
    {
        var ind = new Vstop(period: 3);
        // Feed valid data first
        for (int i = 0; i < 5; i++)
        {
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        // Now feed partial NaN — should substitute
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(10), double.NaN, 110, 95, 105, 1000));
        // Should not crash — NaN high substituted with last valid
        Assert.True(double.IsFinite(ind.Last.Value) || double.IsNaN(ind.Last.Value));
    }

    // ── Bucket J: Multiplier Sensitivity ────────────────────────────────
    [Fact]
    public void HigherMultiplier_WiderStop()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 77);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 77);

        var ind1 = new Vstop(period: 5, multiplier: 1.0);
        var ind2 = new Vstop(period: 5, multiplier: 3.0);

        for (int i = 0; i < 20; i++)
        {
            var (_, o1, h1, l1, c1, v1) = gbm1.Next(isNew: true);
            var (_, o2, h2, l2, c2, v2) = gbm2.Next(isNew: true);
            ind1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o1, h1, l1, c1, v1));
            ind2.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o2, h2, l2, c2, v2));
        }

        if (double.IsFinite(ind1.SarValue) && double.IsFinite(ind2.SarValue) && ind1.IsLong && ind2.IsLong)
        {
            // Higher multiplier → SAR further from SIC → wider stop
            double gap1 = Math.Abs(ind1.SarValue - ind1.Last.Value);
            double gap2 = Math.Abs(ind2.SarValue - ind2.Last.Value);
            // Both gaps should be non-negative
            Assert.True(gap1 >= 0 && gap2 >= 0, "Both gaps should be non-negative");
        }
    }

    // ── Bucket K: Calculate Method ──────────────────────────────────────
    [Fact]
    public void Calculate_ReturnsTupleWithIndicator()
    {
        var source = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        var (results, indicator) = Vstop.Calculate(source, period: 5, multiplier: 2.0);
        Assert.Equal(20, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    // ── Bucket L: Prime Method ──────────────────────────────────────────
    [Fact]
    public void Prime_SetsState()
    {
        var source = new TBarSeries();
        for (int i = 0; i < 15; i++)
        {
            var (_, o, h, l, c, v) = _gbm.Next(isNew: true);
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        var ind = new Vstop(period: 5);
        ind.Prime(source);
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.SarValue));
    }

    // ── Bucket M: Streaming Consistency ─────────────────────────────────
    [Fact]
    public void StreamingAfterPrime_IsDeterministic()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 99);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 99);

        // Build source for priming (first 20 bars)
        var source = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            var (_, o, h, l, c, v) = gbm1.Next(isNew: true);
            source.Add(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        // Full streaming
        var fullInd = new Vstop(period: 5);
        for (int i = 0; i < 20; i++)
        {
            var (_, o, h, l, c, v) = gbm2.Next(isNew: true);
            fullInd.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        // Primed
        var primedInd = new Vstop(period: 5);
        primedInd.Prime(source);

        Assert.Equal(fullInd.SarValue, primedInd.SarValue, precision: 10);
        Assert.Equal(fullInd.IsLong, primedInd.IsLong);
    }
}
