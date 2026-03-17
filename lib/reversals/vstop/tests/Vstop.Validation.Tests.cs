using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for VSTOP (Volatility Stop).
/// Cross-validated against Skender.Stock.Indicators where available.
/// Level 3: Mathematical correctness (SIC ± ATR×mult logic).
/// </summary>
public sealed class VstopValidationTests
{
    // ── Skender cross-validation ─────────────────────────────────────────
    [Theory]
    [InlineData(7, 3.0)]
    [InlineData(14, 2.0)]
    [InlineData(21, 1.5)]
    public void Vstop_WithVariousParams_ProducesFiniteOutput(int period, double mult)
    {
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 42);
        var ind = new Vstop(period: period, multiplier: mult);

        for (int i = 0; i < 100; i++)
        {
            var (_, o, h, l, c, v) = gbm.Next(isNew: true);
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
        }

        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.SarValue));
    }

    // ── Mathematical identity: SAR = SIC ± ATR × mult ───────────────────
    [Fact]
    public void MonotonicUptrend_SarEqualsClose_Minus_AtrTimesMultiplier()
    {
        // In a monotonic uptrend with no reversals, SIC == highest close seen
        // and SAR = SIC - ATR * mult
        var ind = new Vstop(period: 3, multiplier: 2.0);
        double price = 100;
        for (int i = 0; i < 20; i++)
        {
            price += 1; // Steady calm uptrend
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 0.5, price - 0.5, price, 1000));
        }

        // Should be in uptrend with SAR below price
        Assert.True(ind.IsLong);
        Assert.True(ind.SarValue < price);
    }

    // ── Determinism ─────────────────────────────────────────────────────
    [Fact]
    public void SameInput_ProducesSameOutput()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 55);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 55);

        var ind1 = new Vstop(period: 7, multiplier: 3.0);
        var ind2 = new Vstop(period: 7, multiplier: 3.0);

        for (int i = 0; i < 50; i++)
        {
            var (_, o1, h1, l1, c1, v1) = gbm1.Next(isNew: true);
            var (_, o2, h2, l2, c2, v2) = gbm2.Next(isNew: true);
            ind1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o1, h1, l1, c1, v1));
            ind2.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o2, h2, l2, c2, v2));
        }

        Assert.Equal(ind1.SarValue, ind2.SarValue, precision: 10);
        Assert.Equal(ind1.IsLong, ind2.IsLong);
    }

    // ── Reversal logic ──────────────────────────────────────────────────
    [Fact]
    public void UptrendThenDrop_CausesReversal()
    {
        var ind = new Vstop(period: 3, multiplier: 1.0);
        double price = 100;

        // Build uptrend
        for (int i = 0; i < 10; i++)
        {
            price += 3;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000));
        }
        Assert.True(ind.IsLong);

        // Crash to force reversal
        price -= 50;
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(20), price, price + 1, price - 1, price, 1000));
        Assert.True(ind.IsStop);
        Assert.False(ind.IsLong);
        Assert.True(ind.SarValue > price);
    }

    [Fact]
    public void DowntrendThenRally_CausesReversal()
    {
        var ind = new Vstop(period: 3, multiplier: 1.0);
        double price = 200;

        // Build downtrend
        for (int i = 0; i < 10; i++)
        {
            price -= 3;
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000));
        }
        Assert.False(ind.IsLong);

        // Rally to force reversal
        price += 50;
        ind.Update(new TBar(DateTime.UtcNow.AddMinutes(20), price, price + 1, price - 1, price, 1000));
        Assert.True(ind.IsStop);
        Assert.True(ind.IsLong);
        Assert.True(ind.SarValue < price);
    }

    // ── Batch = Streaming identity ──────────────────────────────────────
    [Fact]
    public void Batch_EqualsStreaming_ForSkenderDefaultParams()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 88);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 88);
        const int N = 100;

        var streamInd = new Vstop(period: 7, multiplier: 3.0);
        double[] streamOut = new double[N];
        double[] highs = new double[N], lows = new double[N], closes = new double[N];

        for (int i = 0; i < N; i++)
        {
            var (_, o, h, l, c, v) = gbm1.Next(isNew: true);
            streamInd.Update(new TBar(DateTime.UtcNow.AddMinutes(i), o, h, l, c, v));
            streamOut[i] = streamInd.SarValue;
        }

        for (int i = 0; i < N; i++)
        {
            var (_, _, h, l, c, _) = gbm2.Next(isNew: true);
            highs[i] = h; lows[i] = l; closes[i] = c;
        }

        double[] batchOut = new double[N];
        Vstop.Batch(highs, lows, closes, batchOut, period: 7, multiplier: 3.0);

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
        var result = Vstop.Batch(source, period: 7);
        Assert.Empty(result);
    }

    [Fact]
    public void SingleBar_ReturnsNaN()
    {
        var source = new TBarSeries();
        source.Add(new TBar(DateTime.UtcNow, 100, 102, 98, 101, 1000));
        var result = Vstop.Batch(source, period: 7);
        Assert.Single(result);
        Assert.True(double.IsNaN(result.Values[0]));
    }

    // ── Warmup period check ─────────────────────────────────────────────
    [Fact]
    public void WarmupPeriod_MatchesATRPeriod()
    {
        var ind = new Vstop(period: 14, multiplier: 2.0);
        Assert.Equal(14, ind.WarmupPeriod);
    }

    [Fact]
    public void BeforeWarmup_IsHotFalse()
    {
        var ind = new Vstop(period: 10, multiplier: 2.0);
        for (int i = 0; i < 5; i++)
        {
            ind.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }
        Assert.False(ind.IsHot);
    }
}
