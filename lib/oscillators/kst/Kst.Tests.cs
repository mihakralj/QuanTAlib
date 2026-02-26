using Xunit;

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ────────────────────────────────────────────────
public sealed class KstConstructorTests
{
    [Fact]
    public void Constructor_ZeroR1_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kst(r1: 0));
        Assert.Equal("r1", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeR2_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kst(r2: -1));
        Assert.Equal("r2", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroR3_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kst(r3: 0));
        Assert.Equal("r3", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroR4_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kst(r4: 0));
        Assert.Equal("r4", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroS1_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kst(s1: 0));
        Assert.Equal("s1", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroS4_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kst(s4: 0));
        Assert.Equal("s4", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroSigPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kst(sigPeriod: 0));
        Assert.Equal("sigPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_Defaults_Creates()
    {
        var kst = new Kst();
        Assert.NotNull(kst);
        Assert.Contains("Kst", kst.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsPositive()
    {
        var kst = new Kst();
        Assert.True(kst.WarmupPeriod > 0);
    }

    [Fact]
    public void Constructor_CustomParams_NameReflectsThem()
    {
        var kst = new Kst(r1: 5, r2: 8, r3: 10, r4: 15, s1: 3, s2: 3, s3: 3, s4: 5, sigPeriod: 4);
        Assert.Contains("5", kst.Name, StringComparison.Ordinal);
        Assert.Contains("4", kst.Name, StringComparison.Ordinal);
    }
}

// ── B) Basic Calculation ─────────────────────────────────────────────────────
public sealed class KstBasicTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var kst = new Kst();
        var result = kst.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(result.Value, kst.Last.Value);
    }

    [Fact]
    public void FirstBar_OutputIsFinite()
    {
        var kst = new Kst();
        var result = kst.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Signal_IsFiniteAfterFirstBar()
    {
        var kst = new Kst();
        kst.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(kst.Signal.Value));
    }

    [Fact]
    public void Name_Available()
    {
        var kst = new Kst();
        Assert.False(string.IsNullOrEmpty(kst.Name));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var kst = new Kst(r1: 3, r2: 5, r3: 7, r4: 9, s1: 3, s2: 3, s3: 3, s4: 3, sigPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(double.IsFinite(kst.Last.Value));
        Assert.True(double.IsFinite(kst.KstValue.Value));
        Assert.True(double.IsFinite(kst.Signal.Value));
    }

    [Fact]
    public void ConstantPrice_KstIsZero()
    {
        // All ROC = 0 → KST = 0
        var kst = new Kst(r1: 2, r2: 3, r3: 4, r4: 5, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        for (int i = 0; i < 20; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        Assert.Equal(0.0, kst.KstValue.Value, 1e-10);
        Assert.Equal(0.0, kst.Signal.Value, 1e-10);
    }
}

// ── C) State + Bar Correction ────────────────────────────────────────────────
public sealed class KstBarCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var kst = new Kst(r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        for (int i = 0; i < 5; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0 + i * 2), isNew: true);
        }
        double val1 = kst.Last.Value;
        kst.Update(new TValue(DateTime.UtcNow, 110.0), isNew: true);
        double val2 = kst.Last.Value;
        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
    }

    [Fact]
    public void IsNew_False_Rollback()
    {
        var kst = new Kst(r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            kst.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        var nextBar = gbm.Next(isNew: true);
        var originalInput = new TValue(nextBar.Time, nextBar.Close);
        var val1 = kst.Update(originalInput, isNew: true);

        // Overwrite with different value
        kst.Update(new TValue(nextBar.Time, nextBar.Close + 50), isNew: false);

        // Restore original → must match
        var restored = kst.Update(originalInput, isNew: false);
        Assert.Equal(val1.Value, restored.Value, 1e-10);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var kst = new Kst(r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue twentyInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentyInput = new TValue(bar.Time, bar.Close);
            kst.Update(twentyInput, isNew: true);
        }

        double stateAfterTwenty = kst.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            kst.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        var finalResult = kst.Update(twentyInput, isNew: false);
        Assert.Equal(stateAfterTwenty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var kst = new Kst(r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        for (int i = 0; i < 20; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        kst.Reset();
        Assert.False(kst.IsHot);
        Assert.Equal(0.0, kst.Last.Value);
    }
}

// ── D) Warmup / Convergence ──────────────────────────────────────────────────
public sealed class KstWarmupTests
{
    [Fact]
    public void IsHot_InitiallyFalse()
    {
        var kst = new Kst();
        Assert.False(kst.IsHot);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmupPeriodBars()
    {
        var kst = new Kst(r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        int warmup = kst.WarmupPeriod;

        for (int i = 1; i < warmup; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(kst.IsHot, $"Should not be hot at bar {i} (need {warmup})");
        }

        kst.Update(new TValue(DateTime.UtcNow, 100.0 + warmup));
        Assert.True(kst.IsHot);
    }

    [Fact]
    public void WarmupPeriod_DependsOnParameters()
    {
        var kst1 = new Kst(r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        var kst2 = new Kst(r1: 5, r2: 8, r3: 10, r4: 15, s1: 5, s2: 5, s3: 5, s4: 5, sigPeriod: 5);
        Assert.True(kst2.WarmupPeriod > kst1.WarmupPeriod);
    }
}

// ── E) Robustness ────────────────────────────────────────────────────────────
public sealed class KstRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var kst = new Kst(r1: 2, r2: 3, r3: 4, r4: 5, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        for (int i = 0; i < 10; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        kst.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(kst.Last.Value), "NaN input should not produce NaN output");
    }

    [Fact]
    public void PositiveInfinity_UsesLastValidValue()
    {
        var kst = new Kst(r1: 2, r2: 3, r3: 4, r4: 5, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        for (int i = 0; i < 10; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        kst.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(kst.Last.Value));
    }

    [Fact]
    public void BatchNaN_SafeOutput()
    {
        var kst = new Kst(r1: 2, r2: 3, r3: 4, r4: 5, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        kst.Update(new TValue(DateTime.UtcNow, 100.0));
        for (int i = 0; i < 5; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        kst.Update(new TValue(DateTime.UtcNow, 110.0));
        Assert.True(double.IsFinite(kst.Last.Value));
    }
}

// ── F) Consistency (all 4 API modes agree) ───────────────────────────────────
public sealed class KstConsistencyTests
{
    private static TSeries MakeSeries(double[] vals)
    {
        var times = new List<long>(vals.Length);
        var values = new List<double>(vals.Length);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < vals.Length; i++)
        {
            times.Add(t0.AddSeconds(i).Ticks);
            values.Add(vals[i]);
        }
        return new TSeries(times, values);
    }

    [Fact]
    public void Streaming_Equals_Batch_TSeries()
    {
        int r1 = 3, r2 = 4, r3 = 5, r4 = 6, s1 = 2, s2 = 2, s3 = 2, s4 = 2, sig = 2;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 7);
        int count = 50;
        var prices = new double[count];
        for (int i = 0; i < count; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        // Streaming
        var kstStream = new Kst(r1, r2, r3, r4, s1, s2, s3, s4, sig);
        var streamK = new double[count];
        var streamS = new double[count];
        for (int i = 0; i < count; i++)
        {
            kstStream.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
            streamK[i] = kstStream.KstValue.Value;
            streamS[i] = kstStream.Signal.Value;
        }

        // Batch TSeries
        var series = MakeSeries(prices);
        var kstBatch = new Kst(r1, r2, r3, r4, s1, s2, s3, s4, sig);
        var (batchK, batchSig) = kstBatch.Update(series);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamK[i], batchK.Values[i], 1e-9);
            Assert.Equal(streamS[i], batchSig.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Span_Equals_Streaming()
    {
        int r1 = 3, r2 = 4, r3 = 5, r4 = 6, s1 = 2, s2 = 2, s3 = 2, s4 = 2, sig = 2;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 11);
        int count = 60;
        var prices = new double[count];
        for (int i = 0; i < count; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        // Span Batch
        var spanK = new double[count];
        var spanS = new double[count];
        Kst.Batch(prices, spanK, spanS, r1, r2, r3, r4, s1, s2, s3, s4, sig);

        // Streaming
        var kstStream = new Kst(r1, r2, r3, r4, s1, s2, s3, s4, sig);
        for (int i = 0; i < count; i++)
        {
            kstStream.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
            Assert.Equal(spanK[i], kstStream.KstValue.Value, 1e-9);
            Assert.Equal(spanS[i], kstStream.Signal.Value, 1e-9);
        }
    }

    [Fact]
    public void Eventing_Equals_Manual_Streaming()
    {
        int r1 = 3, r2 = 4, r3 = 5, r4 = 6, s1 = 2, s2 = 2, s3 = 2, s4 = 2, sig = 2;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 13);
        var series = new TSeries();

        // Subscribe BEFORE adding data so Pub events fire into kstEvent
        var kstEvent = new Kst(series, r1, r2, r3, r4, s1, s2, s3, s4, sig);

        for (int i = 0; i < 30; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close), isNew: true);
        }
        double eventLast = kstEvent.Last.Value;

        // Manual streaming (replay same data)
        var kstManual = new Kst(r1, r2, r3, r4, s1, s2, s3, s4, sig);
        foreach (var tv in series)
        {
            kstManual.Update(tv, isNew: true);
        }

        Assert.Equal(eventLast, kstManual.Last.Value, 1e-9);
    }
}

// ── G) Span API Tests ────────────────────────────────────────────────────────
public sealed class KstSpanTests
{
    [Fact]
    public void Span_MismatchedOutputLength_ThrowsArgumentException()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] kstOut = new double[5];
        double[] sigOut = new double[4]; // wrong length
        var ex = Assert.Throws<ArgumentException>(() =>
            Kst.Batch(src, kstOut, sigOut));
        Assert.Equal("sigOut", ex.ParamName);
    }

    [Fact]
    public void Span_MismatchedKstOutputLength_ThrowsArgumentException()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] kstOut = new double[4]; // wrong length
        double[] sigOut = new double[5];
        var ex = Assert.Throws<ArgumentException>(() =>
            Kst.Batch(src, kstOut, sigOut));
        Assert.Equal("kstOut", ex.ParamName);
    }

    [Fact]
    public void Span_ZeroR1_ThrowsArgumentException()
    {
        double[] src = [1, 2, 3];
        double[] k = new double[3];
        double[] s = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Kst.Batch(src, k, s, r1: 0));
        Assert.Equal("r1", ex.ParamName);
    }

    [Fact]
    public void Span_EmptyInput_NoException()
    {
        double[] src = [];
        double[] k = [];
        double[] s = [];
        Kst.Batch(src, k, s); // should not throw
        Assert.Empty(src);
    }

    [Fact]
    public void Span_NaNInput_SafeOutput()
    {
        var prices = new double[50];
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 99);
        for (int i = 0; i < 50; i++) { prices[i] = gbm.Next(isNew: true).Close; }
        prices[10] = double.NaN;
        prices[20] = double.PositiveInfinity;

        var kOut = new double[50];
        var sOut = new double[50];
        Kst.Batch(prices, kOut, sOut, r1: 3, r2: 4, r3: 5, r4: 6, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);

        foreach (var v in kOut) { Assert.True(double.IsFinite(v)); }
        foreach (var v in sOut) { Assert.True(double.IsFinite(v)); }
    }

    [Fact]
    public void Span_LargeInput_NoStackOverflow()
    {
        int n = 5000;
        var prices = new double[n];
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.1, seed: 77);
        for (int i = 0; i < n; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var kOut = new double[n];
        var sOut = new double[n];
        Kst.Batch(prices, kOut, sOut); // default periods, large array
        Assert.True(double.IsFinite(kOut[^1]));
    }
}

// ── H) Chainability ──────────────────────────────────────────────────────────
public sealed class KstChainabilityTests
{
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var kst = new Kst(r1: 2, r2: 3, r3: 4, r4: 5, s1: 2, s2: 2, s3: 2, s4: 2, sigPeriod: 2);
        int fireCount = 0;
        kst.Pub += (object? _, in TValueEventArgs _e) => fireCount++;

        for (int i = 0; i < 5; i++)
        {
            kst.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.Equal(5, fireCount);
    }

    [Fact]
    public void EventBasedChaining_WorksCorrectly()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 5);

        var kst = new Kst(series,
            r1: 2, r2: 3, r3: 4, r4: 5,
            s1: 2, s2: 2, s3: 2, s4: 2,
            sigPeriod: 2);

        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close), isNew: true);
        }

        Assert.True(double.IsFinite(kst.Last.Value));
        Assert.True(double.IsFinite(kst.Signal.Value));
    }
}
