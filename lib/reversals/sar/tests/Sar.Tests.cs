// SAR Tests - Parabolic Stop And Reverse

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ────────────────────────────────────────────
public sealed class SarConstructorTests
{
    [Fact]
    public void Constructor_ZeroAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sar(afStart: 0));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sar(afStart: -0.01));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAfIncrement_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sar(afIncrement: 0));
        Assert.Equal("afIncrement", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeAfIncrement_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sar(afIncrement: -0.01));
        Assert.Equal("afIncrement", ex.ParamName);
    }

    [Fact]
    public void Constructor_AfMaxEqualAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sar(afStart: 0.02, afMax: 0.02));
        Assert.Equal("afMax", ex.ParamName);
    }

    [Fact]
    public void Constructor_AfMaxLessThanAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sar(afStart: 0.10, afMax: 0.05));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidDefaults_SetsProperties()
    {
        var sar = new Sar();

        Assert.Equal(0.02, sar.AfStart);
        Assert.Equal(0.02, sar.AfIncrement);
        Assert.Equal(0.20, sar.AfMax);
        Assert.Equal(1, sar.WarmupPeriod);
        Assert.Contains("Sar", sar.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomParams_SetsProperties()
    {
        var sar = new Sar(afStart: 0.01, afIncrement: 0.01, afMax: 0.10);

        Assert.Equal(0.01, sar.AfStart);
        Assert.Equal(0.01, sar.AfIncrement);
        Assert.Equal(0.10, sar.AfMax);
    }
}

// ── B) Basic Calculation ─────────────────────────────────────────────────
public sealed class SarBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var sar = new Sar();
        var bar = new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000);

        TValue result = sar.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var sar = new Sar();
        var bar = new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000);

        _ = sar.Update(bar);

        Assert.True(double.IsFinite(sar.Last.Value) || double.IsNaN(sar.Last.Value));
    }

    [Fact]
    public void Update_Sar_IsAccessible()
    {
        var sar = new Sar();

        // Feed enough bars
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sar.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.True(double.IsFinite(sar.SarValue));
    }

    [Fact]
    public void Name_ContainsParameters()
    {
        var sar = new Sar(afStart: 0.01, afIncrement: 0.02, afMax: 0.10);

        Assert.Contains("0.01", sar.Name, StringComparison.Ordinal);
        Assert.Contains("0.10", sar.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstBar_Uptrend_SarEqualsLow()
    {
        var sar = new Sar();
        // Close(105) > Open(95) → long mode → SAR = low(90)
        _ = sar.Update(new TBar(DateTime.UtcNow, 95, 110, 90, 105, 1000));

        Assert.Equal(90.0, sar.SarValue);
        Assert.True(sar.IsLong);
    }

    [Fact]
    public void FirstBar_Downtrend_SarEqualsHigh()
    {
        var sar = new Sar();
        // Close(90) < Open(105) → short mode → SAR = high(110)
        _ = sar.Update(new TBar(DateTime.UtcNow, 105, 110, 85, 90, 1000));

        Assert.Equal(110.0, sar.SarValue);
        Assert.False(sar.IsLong);
    }

    [Fact]
    public void Sar_BelowPrice_InUptrend()
    {
        var sar = new Sar();

        // Steady uptrend - SAR should trail below
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + (i * 2);
            _ = sar.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                price + 1, price - 1, price + 0.5, price, 1000));
        }

        double lastClose = 100.0 + (19 * 2);
        Assert.True(sar.SarValue < lastClose, "SAR should be below price in uptrend");
        Assert.True(sar.IsLong, "Should be in long mode during uptrend");
    }

    [Fact]
    public void Sar_AbovePrice_InDowntrend()
    {
        var sar = new Sar();

        // Steady downtrend - SAR should trail above
        for (int i = 0; i < 20; i++)
        {
            double price = 200.0 - (i * 2);
            _ = sar.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                price + 1, price - 1, price + 0.5, price, 1000));
        }

        double lastClose = 200.0 - (19 * 2);
        Assert.True(sar.SarValue > lastClose, "SAR should be above price in downtrend");
        Assert.False(sar.IsLong, "Should be in short mode during downtrend");
    }

    [Fact]
    public void IsHot_TrueAfterFirstBar()
    {
        var sar = new Sar();

        Assert.False(sar.IsHot);

        _ = sar.Update(new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000));

        Assert.True(sar.IsHot);
    }
}

// ── C) State + Bar Correction ────────────────────────────────────────────
public sealed class SarStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var sar = new Sar();

        _ = sar.Update(new TBar(DateTime.UtcNow, 105, 95, 100, 100, 1000), isNew: true);
        var first = sar.Last;

        _ = sar.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 110, 100, 105, 105, 1000), isNew: true);
        var second = sar.Last;

        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var sar = new Sar();
        var dt = DateTime.UtcNow;

        // Feed some bars to warm up
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000), isNew: true);
        }

        // New bar
        _ = sar.Update(new TBar(dt.AddMinutes(5), 110, 105, 108, 107, 1000), isNew: true);

        // Correct the bar (isNew=false with different values)
        _ = sar.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);

        // Another correction should produce same result
        _ = sar.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);
        var corrected1 = sar.SarValue;

        _ = sar.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);
        var corrected2 = sar.SarValue;

        Assert.Equal(corrected1, corrected2);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var sar = new Sar();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000), isNew: true);
        }

        // Add new bar then correct 3 times
        _ = sar.Update(new TBar(dt.AddMinutes(5), 110, 100, 108, 105, 1000), isNew: true);

        double[] results = new double[3];
        for (int i = 0; i < 3; i++)
        {
            _ = sar.Update(new TBar(dt.AddMinutes(5), 112, 101, 110, 107, 1000), isNew: false);
            results[i] = sar.SarValue;
        }

        Assert.Equal(results[0], results[1]);
        Assert.Equal(results[1], results[2]);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var sar = new Sar();

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = sar.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.True(sar.IsHot);

        sar.Reset();

        Assert.False(sar.IsHot);
        Assert.True(double.IsNaN(sar.SarValue));
    }
}

