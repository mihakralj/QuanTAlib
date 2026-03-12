// PSAR Tests - Parabolic Stop And Reverse

namespace QuanTAlib.Tests;

// ── A) Constructor Validation ────────────────────────────────────────────
public sealed class PsarConstructorTests
{
    [Fact]
    public void Constructor_ZeroAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Psar(afStart: 0));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Psar(afStart: -0.01));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroAfIncrement_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Psar(afIncrement: 0));
        Assert.Equal("afIncrement", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeAfIncrement_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Psar(afIncrement: -0.01));
        Assert.Equal("afIncrement", ex.ParamName);
    }

    [Fact]
    public void Constructor_AfMaxEqualAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Psar(afStart: 0.02, afMax: 0.02));
        Assert.Equal("afMax", ex.ParamName);
    }

    [Fact]
    public void Constructor_AfMaxLessThanAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Psar(afStart: 0.10, afMax: 0.05));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidDefaults_SetsProperties()
    {
        var psar = new Psar();

        Assert.Equal(0.02, psar.AfStart);
        Assert.Equal(0.02, psar.AfIncrement);
        Assert.Equal(0.20, psar.AfMax);
        Assert.Equal(1, psar.WarmupPeriod);
        Assert.Contains("Psar", psar.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomParams_SetsProperties()
    {
        var psar = new Psar(afStart: 0.01, afIncrement: 0.01, afMax: 0.10);

        Assert.Equal(0.01, psar.AfStart);
        Assert.Equal(0.01, psar.AfIncrement);
        Assert.Equal(0.10, psar.AfMax);
    }
}

// ── B) Basic Calculation ─────────────────────────────────────────────────
public sealed class PsarBasicTests
{
    [Fact]
    public void Update_ReturnsTValue()
    {
        var psar = new Psar();
        var bar = new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000);

        TValue result = psar.Update(bar);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var psar = new Psar();
        var bar = new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000);

        _ = psar.Update(bar);

        Assert.True(double.IsFinite(psar.Last.Value) || double.IsNaN(psar.Last.Value));
    }

    [Fact]
    public void Update_Sar_IsAccessible()
    {
        var psar = new Psar();

        // Feed enough bars
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = psar.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.True(double.IsFinite(psar.Sar));
    }

    [Fact]
    public void Name_ContainsParameters()
    {
        var psar = new Psar(afStart: 0.01, afIncrement: 0.02, afMax: 0.10);

        Assert.Contains("0.01", psar.Name, StringComparison.Ordinal);
        Assert.Contains("0.10", psar.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstBar_Uptrend_SarEqualsLow()
    {
        var psar = new Psar();
        // Close(105) > Open(95) → long mode → SAR = low(90)
        _ = psar.Update(new TBar(DateTime.UtcNow, 95, 110, 90, 105, 1000));

        Assert.Equal(90.0, psar.Sar);
        Assert.True(psar.IsLong);
    }

    [Fact]
    public void FirstBar_Downtrend_SarEqualsHigh()
    {
        var psar = new Psar();
        // Close(90) < Open(105) → short mode → SAR = high(110)
        _ = psar.Update(new TBar(DateTime.UtcNow, 105, 110, 85, 90, 1000));

        Assert.Equal(110.0, psar.Sar);
        Assert.False(psar.IsLong);
    }

    [Fact]
    public void Sar_BelowPrice_InUptrend()
    {
        var psar = new Psar();

        // Steady uptrend - SAR should trail below
        for (int i = 0; i < 20; i++)
        {
            double price = 100.0 + (i * 2);
            _ = psar.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                price + 1, price - 1, price + 0.5, price, 1000));
        }

        double lastClose = 100.0 + (19 * 2);
        Assert.True(psar.Sar < lastClose, "SAR should be below price in uptrend");
        Assert.True(psar.IsLong, "Should be in long mode during uptrend");
    }

    [Fact]
    public void Sar_AbovePrice_InDowntrend()
    {
        var psar = new Psar();

        // Steady downtrend - SAR should trail above
        for (int i = 0; i < 20; i++)
        {
            double price = 200.0 - (i * 2);
            _ = psar.Update(new TBar(DateTime.UtcNow.AddMinutes(i),
                price + 1, price - 1, price + 0.5, price, 1000));
        }

        double lastClose = 200.0 - (19 * 2);
        Assert.True(psar.Sar > lastClose, "SAR should be above price in downtrend");
        Assert.False(psar.IsLong, "Should be in short mode during downtrend");
    }

    [Fact]
    public void IsHot_TrueAfterFirstBar()
    {
        var psar = new Psar();

        Assert.False(psar.IsHot);

        _ = psar.Update(new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000));

        Assert.True(psar.IsHot);
    }
}

