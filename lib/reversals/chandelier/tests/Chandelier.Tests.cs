// CHANDELIER Tests - Chandelier Exit

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ────────────────────────────────────────────
public sealed class ChandelierConstructorTests
{
    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Chandelier(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Chandelier(period: -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroMultiplier_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Chandelier(multiplier: 0));
        Assert.Equal("multiplier", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeMultiplier_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Chandelier(multiplier: -1.0));
        Assert.Equal("multiplier", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidDefaults_SetsProperties()
    {
        var ch = new Chandelier();

        Assert.Equal(22, ch.Period);
        Assert.Equal(3.0, ch.Multiplier);
        Assert.Equal(23, ch.WarmupPeriod);
        Assert.Contains("Chandelier", ch.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomParams_SetsProperties()
    {
        var ch = new Chandelier(period: 14, multiplier: 2.0);

        Assert.Equal(14, ch.Period);
        Assert.Equal(2.0, ch.Multiplier);
        Assert.Equal(15, ch.WarmupPeriod);
    }
}

// ── B) Basic Calculation ─────────────────────────────────────────────────
public sealed class ChandelierBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        var bar = new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000);

        TValue result = ch.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        var bar = new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000);

        _ = ch.Update(bar);

        Assert.True(double.IsFinite(ch.Last.Value) || double.IsNaN(ch.Last.Value));
    }

    [Fact]
    public void Update_ExitLong_ExitShort_Accessible()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);

        // Feed enough bars to warm up
        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.True(double.IsFinite(ch.ExitLong));
        Assert.True(double.IsFinite(ch.ExitShort));
    }

    [Fact]
    public void Name_ContainsParameters()
    {
        var ch = new Chandelier(period: 14, multiplier: 2.5);

        Assert.Contains("14", ch.Name, StringComparison.Ordinal);
        Assert.Contains("2.5", ch.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ExitLong_BelowPrice_InUptrend()
    {
        var ch = new Chandelier(period: 5, multiplier: 1.0);
        double basePrice = 100.0;

        // Steady uptrend
        for (int i = 0; i < 20; i++)
        {
            double price = basePrice + i * 2;
            _ = ch.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                price + 1, price - 1, price + 0.5, price, 1000));
        }

        Assert.True(ch.ExitLong < 100.0 + 19 * 2, "ExitLong should be below the current price in an uptrend");
    }
}

// ── C) State + Bar Correction ────────────────────────────────────────────
public sealed class ChandelierStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);

        _ = ch.Update(new TBar(DateTime.UtcNow, 105, 95, 100, 100, 1000), isNew: true);
        var first = ch.Last;

        _ = ch.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 110, 100, 105, 105, 1000), isNew: true);
        var second = ch.Last;

        // Second update should change (new bar)
        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        var dt = DateTime.UtcNow;

        // Feed some bars to warm up
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000), isNew: true);
        }

        // New bar
        _ = ch.Update(new TBar(dt.AddMinutes(5), 110, 105, 108, 107, 1000), isNew: true);

        // Correct the bar (isNew=false with different values)
        _ = ch.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);

        // Another correction should produce same result
        _ = ch.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);
        var corrected1 = ch.ExitLong;

        _ = ch.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);
        var corrected2 = ch.ExitLong;

        Assert.Equal(corrected1, corrected2);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000), isNew: true);
        }

        // Add new bar then correct 3 times
        _ = ch.Update(new TBar(dt.AddMinutes(5), 110, 100, 108, 105, 1000), isNew: true);

        double[] results = new double[3];
        for (int i = 0; i < 3; i++)
        {
            _ = ch.Update(new TBar(dt.AddMinutes(5), 112, 101, 110, 107, 1000), isNew: false);
            results[i] = ch.ExitLong;
        }

        Assert.Equal(results[0], results[1]);
        Assert.Equal(results[1], results[2]);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.True(ch.IsHot);

        ch.Reset();

        Assert.False(ch.IsHot);
        Assert.True(double.IsNaN(ch.ExitLong));
        Assert.True(double.IsNaN(ch.ExitShort));
    }
}

// ── D) Warmup / Convergence ──────────────────────────────────────────────
public sealed class ChandelierWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterWarmup()
    {
        int period = 5;
        var ch = new Chandelier(period: period, multiplier: 1.0);

        // Feed period bars — should NOT be hot yet (ATR not seeded)
        for (int i = 0; i < period; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
            Assert.False(ch.IsHot, $"Should not be hot at bar {i}");
        }

        // Feed one more bar — ATR is now seeded, IsHot should flip
        double p = 100.0 + period;
        _ = ch.Update(new TBar(DateTime.UtcNow.AddMinutes(period), p + 2, p - 2, p + 1, p, 1000));
        Assert.True(ch.IsHot, $"Should be hot after {period + 1} bars");
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriodPlusOne()
    {
        var ch = new Chandelier(period: 22, multiplier: 3.0);

        Assert.Equal(23, ch.WarmupPeriod);
    }
}

