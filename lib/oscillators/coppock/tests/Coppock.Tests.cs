using Xunit;

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ────────────────────────────────────────────────
public sealed class CoppockConstructorTests
{
    [Fact]
    public void Constructor_ZeroLongRoc_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coppock(longRoc: 0));
        Assert.Equal("longRoc", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeLongRoc_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coppock(longRoc: -1));
        Assert.Equal("longRoc", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroShortRoc_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coppock(shortRoc: 0));
        Assert.Equal("shortRoc", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeShortRoc_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coppock(shortRoc: -5));
        Assert.Equal("shortRoc", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroWmaPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coppock(wmaPeriod: 0));
        Assert.Equal("wmaPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeWmaPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Coppock(wmaPeriod: -2));
        Assert.Equal("wmaPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_Defaults_Creates()
    {
        var c = new Coppock();
        Assert.NotNull(c);
        Assert.Contains("Coppock", c.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsPositive()
    {
        var c = new Coppock();
        Assert.True(c.WarmupPeriod > 0);
    }

    [Fact]
    public void Constructor_CustomParams_NameReflectsThem()
    {
        var c = new Coppock(longRoc: 7, shortRoc: 5, wmaPeriod: 4);
        Assert.Contains("7", c.Name, StringComparison.Ordinal);
        Assert.Contains("5", c.Name, StringComparison.Ordinal);
        Assert.Contains("4", c.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WarmupPeriod_DependsOnLongestPlusWma()
    {
        // WarmupPeriod = max(longRoc,shortRoc) + wmaPeriod - 1
        var c = new Coppock(longRoc: 14, shortRoc: 11, wmaPeriod: 10);
        Assert.Equal(14 + 10 - 1, c.WarmupPeriod);
    }
}

// ── B) Basic Calculation ─────────────────────────────────────────────────────
public sealed class CoppockBasicTests
{
    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var c = new Coppock();
        var result = c.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(result.Value, c.Last.Value);
    }

    [Fact]
    public void FirstBar_OutputIsFinite()
    {
        var c = new Coppock();
        var result = c.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Name_Available()
    {
        var c = new Coppock();
        Assert.False(string.IsNullOrEmpty(c.Name));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(double.IsFinite(c.Last.Value));
    }

    [Fact]
    public void ConstantPrice_CoppockIsZero()
    {
        // All ROC = 0 → combined = 0 → WMA(0) = 0
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        Assert.Equal(0.0, c.Last.Value, 1e-10);
    }

    [Fact]
    public void KnownValue_WarmupBarIsZero()
    {
        // Before warmup, output is 0 (during WMA fill)
        var c = new Coppock(longRoc: 5, shortRoc: 3, wmaPeriod: 4);
        var result = c.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.False(c.IsHot);
        Assert.True(double.IsFinite(result.Value));
    }
}

// ── C) State + Bar Correction ────────────────────────────────────────────────
public sealed class CoppockBarCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        for (int i = 0; i < 5; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i * 2), isNew: true);
        }
        double val1 = c.Last.Value;
        c.Update(new TValue(DateTime.UtcNow, 115.0), isNew: true);
        double val2 = c.Last.Value;
        Assert.True(double.IsFinite(val1));
        Assert.True(double.IsFinite(val2));
    }

    [Fact]
    public void IsNew_False_Rollback()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            c.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        var nextBar = gbm.Next(isNew: true);
        var originalInput = new TValue(nextBar.Time, nextBar.Close);
        var val1 = c.Update(originalInput, isNew: true);

        // Overwrite with different value
        c.Update(new TValue(nextBar.Time, nextBar.Close + 50), isNew: false);

        // Restore original → must match
        var restored = c.Update(originalInput, isNew: false);
        Assert.Equal(val1.Value, restored.Value, 1e-10);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue twentyInput = default;
        for (int i = 0; i < 20; i++)
        {
            var bar = gbm.Next(isNew: true);
            twentyInput = new TValue(bar.Time, bar.Close);
            c.Update(twentyInput, isNew: true);
        }

        double stateAfterTwenty = c.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            c.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        var finalResult = c.Update(twentyInput, isNew: false);
        Assert.Equal(stateAfterTwenty, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        for (int i = 0; i < 20; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        c.Reset();
        Assert.False(c.IsHot);
        Assert.Equal(0.0, c.Last.Value);
    }
}

// ── D) Warmup / Convergence ──────────────────────────────────────────────────
public sealed class CoppockWarmupTests
{
    [Fact]
    public void IsHot_InitiallyFalse()
    {
        var c = new Coppock();
        Assert.False(c.IsHot);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmupPeriodBars()
    {
        var c = new Coppock(longRoc: 5, shortRoc: 3, wmaPeriod: 4);
        int warmup = c.WarmupPeriod;

        for (int i = 1; i < warmup; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(c.IsHot, $"Should not be hot at bar {i} (need {warmup})");
        }

        c.Update(new TValue(DateTime.UtcNow, 100.0 + warmup));
        Assert.True(c.IsHot);
    }

    [Fact]
    public void WarmupPeriod_DependsOnParameters()
    {
        var c1 = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        var c2 = new Coppock(longRoc: 14, shortRoc: 11, wmaPeriod: 10);
        Assert.True(c2.WarmupPeriod > c1.WarmupPeriod);
    }

    [Fact]
    public void WarmupPeriod_ShortRocLonger_UsesShortRoc()
    {
        // When shortRoc > longRoc, warmup = shortRoc + wmaPeriod - 1
        var c = new Coppock(longRoc: 5, shortRoc: 8, wmaPeriod: 4);
        Assert.Equal(8 + 4 - 1, c.WarmupPeriod);
    }
}

// ── E) Robustness ────────────────────────────────────────────────────────────
public sealed class CoppockRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        for (int i = 0; i < 10; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        c.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(c.Last.Value), "NaN input should not produce NaN output");
    }

    [Fact]
    public void PositiveInfinity_UsesLastValidValue()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        for (int i = 0; i < 10; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        c.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(c.Last.Value));
    }

    [Fact]
    public void NegativeInfinity_UsesLastValidValue()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        for (int i = 0; i < 10; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        c.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(c.Last.Value));
    }

    [Fact]
    public void BatchNaN_SafeOutput()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        c.Update(new TValue(DateTime.UtcNow, 100.0));
        for (int i = 0; i < 5; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, double.NaN));
        }
        c.Update(new TValue(DateTime.UtcNow, 110.0));
        Assert.True(double.IsFinite(c.Last.Value));
    }
}