// ── C) State + Bar Correction ────────────────────────────────────────────
public sealed class PsarStateCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var psar = new Psar();

        _ = psar.Update(new TBar(DateTime.UtcNow, 105, 95, 100, 100, 1000), isNew: true);
        var first = psar.Last;

        _ = psar.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 110, 100, 105, 105, 1000), isNew: true);
        var second = psar.Last;

        Assert.NotEqual(first.Time, second.Time);
    }

    [Fact]
    public void IsNew_False_CorrectionRestoresState()
    {
        var psar = new Psar();
        var dt = DateTime.UtcNow;

        // Feed some bars to warm up
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = psar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000), isNew: true);
        }

        // New bar
        _ = psar.Update(new TBar(dt.AddMinutes(5), 110, 105, 108, 107, 1000), isNew: true);

        // Correct the bar (isNew=false with different values)
        _ = psar.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);

        // Another correction should produce same result
        _ = psar.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);
        var corrected1 = psar.Sar;

        _ = psar.Update(new TBar(dt.AddMinutes(5), 111, 104, 109, 108, 1000), isNew: false);
        var corrected2 = psar.Sar;

        Assert.Equal(corrected1, corrected2);
    }

    [Fact]
    public void IterativeCorrections_ProduceSameResult()
    {
        var psar = new Psar();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = psar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000), isNew: true);
        }

        // Add new bar then correct 3 times
        _ = psar.Update(new TBar(dt.AddMinutes(5), 110, 100, 108, 105, 1000), isNew: true);

        double[] results = new double[3];
        for (int i = 0; i < 3; i++)
        {
            _ = psar.Update(new TBar(dt.AddMinutes(5), 112, 101, 110, 107, 1000), isNew: false);
            results[i] = psar.Sar;
        }

        Assert.Equal(results[0], results[1]);
        Assert.Equal(results[1], results[2]);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var psar = new Psar();

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + i;
            _ = psar.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.True(psar.IsHot);

        psar.Reset();

        Assert.False(psar.IsHot);
        Assert.True(double.IsNaN(psar.Sar));
    }
}

// ── D) Warmup / Convergence ──────────────────────────────────────────────
public sealed class PsarWarmupTests
{
    [Fact]
    public void IsHot_FlipsAfterFirstBar()
    {
        var psar = new Psar();

        Assert.False(psar.IsHot);

        _ = psar.Update(new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000));

        Assert.True(psar.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsOne()
    {
        var psar = new Psar();

        Assert.Equal(1, psar.WarmupPeriod);
    }
}

// ── E) Robustness ────────────────────────────────────────────────────────
public sealed class PsarRobustnessTests
{
    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var psar = new Psar();
        var dt = DateTime.UtcNow;