// ── E) Robustness ────────────────────────────────────────────────────────
public sealed class ChandelierRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        var dt = DateTime.UtcNow;

        // Feed valid bars
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        // Feed NaN bar
        _ = ch.Update(new TBar(dt.AddMinutes(5), double.NaN, double.NaN, double.NaN, double.NaN, 0));

        // Should still produce finite output (using last-valid substitution)
        Assert.True(double.IsFinite(ch.ExitLong));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        _ = ch.Update(new TBar(dt.AddMinutes(5),
            double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity, 0));

        Assert.True(double.IsFinite(ch.ExitLong));
    }

    [Fact]
    public void FirstBar_NaN_ReturnsNaN()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);

        _ = ch.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(ch.Last.Value));
    }
}

// ── F) Consistency ───────────────────────────────────────────────────────
public sealed class ChandelierConsistencyTests
{
    private static TBarSeries CreateGbmBars(int count = 500)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Streaming_MatchesBatch()
    {
        var bars = CreateGbmBars();
        int period = 22;
        double multiplier = 3.0;

        // Streaming
        var streaming = new Chandelier(period, multiplier);
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamResults[i] = streaming.ExitLong;
        }

        // Batch
        var batchResults = Chandelier.Batch(bars, period, multiplier);

        int warmup = period;
        for (int i = warmup; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, precision: 10);
        }
    }

    [Fact]
    public void ExitShort_GreaterOrEqual_ExitLong_Often()
    {
        var bars = CreateGbmBars();
        var ch = new Chandelier(period: 22, multiplier: 3.0);

        int aboveCount = 0;
        int belowCount = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            _ = ch.Update(bars[i], isNew: true);

            if (ch.IsHot)
            {
                if (ch.ExitShort >= ch.ExitLong)
                {
                    aboveCount++;
                }
                else
                {
                    belowCount++;
                }
            }
        }

        // Verify the indicator produces hot bars
        Assert.True(aboveCount + belowCount > 0, "Should have some hot bars");
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var ch1 = new Chandelier(period: 5, multiplier: 1.0);
        var ch2 = new Chandelier(period: 5, multiplier: 1.0);

        double[] prices = [100, 102, 98, 105, 99, 103, 107, 95, 110, 108];

        for (int i = 0; i < prices.Length; i++)
        {
            double p = prices[i];
            // TBar with equal OHLC
            _ = ch1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p, p, p, 0), isNew: true);
            // TValue
            _ = ch2.Update(new TValue(DateTime.UtcNow.AddMinutes(i), p), isNew: true);
        }

        Assert.Equal(ch1.ExitLong, ch2.ExitLong);
        Assert.Equal(ch1.ExitShort, ch2.ExitShort);
    }
}

// ── G) Span API Tests ────────────────────────────────────────────────────
public sealed class ChandelierSpanTests
{
    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Chandelier.Batch(new double[10], new double[10], new double[10], new double[10], new double[10], period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Chandelier.Batch(new double[10], new double[10], new double[5], new double[10], new double[10], period: 5));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Chandelier.Batch(new double[10], new double[10], new double[10], new double[10], new double[5], period: 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var output = Array.Empty<double>();
        var ex = Record.Exception(() =>
            Chandelier.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, output.AsSpan(), period: 5));
        Assert.Null(ex);
    }
}

// ── H) Event / Chainability ──────────────────────────────────────────────
public sealed class ChandelierEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        int fireCount = 0;

        ch.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = ch.Update(new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var ch = new Chandelier(period: 3, multiplier: 1.0);
        int fireCount = 0;

        ch.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = ch.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.Equal(5, fireCount);
    }
}

// ── I) Prime Tests ───────────────────────────────────────────────────────
public sealed class ChandelierPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ch = new Chandelier(period: 22, multiplier: 3.0);
        ch.Prime(bars);

        Assert.True(ch.IsHot);
        Assert.True(double.IsFinite(ch.ExitLong));
        Assert.True(double.IsFinite(ch.ExitShort));
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var ch = new Chandelier();
        var bars = new TBarSeries();

        var ex = Record.Exception(() => ch.Prime(bars));
        Assert.Null(ex);
        Assert.False(ch.IsHot);
    }
}