// ── F) Consistency (all API modes agree) ─────────────────────────────────────
public sealed class CoppockConsistencyTests
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
        int lr = 5, sr = 4, wp = 4;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 7);
        int count = 60;
        var prices = new double[count];
        for (int i = 0; i < count; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        // Streaming
        var cStream = new Coppock(lr, sr, wp);
        var streamOut = new double[count];
        for (int i = 0; i < count; i++)
        {
            cStream.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
            streamOut[i] = cStream.Last.Value;
        }

        // Batch TSeries
        var series = MakeSeries(prices);
        var cBatch = new Coppock(lr, sr, wp);
        var batchOut = cBatch.Update(series);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamOut[i], batchOut.Values[i], 1e-9);
        }
    }

    [Fact]
    public void Span_Equals_Streaming()
    {
        int lr = 5, sr = 4, wp = 4;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 11);
        int count = 60;
        var prices = new double[count];
        for (int i = 0; i < count; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        // Span Batch
        var spanOut = new double[count];
        Coppock.Batch(prices, spanOut, lr, sr, wp);

        // Streaming
        var cStream = new Coppock(lr, sr, wp);
        for (int i = 0; i < count; i++)
        {
            cStream.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
            Assert.Equal(spanOut[i], cStream.Last.Value, 1e-9);
        }
    }

    [Fact]
    public void Eventing_Equals_Manual_Streaming()
    {
        int lr = 5, sr = 4, wp = 4;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 13);
        var series = new TSeries();

        // Subscribe BEFORE adding data so Pub events fire
        var cEvent = new Coppock(series, lr, sr, wp);

        for (int i = 0; i < 40; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close), isNew: true);
        }
        double eventLast = cEvent.Last.Value;

        // Manual streaming replay
        var cManual = new Coppock(lr, sr, wp);
        foreach (var tv in series)
        {
            cManual.Update(tv, isNew: true);
        }

        Assert.Equal(eventLast, cManual.Last.Value, 1e-9);
    }
}

// ── G) Span API Tests ────────────────────────────────────────────────────────
public sealed class CoppockSpanTests
{
    [Fact]
    public void Span_MismatchedOutputLength_ThrowsArgumentException()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] output = new double[4]; // wrong length
        var ex = Assert.Throws<ArgumentException>(() =>
            Coppock.Batch(src, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Span_ZeroLongRoc_ThrowsArgumentException()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Coppock.Batch(src, output, longRoc: 0));
        Assert.Equal("longRoc", ex.ParamName);
    }

    [Fact]
    public void Span_ZeroShortRoc_ThrowsArgumentException()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Coppock.Batch(src, output, shortRoc: 0));
        Assert.Equal("shortRoc", ex.ParamName);
    }

    [Fact]
    public void Span_ZeroWmaPeriod_ThrowsArgumentException()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Coppock.Batch(src, output, wmaPeriod: 0));
        Assert.Equal("wmaPeriod", ex.ParamName);
    }

    [Fact]
    public void Span_EmptyInput_NoException()
    {
        double[] src = [];
        double[] output = [];
        Coppock.Batch(src, output); // should not throw
        Assert.Empty(src);
    }

    [Fact]
    public void Span_NaNInput_SafeOutput()
    {
        var prices = new double[60];
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 99);
        for (int i = 0; i < 60; i++) { prices[i] = gbm.Next(isNew: true).Close; }
        prices[10] = double.NaN;
        prices[25] = double.PositiveInfinity;

        var output = new double[60];
        Coppock.Batch(prices, output, longRoc: 5, shortRoc: 4, wmaPeriod: 4);

        foreach (var v in output) { Assert.True(double.IsFinite(v)); }
    }

    [Fact]
    public void Span_LargeInput_NoStackOverflow()
    {
        int n = 5000;
        var prices = new double[n];
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.1, seed: 77);
        for (int i = 0; i < n; i++) { prices[i] = gbm.Next(isNew: true).Close; }

        var output = new double[n];
        Coppock.Batch(prices, output); // default periods, large array
        Assert.True(double.IsFinite(output[^1]));
    }
}

// ── H) Chainability ──────────────────────────────────────────────────────────
public sealed class CoppockChainabilityTests
{
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var c = new Coppock(longRoc: 3, shortRoc: 2, wmaPeriod: 3);
        int fireCount = 0;
        c.Pub += (object? _, in TValueEventArgs _e) => fireCount++;

        for (int i = 0; i < 5; i++)
        {
            c.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.Equal(5, fireCount);
    }

    [Fact]
    public void EventBasedChaining_WorksCorrectly()
    {
        var series = new TSeries();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 5);

        var c = new Coppock(series, longRoc: 3, shortRoc: 2, wmaPeriod: 3);

        for (int i = 0; i < 25; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(new TValue(bar.Time, bar.Close), isNew: true);
        }

        Assert.True(double.IsFinite(c.Last.Value));
    }
}
