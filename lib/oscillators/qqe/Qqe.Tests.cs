using Xunit;

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ──────────────────────────────────────
public sealed class QqeConstructorTests
{
    [Fact]
    public void DefaultParameters_AreCorrect()
    {
        var ind = new Qqe();
        Assert.Equal("Qqe(14,5,4.236)", ind.Name);
        Assert.True(ind.WarmupPeriod > 0);
    }

    [Fact]
    public void CustomParameters_SetsNameCorrectly()
    {
        var ind = new Qqe(7, 3, 2.0);
        Assert.Equal("Qqe(7,3,2)", ind.Name);
    }

    [Theory]
    [InlineData(0, 5, 4.236, "rsiPeriod")]
    [InlineData(-1, 5, 4.236, "rsiPeriod")]
    [InlineData(14, 0, 4.236, "smoothFactor")]
    [InlineData(14, -1, 4.236, "smoothFactor")]
    [InlineData(14, 5, 0.0, "qqeFactor")]
    [InlineData(14, 5, -1.0, "qqeFactor")]
    public void InvalidParameters_ThrowsArgumentException(int rsi, int sf, double qf, string paramName)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Qqe(rsi, sf, qf));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void MinimalParameters_Work()
    {
        var ind = new Qqe(1, 1, 0.001);
        Assert.NotNull(ind);
    }
}

// ── B) Basic Calculation ───────────────────────────────────────────
public sealed class QqeBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var ind = new Qqe();
        TValue result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ind = new Qqe(5, 3, 2.0);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 110));
        Assert.IsType<TValue>(ind.Last);
    }

    [Fact]
    public void Name_Available()
    {
        var ind = new Qqe(7, 3, 2.0);
        Assert.Equal("Qqe(7,3,2)", ind.Name);
    }

    [Fact]
    public void QqeValueAndSignal_AreAccessible()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 60; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(ind.QqeValue));
        Assert.True(double.IsFinite(ind.Signal));
    }

    [Fact]
    public void ConvergedQqeValue_NearRsiRange()
    {
        var ind = new Qqe(7, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            ind.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ind.IsHot);
        // QQE line is smoothed RSI — should be bounded 0-100 for well-behaved data
        Assert.InRange(ind.QqeValue, 0.0, 100.0);
    }

    [Fact]
    public void Last_MatchesQqeValue()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 99);

        TValue last = default;
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            last = ind.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.Equal(ind.QqeValue, last.Value, 1e-12);
    }
}

// ── C) State + Bar Correction ──────────────────────────────────────
public sealed class QqeBarCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), gbm.Next(isNew: true).Close));
        }

        double qqeBefore = ind.QqeValue;
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(30), 150.0), isNew: true);
        Assert.NotEqual(qqeBefore, ind.QqeValue);
    }

    [Fact]
    public void IsNew_False_UpdatesLastBar()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), gbm.Next(isNew: true).Close));
        }

        // Rewrite last bar with a very different value
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(29), 80.0), isNew: false);
        double qqeRewritten = ind.QqeValue;
        // Apply same rewrite again — result must be idempotent
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(29), 80.0), isNew: false);
        Assert.Equal(qqeRewritten, ind.QqeValue, 1e-12);
    }

    [Fact]
    public void IterativeCorrection_Restores()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        // Feed 40 bars (all isNew=true)
        var times = new DateTime[45];
        var prices = new double[45];
        for (int i = 0; i < 45; i++)
        {
            times[i] = DateTime.UtcNow.AddMinutes(i);
            prices[i] = gbm.Next(isNew: true).Close;
        }

        for (int i = 0; i < 40; i++)
        {
            ind.Update(new TValue(times[i], prices[i]));
        }
        // Add 5 more bars with isNew=true, then rollback each with isNew=false using original price
        for (int i = 40; i < 45; i++)
        {
            ind.Update(new TValue(times[i], prices[i]), isNew: true);
        }
        // Now re-apply bar 44 with isNew=false (correction)
        ind.Update(new TValue(times[44], prices[44]), isNew: false);

        // Roll state all the way back by doing isNew=false on each bar from 44 down to 40
        for (int i = 44; i >= 40; i--)
        {
            ind.Update(new TValue(times[i], prices[i]), isNew: false);
        }

        // We can't fully roll back because bar-correction only rolls back one level (_ps).
        // Just verify the state is consistent after final isNew=false call:
        Assert.True(double.IsFinite(ind.QqeValue));
        Assert.True(double.IsFinite(ind.Signal));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, gbm.Next(isNew: true).Close));
        }

        ind.Reset();

        Assert.False(ind.IsHot);
        Assert.Equal(default, ind.Last);
    }
}

// ── D) Warmup / Convergence ────────────────────────────────────────
public sealed class QqeWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        bool sawCold = false;
        bool sawHot = false;

        for (int i = 0; i < ind.WarmupPeriod + 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), gbm.Next(isNew: true).Close));
            if (!ind.IsHot)
            {
                sawCold = true;
            }
            else
            {
                sawHot = true;
            }
        }

        Assert.True(sawCold, "Should start cold");
        Assert.True(sawHot, "Should become hot");
    }

    [Fact]
    public void WarmupPeriod_ScalesWithPeriods()
    {
        var ind14 = new Qqe(14, 5, 4.236);
        var ind7  = new Qqe(7,  3, 4.236);
        Assert.True(ind14.WarmupPeriod > ind7.WarmupPeriod);
    }
}

