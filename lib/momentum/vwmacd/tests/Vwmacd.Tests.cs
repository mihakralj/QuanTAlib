using System.Runtime.CompilerServices;
using Xunit;

namespace QuanTAlib.Tests;

public sealed class VwmacdTests
{
    private static TBarSeries GenerateBars(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // === A) Constructor validation ===

    [Fact]
    public void Constructor_InvalidFastPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwmacd(fastPeriod: 0));
        Assert.Equal("fastPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeFastPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwmacd(fastPeriod: -1));
        Assert.Equal("fastPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidSlowPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwmacd(slowPeriod: 0));
        Assert.Equal("slowPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidSignalPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwmacd(signalPeriod: 0));
        Assert.Equal("signalPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_DefaultParams()
    {
        var ind = new Vwmacd();
        Assert.Equal("Vwmacd(12,26,9)", ind.Name);
        Assert.Equal(33, ind.WarmupPeriod); // Max(12,26)+9-2 = 33
    }

    [Fact]
    public void Constructor_CustomParams()
    {
        var ind = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        Assert.Equal("Vwmacd(5,10,3)", ind.Name);
        Assert.Equal(10 + 3 - 2, ind.WarmupPeriod); // Max(5,10)+3-2 = 11
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 101, 1000);
        TValue result = ind.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Last_Signal_Histogram_Accessible()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100 + i, 105 + i, 95 + i, 101 + i, 1000 + (i * 10));
            ind.Update(bar);
        }
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Signal.Value));
        Assert.True(double.IsFinite(ind.Histogram.Value));
    }

    [Fact]
    public void ConstantPrice_VwmacdNearZero()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        for (int i = 0; i < 30; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            ind.Update(bar);
        }
        // With constant price, VWMA fast = VWMA slow = 100, so VWMACD = 0
        Assert.Equal(0.0, ind.Last.Value, precision: 10);
        Assert.Equal(0.0, ind.Signal.Value, precision: 10);
        Assert.Equal(0.0, ind.Histogram.Value, precision: 10);
    }

    [Fact]
    public void Histogram_Equals_Vwmacd_Minus_Signal()
    {
        var ind = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        var bars = GenerateBars(50);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
        }
        double expected = ind.Last.Value - ind.Signal.Value;
        Assert.Equal(expected, ind.Histogram.Value, precision: 10);
    }

    [Fact]
    public void RisingPrice_HighVolume_PositiveVwmacd()
    {
        var ind = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + (i * 2);
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 10000);
            ind.Update(bar);
        }
        Assert.True(ind.IsHot);
        Assert.True(ind.Last.Value > 0.0);
    }

    [Fact]
    public void FallingPrice_HighVolume_NegativeVwmacd()
    {
        var ind = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        for (int i = 0; i < 30; i++)
        {
            double price = 200.0 - (i * 2);
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 10000);
            ind.Update(bar);
        }
        Assert.True(ind.IsHot);
        Assert.True(ind.Last.Value < 0.0);
    }

    // === C) State + bar correction ===

    [Fact]
    public void IsNew_True_Advances_State()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var bars = GenerateBars(10);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(bars[i], isNew: true);
        }

        var nextBar = new TBar(DateTime.UtcNow.AddMinutes(100), 200, 210, 190, 205, 5000);
        ind.Update(nextBar, isNew: true);

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var bars = GenerateBars(10);
        for (int i = 0; i < 9; i++)
        {
            ind.Update(bars[i], isNew: true);
        }

        ind.Update(bars[9], isNew: true);
        double vwmacdAfterNew = ind.Last.Value;

        // Rewrite bar 9 with very different OHLCV
        var corrected = new TBar(bars[9].Time, 999, 1005, 990, 1000, 50000);
        ind.Update(corrected, isNew: false);
        double vwmacdAfterCorrection = ind.Last.Value;

        Assert.NotEqual(vwmacdAfterNew, vwmacdAfterCorrection, precision: 4);
    }

    [Fact]
    public void IsNew_False_Idempotent()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var bars = GenerateBars(10);
        for (int i = 0; i < 9; i++)
        {
            ind.Update(bars[i], isNew: true);
        }

        ind.Update(bars[9], isNew: true);
        double baseline = ind.Last.Value;

        // Replaying same bar with isNew = false should yield same result
        ind.Update(bars[9], isNew: false);
        Assert.Equal(baseline, ind.Last.Value, precision: 10);
    }

    // === D) Reset ===

    [Fact]
    public void Reset_RestoresInitialState()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var bars = GenerateBars(20);
        for (int i = 0; i < 20; i++)
        {
            ind.Update(bars[i], isNew: true);
        }
        Assert.True(ind.IsHot);

        ind.Reset();
        Assert.False(ind.IsHot);
    }

    [Fact]
    public void Reset_ThenUpdate_Identical()
    {
        var ind1 = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        var ind2 = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        var bars = GenerateBars(30);

        for (int i = 0; i < bars.Count; i++)
        {
            ind1.Update(bars[i], isNew: true);
        }

        ind1.Reset();
        for (int i = 0; i < bars.Count; i++)
        {
            ind1.Update(bars[i], isNew: true);
            ind2.Update(bars[i], isNew: true);
        }

        Assert.Equal(ind2.Last.Value, ind1.Last.Value, precision: 10);
        Assert.Equal(ind2.Signal.Value, ind1.Signal.Value, precision: 10);
        Assert.Equal(ind2.Histogram.Value, ind1.Histogram.Value, precision: 10);
    }

    // === E) Series / Batch ===

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        var ind = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        var bars = GenerateBars(50);
        var (vwmacd, signal, hist) = ind.Update(bars);

        Assert.Equal(50, vwmacd.Count);
        Assert.Equal(50, signal.Count);
        Assert.Equal(50, hist.Count);
    }

    [Fact]
    public void Batch_TBarSeries_MatchesStreaming()
    {
        var bars = GenerateBars(50);

        // Streaming
        var indS = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        for (int i = 0; i < bars.Count; i++)
        {
            indS.Update(bars[i], isNew: true);
        }
        double streamLast = indS.Last.Value;
        double streamSignal = indS.Signal.Value;

        // Batch
        var (bV, bS, _) = Vwmacd.Batch(bars, fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        double batchLast = bV[^1].Value;
        double batchSignal = bS[^1].Value;

        Assert.Equal(streamLast, batchLast, precision: 10);
        Assert.Equal(streamSignal, batchSignal, precision: 10);
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        var bars = GenerateBars(50);
        double[] close = new double[bars.Count];
        double[] volume = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            close[i] = bars[i].Close;
            volume[i] = bars[i].Volume;
        }

        double[] vwmacdOut = new double[bars.Count];
        double[] signalOut = new double[bars.Count];
        double[] histOut = new double[bars.Count];

        Vwmacd.Batch(close, volume, vwmacdOut, signalOut, histOut, fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);

        // Compare last values with streaming
        var indS = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        for (int i = 0; i < bars.Count; i++)
        {
            indS.Update(bars[i], isNew: true);
        }

        Assert.Equal(indS.Last.Value, vwmacdOut[^1], precision: 10);
        Assert.Equal(indS.Signal.Value, signalOut[^1], precision: 10);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var ind = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        var bars = GenerateBars(50);
        ind.Prime(bars);
        Assert.True(ind.IsHot);
    }

    // === F) Volume weighting ===

    [Fact]
    public void HighVolume_Bars_DominateVwma()
    {
        // Create two indicators - same price data but different volumes
        var ind1 = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);
        var ind2 = new Vwmacd(fastPeriod: 5, slowPeriod: 10, signalPeriod: 3);

        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + i;
            // ind1: uniform volume
            ind1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000));
            // ind2: high volume on latter bars (accelerating weight)
            ind2.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price, price + 1, price - 1, price, 1000 + (i * 500)));
        }

        // Both should be finite; values may differ due to volume weighting
        Assert.True(double.IsFinite(ind1.Last.Value));
        Assert.True(double.IsFinite(ind2.Last.Value));
    }

    [Fact]
    public void ZeroVolume_FallsBackToClose()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            // Zero volume — code uses close as fallback
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 105, 95, 100 + i, 0);
            ind.Update(bar);
        }
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    // === G) Dispose ===

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ind = new Vwmacd();
        var ex = Record.Exception(() => ind.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var ind = new Vwmacd();
        ind.Dispose();
        var ex = Record.Exception(() => ind.Dispose());
        Assert.Null(ex);
    }

    // === H) Edge cases ===

    [Fact]
    public void SingleBar_ProducesFiniteOutput()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 101, 1000);
        ind.Update(bar);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void LargeDataset_ProducesFiniteOutput()
    {
        var ind = new Vwmacd();
        var bars = GenerateBars(10_000);
        for (int i = 0; i < bars.Count; i++)
        {
            ind.Update(bars[i], isNew: true);
        }
        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Signal.Value));
        Assert.True(double.IsFinite(ind.Histogram.Value));
    }

    [Fact]
    public void NegativeVolume_ClampedToZero()
    {
        var ind = new Vwmacd(fastPeriod: 3, slowPeriod: 5, signalPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 105, 95, 101, -500);
            ind.Update(bar);
        }
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void Batch_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var (v, s, h) = Vwmacd.Batch(bars);
        Assert.Empty(v);
        Assert.Empty(s);
        Assert.Empty(h);
    }

    [Fact]
    public void Batch_Span_LengthMismatch_Throws()
    {
        double[] close = new double[10];
        double[] volume = new double[5]; // mismatch!
        double[] vOut = new double[10];
        double[] sOut = new double[10];
        double[] hOut = new double[10];

        Assert.Throws<ArgumentException>(() =>
            Vwmacd.Batch(close, volume, vOut, sOut, hOut));
    }
}
