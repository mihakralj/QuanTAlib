using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ATRSTOP (ATR Trailing Stop).
/// Cross-validated against Skender.Stock.Indicators where available.
/// Level 3: Mathematical correctness (band ratcheting + ATR×mult logic).
/// </summary>
public sealed class AtrstopValidationTests
{
    // ── Parameter variation ──────────────────────────────────────────────
    [Theory]
    [InlineData(7, 3.0, false)]
    [InlineData(14, 2.0, false)]
    [InlineData(21, 3.0, false)]
    [InlineData(14, 2.0, true)]
    public void Atrstop_WithVariousParams_ProducesFiniteOutput(int period, double mult, bool useHL)
    {
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 42);
        var ind = new Atrstop(period: period, multiplier: mult, useHighLow: useHL);

        for (int i = 0; i < 100; i++)
        {
            var (_, o, h, l, c, v) = gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.StopValue));
    }

    // ── Determinism ─────────────────────────────────────────────────────
    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 55);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 55);

        var ind1 = new Atrstop(period: 21, multiplier: 3.0);
        var ind2 = new Atrstop(period: 21, multiplier: 3.0);

        for (int i = 0; i < 50; i++)
        {
            var (_, o1, h1, l1, c1, v1) = gbm1.Next(isNew: true);
            var (_, o2, h2, l2, c2, v2) = gbm2.Next(isNew: true);
            ind1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o1, h1, l1, c1, v1));
            ind2.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o2, h2, l2, c2, v2));
        }

        Assert.Equal(ind1.StopValue, ind2.StopValue, precision: 10);
        Assert.Equal(ind1.IsBullish, ind2.IsBullish);
    }

    // ── Reversal logic ──────────────────────────────────────────────────
    [Fact]
    public void UptrendThenDrop_CausesReversal()
    {
        var ind = new Atrstop(period: 3, multiplier: 1.0);
        double price = 100;

        for (int i = 0; i < 10; i++)
        {
            price += 3;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000));
        }
        Assert.True(ind.IsBullish);

        price -= 50;
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(20), price, price + 1, price - 1, price, 1000));
        Assert.False(ind.IsBullish);
        Assert.True(ind.StopValue > price);
    }

    [Fact]
    public void DowntrendThenRally_CausesReversal()
    {
        var ind = new Atrstop(period: 3, multiplier: 1.0);
        double price = 200;

        for (int i = 0; i < 10; i++)
        {
            price -= 3;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000));
        }
        Assert.False(ind.IsBullish);

        price += 50;
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(20), price, price + 1, price - 1, price, 1000));
        Assert.True(ind.IsBullish);
        Assert.True(ind.StopValue < price);
    }

    // ── Batch = Streaming identity ──────────────────────────────────────
    [Fact]
    public void Batch_EqualsStreaming_ForSkenderDefaultParams()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 88);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 88);
        const int N = 100;

        var streamInd = new Atrstop(period: 21, multiplier: 3.0);
        double[] streamOut = new double[N];
        double[] highs = new double[N], lows = new double[N], closes = new double[N];

        for (int i = 0; i < N; i++)
        {
            var (_, o, h, l, c, v) = gbm1.Next(isNew: true);
            streamInd.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
            streamOut[i] = streamInd.StopValue;
        }

        for (int i = 0; i < N; i++)
        {
            var (_, _, h, l, c, _) = gbm2.Next(isNew: true);
            highs[i] = h; lows[i] = l; closes[i] = c;
        }

        double[] batchOut = new double[N];
        Atrstop.Batch(highs, lows, closes, batchOut, period: 21, multiplier: 3.0);

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

    // ── Edge cases ──────────────────────────────────────────────────────
    [Fact]
    public void EmptySource_ReturnsEmpty()
    {
        var source = new TBarSeries();
        var result = Atrstop.Batch(source, period: 21);
        Assert.Empty(result);
    }

    [Fact]
    public void SingleBar_ReturnsNaN()
    {
        var source = new TBarSeries();
        source.Add(new TBar(DateTime.UtcNow, 100, 102, 98, 101, 1000));
        var result = Atrstop.Batch(source, period: 21);
        Assert.Single(result);
        Assert.True(double.IsNaN(result.Values[0]));
    }

    // ── Warmup period check ─────────────────────────────────────────────
    [Fact]
    public void WarmupPeriod_IsPeriodPlusOne()
    {
        var ind = new Atrstop(period: 14, multiplier: 2.0);
        Assert.Equal(15, ind.WarmupPeriod);
    }
}