// ── D) Warmup / Convergence ──────────────────────────────────────────────
public sealed class SarWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterFirstBar()
    {
        var sar = new Sar();

        Assert.False(sar.IsHot);

        _ = sar.Update(new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000));

        Assert.True(sar.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsOne()
    {
        var sar = new Sar();

        Assert.Equal(1, sar.WarmupPeriod);
    }
}

// ── E) Robustness ────────────────────────────────────────────────────────
public sealed class SarRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var sar = new Sar();
        var dt = DateTime.UtcNow;

        // Feed valid bars
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        // Feed NaN bar
        _ = sar.Update(new TBar(dt.AddMinutes(5), double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsFinite(sar.SarValue));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var sar = new Sar();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        _ = sar.Update(new TBar(dt.AddMinutes(5),
            double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity, 0));

        Assert.True(double.IsFinite(sar.SarValue));
    }

    [Fact]
    public void FirstBar_NaN_ReturnsNaN()
    {
        var sar = new Sar();

        _ = sar.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(sar.Last.Value));
    }
}

// ── F) Consistency ───────────────────────────────────────────────────────
public sealed class SarConsistencyTests
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

        // Streaming
        var streaming = new Sar();
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamResults[i] = streaming.SarValue;
        }

        // Batch
        var batchResults = Sar.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, precision: 10);
        }
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var ch1 = new Sar();
        var ch2 = new Sar();

        double[] prices = [100, 102, 98, 105, 99, 103, 107, 95, 110, 108];

        for (int i = 0; i < prices.Length; i++)
        {
            double p = prices[i];
            // TBar with equal OHLC
            _ = ch1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p, p, p, 0), isNew: true);
            // TValue
            _ = ch2.Update(new TValue(DateTime.UtcNow.AddMinutes(i), p), isNew: true);
        }

        Assert.Equal(ch1.SarValue, ch2.SarValue);
    }

    [Fact]
    public void Reversal_DetectedOnPriceCrossover()
    {
        var sar = new Sar();
        var dt = DateTime.UtcNow;

        // Start in uptrend
        _ = sar.Update(new TBar(dt, 100, 90, 95, 105, 1000), isNew: true);
        Assert.True(sar.IsLong);

        // Continue uptrend
        for (int i = 1; i <= 5; i++)
        {
            double price = 105 + (i * 2);
            _ = sar.Update(new TBar(dt.AddMinutes(i),
                price + 1, price - 1, price + 0.5, price, 1000), isNew: true);
        }
        Assert.True(sar.IsLong);

        // Sharp reversal — price drops below SAR
        double sarBeforeReversal = sar.SarValue;
        _ = sar.Update(new TBar(dt.AddMinutes(10),
            sarBeforeReversal - 5, sarBeforeReversal - 20,
            sarBeforeReversal - 18, sarBeforeReversal - 15, 1000), isNew: true);

        Assert.False(sar.IsLong, "Should reverse to short after price crosses below SAR");
    }

    [Fact]
    public void Update_TSeries_MatchesStreaming()
    {
        var bars = CreateGbmBars(100);

        // Streaming
        var streaming = new Sar();
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
        }
        double streamLast = streaming.SarValue;

        // TSeries batch
        var batch = new Sar();
        _ = batch.Update(bars);

        Assert.Equal(streamLast, batch.SarValue, precision: 10);
    }
}

// ── G) Span API Tests ────────────────────────────────────────────────────
public sealed class SarSpanTests
{
    [Fact]
    public void Batch_Span_InvalidAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Sar.Batch(new double[10], new double[10], new double[10], new double[10], new double[10], afStart: 0));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Sar.Batch(new double[10], new double[10], new double[5], new double[10], new double[10]));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Sar.Batch(new double[10], new double[10], new double[10], new double[10], new double[5]));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var output = Array.Empty<double>();
        var ex = Record.Exception(() =>
            Sar.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, output.AsSpan()));
        Assert.Null(ex);
    }
}

// ── H) Event / Chainability ──────────────────────────────────────────────
public sealed class SarEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var sar = new Sar();
        int fireCount = 0;

        sar.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = sar.Update(new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var sar = new Sar();
        int fireCount = 0;

        sar.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = sar.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.Equal(5, fireCount);
    }
}

// ── I) Prime Tests ───────────────────────────────────────────────────────
public sealed class SarPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var sar = new Sar();
        sar.Prime(bars);

        Assert.True(sar.IsHot);
        Assert.True(double.IsFinite(sar.SarValue));
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var sar = new Sar();
        var bars = new TBarSeries();

        var ex = Record.Exception(() => sar.Prime(bars));
        Assert.Null(ex);
        Assert.False(sar.IsHot);
    }
}
