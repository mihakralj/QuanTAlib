using Xunit;

namespace QuanTAlib.Tests;

public sealed class KdjTests
{
    // ── A) Constructor validation ──────────────────────────────────────

    [Fact]
    public void Constructor_ValidParameters()
    {
        var kdj = new Kdj(length: 9, signal: 3);

        Assert.NotNull(kdj);
        Assert.Equal("Kdj(9,3)", kdj.Name);
        Assert.Equal(11, kdj.WarmupPeriod);
        Assert.False(kdj.IsHot);
    }

    [Fact]
    public void Constructor_InvalidLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kdj(length: 0, signal: 3));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeLength_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kdj(length: -5, signal: 3));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidSignal_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kdj(length: 9, signal: 0));
        Assert.Equal("signal", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeSignal_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kdj(length: 9, signal: -1));
        Assert.Equal("signal", ex.ParamName);
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        var result = kdj.Update(new TBar(time, 100, 110, 90, 105, 1000));

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_K_D_Accessible()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000));

        Assert.True(double.IsFinite(kdj.Last.Value));
        Assert.True(double.IsFinite(kdj.K.Value));
        Assert.True(double.IsFinite(kdj.D.Value));
    }

    [Fact]
    public void Name_ContainsKdj()
    {
        var kdj = new Kdj(length: 14, signal: 5);
        Assert.Contains("Kdj", kdj.Name, StringComparison.Ordinal);
        Assert.Contains("14", kdj.Name, StringComparison.Ordinal);
        Assert.Contains("5", kdj.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstantPrice_KDConvergeToFifty()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        // With constant OHLC, range = 0, RSV = 50
        // Need enough iterations for exponential warmup compensator to converge
        for (int i = 0; i < 100; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 100, 100, 100, 100, 1000));
        }

        Assert.Equal(50.0, kdj.K.Value, 1e-3);
        Assert.Equal(50.0, kdj.D.Value, 1e-3);
        // J = 3*50 - 2*50 = 50
        Assert.Equal(50.0, kdj.Last.Value, 1e-3);
    }

    [Fact]
    public void CloseAtHigh_KConvergesToHundred()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        // Close always at the high of the range => RSV = 100
        for (int i = 0; i < 50; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 100, 110, 90, 110, 1000));
        }

        Assert.True(kdj.K.Value > 99.0);
        Assert.True(kdj.D.Value > 99.0);
    }

    [Fact]
    public void CloseAtLow_KConvergesToZero()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        // Close always at the low of the range => RSV = 0
        for (int i = 0; i < 50; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 100, 110, 90, 90, 1000));
        }

        Assert.True(kdj.K.Value < 1.0);
        Assert.True(kdj.D.Value < 1.0);
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        double k1 = kdj.K.Value;

        kdj.Update(new TBar(time.AddSeconds(1), 101, 115, 95, 112, 1000), isNew: true);
        double k2 = kdj.K.Value;

        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        kdj.Update(new TBar(time.AddSeconds(1), 101, 111, 91, 106, 1000), isNew: true);
        kdj.Update(new TBar(time.AddSeconds(2), 102, 112, 92, 107, 1000), isNew: true);

        double kBefore = kdj.K.Value;
        double dBefore = kdj.D.Value;

        // Correct current bar with different close
        kdj.Update(new TBar(time.AddSeconds(2), 102, 120, 85, 115, 1000), isNew: false);

        double kAfter = kdj.K.Value;
        double dAfter = kdj.D.Value;

        Assert.NotEqual(kBefore, kAfter);
        Assert.NotEqual(dBefore, dAfter);
    }

    [Fact]
    public void IterativeCorrections_RestoreState()
    {
        var kdj = new Kdj(length: 5, signal: 3);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        TBar remembered = default;
        for (int i = 0; i < 10; i++)
        {
            remembered = gbm.Next(isNew: true);
            kdj.Update(remembered, isNew: true);
        }

        double snapK = kdj.K.Value;
        double snapD = kdj.D.Value;
        double snapJ = kdj.Last.Value;

        // Several corrections
        for (int i = 0; i < 5; i++)
        {
            var corrected = gbm.Next(isNew: false);
            kdj.Update(corrected, isNew: false);
        }

        // Restore original bar
        kdj.Update(remembered, isNew: false);

        Assert.Equal(snapK, kdj.K.Value, 1e-10);
        Assert.Equal(snapD, kdj.D.Value, 1e-10);
        Assert.Equal(snapJ, kdj.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var kdj = new Kdj(length: 5, signal: 3);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 7);

        for (int i = 0; i < 10; i++)
        {
            kdj.Update(gbm.Next(isNew: true), isNew: true);
        }

        Assert.True(kdj.IsHot);

        kdj.Reset();

        Assert.False(kdj.IsHot);
        Assert.Equal(0.0, kdj.Last.Value);
        Assert.Equal(0.0, kdj.K.Value);
        Assert.Equal(0.0, kdj.D.Value);
    }

    // ── D) Warmup / convergence ────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAfterLengthBars()
    {
        var kdj = new Kdj(length: 5, signal: 3);
        DateTime time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 100 + i, 101 + i, 99 + i, 100 + i, 1000));
            Assert.False(kdj.IsHot);
        }

        kdj.Update(new TBar(time.AddSeconds(4), 104, 105, 103, 104, 1000));
        Assert.True(kdj.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsLengthPlusSignalMinusOne()
    {
        var kdj = new Kdj(length: 9, signal: 3);
        Assert.Equal(11, kdj.WarmupPeriod);

        var kdj2 = new Kdj(length: 14, signal: 5);
        Assert.Equal(18, kdj2.WarmupPeriod);
    }

    // ── E) Robustness (NaN / Infinity) ─────────────────────────────────

    [Fact]
    public void NaN_HighUsesLastValid()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000));
        kdj.Update(new TBar(time.AddSeconds(1), 101, 111, 91, 106, 1000));
        var result = kdj.Update(new TBar(time.AddSeconds(2), 102, double.NaN, 92, 107, 1000));

        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(kdj.K.Value));
        Assert.True(double.IsFinite(kdj.D.Value));
    }

    [Fact]
    public void NaN_LowUsesLastValid()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000));
        kdj.Update(new TBar(time.AddSeconds(1), 101, 111, 91, 106, 1000));
        var result = kdj.Update(new TBar(time.AddSeconds(2), 102, 112, double.NaN, 107, 1000));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void NaN_CloseUsesLastValid()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000));
        kdj.Update(new TBar(time.AddSeconds(1), 101, 111, 91, 106, 1000));
        var result = kdj.Update(new TBar(time.AddSeconds(2), 102, 112, 92, double.NaN, 1000));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_HandledGracefully()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000));
        kdj.Update(new TBar(time.AddSeconds(1), 101, 111, 91, 106, 1000));
        var result = kdj.Update(new TBar(time.AddSeconds(2), 102, double.PositiveInfinity, 92, 107, 1000));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        DateTime time = DateTime.UtcNow;

        // All NaN inputs at the start
        var result = kdj.Update(new TBar(time, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        Assert.True(double.IsNaN(result.Value));

        // Then valid data
        result = kdj.Update(new TBar(time.AddSeconds(1), 100, 110, 90, 105, 1000));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── F) Consistency (4 API modes) ───────────────────────────────────

    [Fact]
    public void AllFourModes_ProduceConsistentResults()
    {
        const int length = 9;
        const int signal = 3;
        int barCount = 50;

        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 123);
        var bars = new TBarSeries();
        for (int i = 0; i < barCount; i++)
        {
            bars.Add(gbm.Next(isNew: true));
        }

        // Mode 1: Streaming
        var streamKdj = new Kdj(length, signal);
        for (int i = 0; i < barCount; i++)
        {
            streamKdj.Update(bars[i], isNew: true);
        }
        double streamK = streamKdj.K.Value;
        double streamD = streamKdj.D.Value;
        double streamJ = streamKdj.Last.Value;

        // Mode 2: Batch via instance Update(TBarSeries)
        var batchKdj = new Kdj(length, signal);
        var (bK, bD, bJ) = batchKdj.Update(bars);
        double batchK = bK.Values[^1];
        double batchD = bD.Values[^1];
        double batchJ = bJ.Values[^1];

        // Mode 3: Static Batch
        var (sK, sD, sJ) = Kdj.Batch(bars, length, signal);
        double staticK = sK.Values[^1];
        double staticD = sD.Values[^1];
        double staticJ = sJ.Values[^1];

        // Mode 4: Static Calculate
        var ((cK, cD, cJ), _) = Kdj.Calculate(bars, length, signal);
        double calcK = cK.Values[^1];
        double calcD = cD.Values[^1];
        double calcJ = cJ.Values[^1];

        // All modes must produce same results
        Assert.Equal(streamK, batchK, 1e-10);
        Assert.Equal(streamD, batchD, 1e-10);
        Assert.Equal(streamJ, batchJ, 1e-10);

        Assert.Equal(streamK, staticK, 1e-10);
        Assert.Equal(streamD, staticD, 1e-10);
        Assert.Equal(streamJ, staticJ, 1e-10);

        Assert.Equal(streamK, calcK, 1e-10);
        Assert.Equal(streamD, calcD, 1e-10);
        Assert.Equal(streamJ, calcJ, 1e-10);
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Batch_Span_InvalidLength_Throws()
    {
        double[] high = [1, 2, 3];
        double[] low = [0.5, 1.5, 2.5];
        double[] close = [0.8, 1.8, 2.8];
        double[] kOut = new double[3];
        double[] dOut = new double[3];
        double[] jOut = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Kdj.Batch(high, low, close, kOut, dOut, jOut, 0, 3));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidSignal_Throws()
    {
        double[] high = [1, 2, 3];
        double[] low = [0.5, 1.5, 2.5];
        double[] close = [0.8, 1.8, 2.8];
        double[] kOut = new double[3];
        double[] dOut = new double[3];
        double[] jOut = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Kdj.Batch(high, low, close, kOut, dOut, jOut, 3, 0));
        Assert.Equal("signal", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MismatchedInputs_Throws()
    {
        double[] high = [1, 2, 3];
        double[] low = [0.5, 1.5];
        double[] close = [0.8, 1.8, 2.8];
        double[] kOut = new double[3];
        double[] dOut = new double[3];
        double[] jOut = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Kdj.Batch(high, low, close, kOut, dOut, jOut, 3, 3));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ShortKOutput_Throws()
    {
        double[] high = [1, 2, 3];
        double[] low = [0.5, 1.5, 2.5];
        double[] close = [0.8, 1.8, 2.8];
        double[] kOut = new double[2]; // too short
        double[] dOut = new double[3];
        double[] jOut = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Kdj.Batch(high, low, close, kOut, dOut, jOut, 3, 3));
        Assert.Equal("kOut", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ShortDOutput_Throws()
    {
        double[] high = [1, 2, 3];
        double[] low = [0.5, 1.5, 2.5];
        double[] close = [0.8, 1.8, 2.8];
        double[] kOut = new double[3];
        double[] dOut = new double[2]; // too short
        double[] jOut = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Kdj.Batch(high, low, close, kOut, dOut, jOut, 3, 3));
        Assert.Equal("dOut", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ShortJOutput_Throws()
    {
        double[] high = [1, 2, 3];
        double[] low = [0.5, 1.5, 2.5];
        double[] close = [0.8, 1.8, 2.8];
        double[] kOut = new double[3];
        double[] dOut = new double[3];
        double[] jOut = new double[2]; // too short

        var ex = Assert.Throws<ArgumentException>(() =>
            Kdj.Batch(high, low, close, kOut, dOut, jOut, 3, 3));
        Assert.Equal("jOut", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        int barCount = 30;
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 77);
        var bars = new TBarSeries();
        for (int i = 0; i < barCount; i++)
        {
            bars.Add(gbm.Next(isNew: true));
        }

        // Streaming
        var kdj = new Kdj(length: 5, signal: 3);
        for (int i = 0; i < barCount; i++)
        {
            kdj.Update(bars[i], isNew: true);
        }

        // Span
        double[] kOut = new double[barCount];
        double[] dOut = new double[barCount];
        double[] jOut = new double[barCount];
        Kdj.Batch(bars.HighValues, bars.LowValues, bars.CloseValues,
            kOut, dOut, jOut, 5, 3);

        Assert.Equal(kdj.K.Value, kOut[^1], 1e-10);
        Assert.Equal(kdj.D.Value, dOut[^1], 1e-10);
        Assert.Equal(kdj.Last.Value, jOut[^1], 1e-10);
    }

    [Fact]
    public void Batch_Span_LargeData_NoStackOverflow()
    {
        int barCount = 1000;
        double[] high = new double[barCount];
        double[] low = new double[barCount];
        double[] close = new double[barCount];
        double[] kOut = new double[barCount];
        double[] dOut = new double[barCount];
        double[] jOut = new double[barCount];

        for (int i = 0; i < barCount; i++)
        {
            high[i] = 100.0 + (i * 0.1);
            low[i] = 99.0 + (i * 0.1);
            close[i] = 99.5 + (i * 0.1);
        }

        // Should not throw StackOverflowException (uses ArrayPool for > 256)
        Kdj.Batch(high, low, close, kOut, dOut, jOut, 14, 3);

        Assert.True(double.IsFinite(kOut[^1]));
        Assert.True(double.IsFinite(dOut[^1]));
        Assert.True(double.IsFinite(jOut[^1]));
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_EventFires()
    {
        var kdj = new Kdj(length: 3, signal: 2);
        int fired = 0;
        kdj.Pub += (object? _, in TValueEventArgs _) => fired++;

        DateTime time = DateTime.UtcNow;
        kdj.Update(new TBar(time, 100, 110, 90, 105, 1000));
        kdj.Update(new TBar(time.AddSeconds(1), 101, 111, 91, 106, 1000));

        Assert.Equal(2, fired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var bars = new TBarSeries();
        var kdj = new Kdj(bars, length: 5, signal: 3);

        int fired = 0;
        kdj.Pub += (object? _, in TValueEventArgs _) => fired++;

        DateTime time = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            bars.Add(new TBar(time.AddSeconds(i), 100 + i, 110 + i, 90 + i, 105 + i, 1000));
        }

        Assert.Equal(10, fired);
        Assert.True(kdj.IsHot);
    }

    // ── Additional: J line properties ──────────────────────────────────

    [Fact]
    public void J_CanExceedHundred()
    {
        // J = 3K - 2D. When K > D significantly, J > 100
        var kdj = new Kdj(length: 3, signal: 3);
        DateTime time = DateTime.UtcNow;

        // Sharp upward move should make K > D, and J can exceed 100
        for (int i = 0; i < 3; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 100, 105, 95, 100, 1000));
        }
        // Now sharp move up
        for (int i = 3; i < 8; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 100 + ((i - 2) * 5), 110 + ((i - 2) * 5), 95 + ((i - 2) * 5), 110 + ((i - 2) * 5), 1000));
        }

        // J should be able to exceed 100 (it's unbounded)
        // This is a property test - we just verify J is computed as 3K-2D
        double expectedJ = (3.0 * kdj.K.Value) - (2.0 * kdj.D.Value);
        Assert.Equal(expectedJ, kdj.Last.Value, 1e-10);
    }

    [Fact]
    public void J_CanGoNegative()
    {
        // J = 3K - 2D. When D > K significantly, J < 0
        var kdj = new Kdj(length: 3, signal: 3);
        DateTime time = DateTime.UtcNow;

        // Start high
        for (int i = 0; i < 3; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 200, 210, 190, 210, 1000));
        }
        // Sharp move down
        for (int i = 3; i < 8; i++)
        {
            kdj.Update(new TBar(time.AddSeconds(i), 200 - ((i - 2) * 5), 210 - ((i - 2) * 5), 190 - ((i - 2) * 5), 190 - ((i - 2) * 5), 1000));
        }

        double expectedJ = (3.0 * kdj.K.Value) - (2.0 * kdj.D.Value);
        Assert.Equal(expectedJ, kdj.Last.Value, 1e-10);
    }

    [Fact]
    public void K_D_ClampedBetween0And100()
    {
        var kdj = new Kdj(length: 5, signal: 3);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.3, seed: 99);

        for (int i = 0; i < 100; i++)
        {
            kdj.Update(gbm.Next(isNew: true), isNew: true);

            Assert.True(kdj.K.Value >= 0.0 && kdj.K.Value <= 100.0,
                $"K={kdj.K.Value} out of [0,100] at bar {i}");
            Assert.True(kdj.D.Value >= 0.0 && kdj.D.Value <= 100.0,
                $"D={kdj.D.Value} out of [0,100] at bar {i}");
        }
    }

    [Fact]
    public void Prime_SetsCorrectState()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 55);
        var bars = new TBarSeries();
        for (int i = 0; i < 20; i++)
        {
            bars.Add(gbm.Next(isNew: true));
        }

        // Prime from TBarSeries
        var kdj1 = new Kdj(length: 5, signal: 3);
        kdj1.Prime(bars);

        // Manual streaming
        var kdj2 = new Kdj(length: 5, signal: 3);
        for (int i = 0; i < 20; i++)
        {
            kdj2.Update(bars[i], isNew: true);
        }

        Assert.Equal(kdj2.K.Value, kdj1.K.Value, 1e-10);
        Assert.Equal(kdj2.D.Value, kdj1.D.Value, 1e-10);
        Assert.Equal(kdj2.Last.Value, kdj1.Last.Value, 1e-10);
    }

    [Fact]
    public void Batch_EmptySource_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var (k, d, j) = Kdj.Batch(bars, 9, 3);

        Assert.Empty(k);
        Assert.Empty(d);
        Assert.Empty(j);
    }

    [Fact]
    public void Batch_NullSource_ReturnsEmpty()
    {
        var (k, d, j) = Kdj.Batch(null!, 9, 3);

        Assert.Empty(k);
        Assert.Empty(d);
        Assert.Empty(j);
    }
}
