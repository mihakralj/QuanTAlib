using Xunit;

namespace QuanTAlib.Tests;

public sealed class StmacdValidationTests : IDisposable
{
    private readonly Stmacd _ind;
    private readonly TBarSeries _bars;

    public StmacdValidationTests()
    {
        _ind = new Stmacd(45, 12, 26, 9);
        var gbm = new GBM(mu: 0.05, sigma: 0.2);
        _bars = gbm.Fetch(500, DateTime.UtcNow.AddDays(-500).Ticks,
            TimeSpan.FromDays(1));
    }

    public void Dispose()
    {
        _ind.Reset();
    }

    // ── Mathematical Identity ─────────────────────────────────
    // STMACD = 100 × MACD / PriceRange
    // where MACD = EMA(close,fast) - EMA(close,slow)
    //       PriceRange = Highest(high,periods) - Lowest(low,periods)

    [Fact]
    public void StmacdEquivalence_MacdOverRange()
    {
        // Run streaming to compute STMACD normally
        for (int i = 0; i < _bars.Count; i++)
        {
            _ind.Update(_bars[i], true);
        }

        // Independently compute MACD and range at the last bar
        double fastAlpha = 2.0 / 13;
        double slowAlpha = 2.0 / 27;
        double fastEma = _bars[0].Close;
        double slowEma = _bars[0].Close;

        for (int i = 1; i < _bars.Count; i++)
        {
            fastEma = fastAlpha * _bars[i].Close + (1 - fastAlpha) * fastEma;
            slowEma = slowAlpha * _bars[i].Close + (1 - slowAlpha) * slowEma;
        }

        // Compute Highest(high, 45) and Lowest(low, 45) over last 45 bars
        double highest = double.MinValue;
        double lowest = double.MaxValue;
        int start = _bars.Count - 45;
        for (int i = start; i < _bars.Count; i++)
        {
            if (_bars[i].High > highest) { highest = _bars[i].High; }
            if (_bars[i].Low < lowest) { lowest = _bars[i].Low; }
        }

        double range = highest - lowest;
        double expectedStmacd = range > 0 ? 100.0 * (fastEma - slowEma) / range : 0.0;

        Assert.Equal(expectedStmacd, _ind.StmacdValue.Value, 6);
    }

    // ── Batch vs Streaming Parity ─────────────────────────────

    [Fact]
    public void BatchMatchesStreaming_AllBars()
    {
        var (bStmacd, bSignal) = Stmacd.Batch(_bars);

        var streaming = new Stmacd(45, 12, 26, 9);
        for (int i = 0; i < _bars.Count; i++)
        {
            streaming.Update(_bars[i], true);
        }

        // Check last 50 bars for parity
        for (int i = _bars.Count - 50; i < _bars.Count; i++)
        {
            var s = new Stmacd(45, 12, 26, 9);
            for (int j = 0; j <= i; j++)
            {
                s.Update(_bars[j], true);
            }
            Assert.Equal(bStmacd.Values[i], s.StmacdValue.Value, 8);
            Assert.Equal(bSignal.Values[i], s.Signal.Value, 8);
        }
    }

    // ── Signal is smoother than STMACD ────────────────────────

    [Fact]
    public void Signal_IsSmoother_ThanStmacd()
    {
        var (bStmacd, bSignal) = Stmacd.Batch(_bars);

        // Compare variance of STMACD vs Signal over hot bars
        int startIdx = 45;
        double sumSqStmacd = 0, sumStmacd = 0;
        double sumSqSignal = 0, sumSignal = 0;
        int n = _bars.Count - startIdx;

        for (int i = startIdx; i < _bars.Count; i++)
        {
            double s = bStmacd.Values[i];
            double sig = bSignal.Values[i];
            sumStmacd += s;
            sumSqStmacd += s * s;
            sumSignal += sig;
            sumSqSignal += sig * sig;
        }

        double varStmacd = sumSqStmacd / n - (sumStmacd / n) * (sumStmacd / n);
        double varSignal = sumSqSignal / n - (sumSignal / n) * (sumSignal / n);

        Assert.True(varSignal < varStmacd,
            $"Signal variance ({varSignal:F4}) should be less than STMACD variance ({varStmacd:F4})");
    }