        // Feed valid bars
        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = psar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        // Feed NaN bar
        _ = psar.Update(new TBar(dt.AddMinutes(5), double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsFinite(psar.Sar));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var psar = new Psar();
        var dt = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = psar.Update(new TBar(dt.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        _ = psar.Update(new TBar(dt.AddMinutes(5),
            double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity, 0));

        Assert.True(double.IsFinite(psar.Sar));
    }

    [Fact]
    public void FirstBar_NaN_ReturnsNaN()
    {
        var psar = new Psar();

        _ = psar.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 0));

        Assert.True(double.IsNaN(psar.Last.Value));
    }
}

// ── F) Consistency ───────────────────────────────────────────────────────
public sealed class PsarConsistencyTests
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
        var streaming = new Psar();
        var streamResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
            streamResults[i] = streaming.Sar;
        }

        // Batch
        var batchResults = Psar.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, precision: 10);
        }
    }

    [Fact]
    public void TValue_Update_MatchesTBar_Update()
    {
        var ch1 = new Psar();
        var ch2 = new Psar();

        double[] prices = [100, 102, 98, 105, 99, 103, 107, 95, 110, 108];

        for (int i = 0; i < prices.Length; i++)
        {
            double p = prices[i];
            // TBar with equal OHLC
            _ = ch1.Update(new TBar(DateTime.UtcNow.AddMinutes(i), p, p, p, p, 0), isNew: true);
            // TValue
            _ = ch2.Update(new TValue(DateTime.UtcNow.AddMinutes(i), p), isNew: true);
        }

        Assert.Equal(ch1.Sar, ch2.Sar);
    }

    [Fact]
    public void Reversal_DetectedOnPriceCrossover()
    {
        var psar = new Psar();
        var dt = DateTime.UtcNow;

        // Start in uptrend
        _ = psar.Update(new TBar(dt, 100, 90, 95, 105, 1000), isNew: true);
        Assert.True(psar.IsLong);

        // Continue uptrend
        for (int i = 1; i <= 5; i++)
        {
            double price = 105 + (i * 2);
            _ = psar.Update(new TBar(dt.AddMinutes(i),
                price + 1, price - 1, price + 0.5, price, 1000), isNew: true);
        }
        Assert.True(psar.IsLong);

        // Sharp reversal — price drops below SAR
        double sarBeforeReversal = psar.Sar;
        _ = psar.Update(new TBar(dt.AddMinutes(10),
            sarBeforeReversal - 5, sarBeforeReversal - 20,
            sarBeforeReversal - 18, sarBeforeReversal - 15, 1000), isNew: true);

        Assert.False(psar.IsLong, "Should reverse to short after price crosses below SAR");
    }

    [Fact]
    public void Update_TSeries_MatchesStreaming()
    {
        var bars = CreateGbmBars(100);

        // Streaming
        var streaming = new Psar();
        for (int i = 0; i < bars.Count; i++)
        {
            _ = streaming.Update(bars[i], isNew: true);
        }
        double streamLast = streaming.Sar;

        // TSeries batch
        var batch = new Psar();
        _ = batch.Update(bars);

        Assert.Equal(streamLast, batch.Sar, precision: 10);
    }
}

// ── G) Span API Tests ────────────────────────────────────────────────────
public sealed class PsarSpanTests
{
    [Fact]
    public void Batch_Span_InvalidAfStart_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Psar.Batch(new double[10], new double[10], new double[10], new double[10], new double[10], afStart: 0));
        Assert.Equal("afStart", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Psar.Batch(new double[10], new double[10], new double[5], new double[10], new double[10]));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Psar.Batch(new double[10], new double[10], new double[10], new double[10], new double[5]));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var output = Array.Empty<double>();
        var ex = Record.Exception(() =>
            Psar.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
                ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, output.AsSpan()));
        Assert.Null(ex);
    }
}

// ── H) Event / Chainability ──────────────────────────────────────────────
public sealed class PsarEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var psar = new Psar();
        int fireCount = 0;

        psar.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        _ = psar.Update(new TBar(DateTime.UtcNow, 100, 95, 98, 97, 1000));

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Pub_FiresOnEachUpdate()
    {
        var psar = new Psar();
        int fireCount = 0;

        psar.Pub += (object? _, in TValueEventArgs _e) => { fireCount++; };

        for (int i = 0; i < 5; i++)
        {
            double price = 100.0 + i;
            _ = psar.Update(new TBar(DateTime.UtcNow.AddMinutes(i), price + 2, price - 2, price + 1, price, 1000));
        }

        Assert.Equal(5, fireCount);
    }
}

// ── I) Prime Tests ───────────────────────────────────────────────────────
public sealed class PsarPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.20, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var psar = new Psar();
        psar.Prime(bars);

        Assert.True(psar.IsHot);
        Assert.True(double.IsFinite(psar.Sar));
    }

    [Fact]
    public void Prime_EmptySource_NoException()
    {
        var psar = new Psar();
        var bars = new TBarSeries();

        var ex = Record.Exception(() => psar.Prime(bars));
        Assert.Null(ex);
        Assert.False(psar.IsHot);
    }
}