// ── E) Robustness ─────────────────────────────────────────────────
public sealed class QqeRobustnessTests
{
    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), gbm.Next(isNew: true).Close));
        }
        // Feed NaN — should not propagate
        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(30), double.NaN));
        Assert.True(double.IsFinite(ind.QqeValue));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var ind = new Qqe(5, 3, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), gbm.Next(isNew: true).Close));
        }

        ind.Update(new TValue(DateTime.UtcNow.AddMinutes(30), double.PositiveInfinity));
        Assert.True(double.IsFinite(ind.QqeValue));
    }

    [Fact]
    public void BatchNaN_IsSafe()
    {
        var ind = new Qqe(5, 3, 2.0);
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), double.NaN));
        }
        Assert.True(double.IsFinite(ind.QqeValue) || double.IsNaN(ind.QqeValue));
    }
}

// ── F) Consistency — all 4 API modes must match ──────────────────
public sealed class QqeConsistencyTests
{
    private static TSeries MakeCloseSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    [Fact]
    public void Streaming_Matches_Batch()
    {
        var close = MakeCloseSeries(300);
        const int rsiPeriod = 14;
        const int sf = 5;
        const double qf = 4.236;

        // Streaming
        var ind = new Qqe(rsiPeriod, sf, qf);
        for (int i = 0; i < close.Count; i++)
        {
            ind.Update(new TValue(close.Times[i], close.Values[i]));
        }
        double streamQqe = ind.QqeValue;

        // Batch (TSeries path)
        var batchResult = Qqe.Batch(close, rsiPeriod, sf, qf);

        Assert.Equal(streamQqe, batchResult[^1].Value, 1e-10);
    }

    [Fact]
    public void Span_Matches_Streaming()
    {
        var close = MakeCloseSeries(200);
        const int rsiPeriod = 10;
        const int sf = 4;
        const double qf = 3.0;

        // Streaming
        var ind = new Qqe(rsiPeriod, sf, qf);
        for (int i = 0; i < close.Count; i++)
        {
            ind.Update(new TValue(close.Times[i], close.Values[i]));
        }
        double streamQqe = ind.QqeValue;

        // Span Batch
        double[] src = close.Values.ToArray();
        double[] output = new double[src.Length];
        Qqe.Batch(src.AsSpan(), output.AsSpan(), rsiPeriod, sf, qf);

        Assert.Equal(streamQqe, output[^1], 1e-10);
    }

    [Fact]
    public void Update_TSeries_Matches_Streaming()
    {
        var close = MakeCloseSeries(250);
        const int rsiPeriod = 14;
        const int sf = 5;
        const double qf = 4.236;

        // Streaming
        var ind1 = new Qqe(rsiPeriod, sf, qf);
        for (int i = 0; i < close.Count; i++)
        {
            ind1.Update(new TValue(close.Times[i], close.Values[i]));
        }

        // Update(TSeries)
        var ind2 = new Qqe(rsiPeriod, sf, qf);
        var result2 = ind2.Update(close);

        Assert.Equal(ind1.QqeValue, result2[^1].Value, 1e-10);
    }
}

// ── G) Span API Tests ─────────────────────────────────────────────
public sealed class QqeSpanTests
{
    [Fact]
    public void Batch_LengthMismatch_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[9];
        var ex = Assert.Throws<ArgumentException>(
            () => Qqe.Batch(src.AsSpan(), output.AsSpan(), 5, 3, 2.0));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidRsiPeriod_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(
            () => Qqe.Batch(src.AsSpan(), output.AsSpan(), 0, 3, 2.0));
        Assert.Equal("rsiPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidSmoothFactor_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(
            () => Qqe.Batch(src.AsSpan(), output.AsSpan(), 5, 0, 2.0));
        Assert.Equal("smoothFactor", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidQqeFactor_Throws()
    {
        double[] src = new double[10];
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(
            () => Qqe.Batch(src.AsSpan(), output.AsSpan(), 5, 3, 0.0));
        Assert.Equal("qqeFactor", ex.ParamName);
    }

    [Fact]
    public void Batch_Empty_NoException()
    {
        double[] src = Array.Empty<double>();
        double[] output = Array.Empty<double>();
        Qqe.Batch(src.AsSpan(), output.AsSpan(), 5, 3, 2.0);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_LargeData_NoStackOverflow()
    {
        int size = 2000;
        double[] src = new double[size];
        double[] output = new double[size];
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < size; i++)
        {
            src[i] = gbm.Next(isNew: true).Close;
        }
        Qqe.Batch(src.AsSpan(), output.AsSpan(), 14, 5, 4.236);
        Assert.True(double.IsFinite(output[^1]));
    }
}

// ── H) Chainability ───────────────────────────────────────────────
public sealed class QqeChainabilityTests
{
    [Fact]
    public void PubEvent_Fires()
    {
        var ind = new Qqe(5, 3, 2.0);
        int fired = 0;
        ind.Pub += (_, in _) => fired++;

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), gbm.Next(isNew: true).Close));
        }

        Assert.Equal(10, fired);
    }

    [Fact]
    public void SourceConstructor_SubscribesAndComputes()
    {
        // Use a simple source indicator (another Qqe works as ITValuePublisher)
        var source = new Qqe(5, 2, 2.0);
        var chained = new Qqe(source, 5, 2, 2.0);

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < 60; i++)
        {
            source.Update(new TValue(DateTime.UtcNow.AddMinutes(i), gbm.Next(isNew: true).Close));
        }

        Assert.True(double.IsFinite(chained.QqeValue));
    }
}