    // ── Constant price → zero ─────────────────────────────────

    [Fact]
    public void ConstantPrice_AllZero()
    {
        var constBars = new TBarSeries();
        var t = DateTime.UtcNow.AddDays(-100);
        for (int i = 0; i < 100; i++)
        {
            constBars.Add(new TBar(t.AddDays(i), 50, 50, 50, 50, 1000));
        }

        var (bStmacd, bSignal) = Stmacd.Batch(constBars);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(0.0, bStmacd.Values[i], 10);
            Assert.Equal(0.0, bSignal.Values[i], 10);
        }
    }

    // ── Symmetry: reversing trend flips sign ──────────────────

    [Fact]
    public void Symmetry_UptrendDowntrend_OppositeSign()
    {
        int p = 20, f = 5, s = 15, sig = 3;

        // Uptrend bars
        var upBars = new TBarSeries();
        var t = DateTime.UtcNow.AddDays(-100);
        for (int i = 0; i < 100; i++)
        {
            double c = 50 + i * 0.5;
            upBars.Add(new TBar(t.AddDays(i), c + 1, c + 2, c - 1, c, 1000));
        }

        // Downtrend bars (mirrored)
        var downBars = new TBarSeries();
        for (int i = 0; i < 100; i++)
        {
            double c = 50 + (99 - i) * 0.5;
            downBars.Add(new TBar(t.AddDays(i), c + 1, c + 2, c - 1, c, 1000));
        }

        var upInd = new Stmacd(p, f, s, sig);
        var downInd = new Stmacd(p, f, s, sig);

        for (int i = 0; i < 100; i++)
        {
            upInd.Update(upBars[i], true);
            downInd.Update(downBars[i], true);
        }

        // Uptrend should be positive, downtrend negative
        Assert.True(upInd.StmacdValue.Value > 0);
        Assert.True(downInd.StmacdValue.Value < 0);
    }

    // ── Cross-validation with manual stochastic decomposition ──

    [Fact]
    public void CrossValidate_StochasticDecomposition()
    {
        var ind = new Stmacd(20, 5, 15, 3);

        for (int i = 0; i < _bars.Count; i++)
        {
            ind.Update(_bars[i], true);
        }

        // Manual: compute EMA(5) and EMA(15) of close
        double a5 = 2.0 / 6;
        double a15 = 2.0 / 16;
        double ema5 = _bars[0].Close;
        double ema15 = _bars[0].Close;

        for (int i = 1; i < _bars.Count; i++)
        {
            ema5 = a5 * _bars[i].Close + (1 - a5) * ema5;
            ema15 = a15 * _bars[i].Close + (1 - a15) * ema15;
        }

        // Highest(high,20) and Lowest(low,20) over last 20 bars
        double hh = double.MinValue, ll = double.MaxValue;
        int s2 = _bars.Count - 20;
        for (int i = s2; i < _bars.Count; i++)
        {
            if (_bars[i].High > hh) { hh = _bars[i].High; }
            if (_bars[i].Low < ll) { ll = _bars[i].Low; }
        }

        double r = hh - ll;
        double fStoch = r > 0 ? (ema5 - ll) / r : 0;
        double sStoch = r > 0 ? (ema15 - ll) / r : 0;
        double expected = (fStoch - sStoch) * 100;

        Assert.Equal(expected, ind.StmacdValue.Value, 6);
    }

    // ── Output bounded in typical scenarios ───────────────────

    [Fact]
    public void Output_TypicallyBounded()
    {
        var (bStmacd, _) = Stmacd.Batch(_bars);

        int hotStart = 45;
        for (int i = hotStart; i < _bars.Count; i++)
        {
            double v = bStmacd.Values[i];
            Assert.True(v >= -200 && v <= 200,
                $"STMACD value {v} at index {i} outside expected range");
        }
    }
}
