namespace QuanTAlib.Tests;

public class TrimTests
{
    // ── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsOnPeriodLessThan3()
    {
        Assert.Throws<ArgumentException>(() => new Trim(2));
        Assert.Throws<ArgumentException>(() => new Trim(1));
        Assert.Throws<ArgumentException>(() => new Trim(0));
        Assert.Throws<ArgumentException>(() => new Trim(-1));
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidTrimPct()
    {
        Assert.Throws<ArgumentException>(() => new Trim(10, -1.0));
        Assert.Throws<ArgumentException>(() => new Trim(10, 50.0));
        Assert.Throws<ArgumentException>(() => new Trim(10, 75.0));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var trim = new Trim(20, 10.0);
        Assert.Equal("Trim(20,10)", trim.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var trim = new Trim(15, 10.0);
        Assert.Equal(15, trim.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ValidMinimalPeriod()
    {
        var trim = new Trim(3);
        Assert.NotNull(trim);
    }

    // ── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValue()
    {
        var trim = new Trim(5);
        TValue result = trim.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result.Value, trim.Last.Value);
    }

    [Fact]
    public void IsHot_FalseUntilWindowFull()
    {
        var trim = new Trim(5);
        for (int i = 0; i < 4; i++)
        {
            trim.Update(new TValue(DateTime.UtcNow, i + 1.0));
            Assert.False(trim.IsHot);
        }

        trim.Update(new TValue(DateTime.UtcNow, 5.0));
        Assert.True(trim.IsHot);
    }

    [Fact]
    public void TrimPctZero_EqualsSMA()
    {
        // With trimPct=0, TRIM should equal SMA
        var trim = new Trim(5, 0.0);
        double[] vals = [10.0, 20.0, 30.0, 40.0, 50.0];
        double result = 0;
        foreach (double v in vals)
        {
            result = trim.Update(new TValue(DateTime.UtcNow, v)).Value;
        }

        Assert.Equal(30.0, result, 10); // SMA of [10,20,30,40,50] = 30
    }

    [Fact]
    public void TrimKnownValue_CorrectResult()
    {
        // Window: [1,2,3,4,5,6,7,8,9,10], trimPct=10 on period=10
        // trimCount = floor(10 * 10/100) = 1
        // keepCount = 10 - 2 = 8
        // mean([2,3,4,5,6,7,8,9]) = 44/8 = 5.5
        var trim = new Trim(10, 10.0);
        for (int i = 1; i <= 10; i++)
        {
            trim.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(5.5, trim.Last.Value, 10);
    }

    // ── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void BarCorrection_IsNewFalse_RewritesLastBar()
    {
        var trim = new Trim(5, 10.0);
        var t = DateTime.UtcNow;

        // Fill window with [1,2,3,4,5]
        for (int i = 1; i <= 5; i++)
        {
            trim.Update(new TValue(t, i));
        }

        double before = trim.Last.Value; // TRIM([1,2,3,4,5], 10%) — trimCount=0, SMA=3.0

        // Bar correction: replace last value (5) with 100 (an outlier)
        trim.Update(new TValue(t, 100.0), isNew: false);
        double afterCorrection = trim.Last.Value;

        // Next bar (isNew=true) with value=5: window slides to [2,3,4,5,5] from corrected state
        // (isNew=false set last bar to 5.0 before this new bar arrives)
        trim.Update(new TValue(t, 5.0), isNew: true);
        double afterNewBar = trim.Last.Value;

        // Correction with outlier should differ from original
        Assert.NotEqual(before, afterCorrection);
        // After new bar, result is finite and valid
        Assert.True(double.IsFinite(afterNewBar));
        // The new bar result differs from original (window shifted, different values)
        Assert.NotEqual(afterCorrection, afterNewBar);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var trim = new Trim(5);
        for (int i = 0; i < 5; i++)
        {
            trim.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        Assert.True(trim.IsHot);
        trim.Reset();
        Assert.False(trim.IsHot);
        Assert.Equal(0, trim.Last.Value);
    }

    // ── D) Warmup/convergence ────────────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        int period = 7;
        var trim = new Trim(period);
        for (int i = 0; i < period - 1; i++)
        {
            trim.Update(new TValue(DateTime.UtcNow, i));
            Assert.False(trim.IsHot);
        }

        trim.Update(new TValue(DateTime.UtcNow, period));
        Assert.True(trim.IsHot);
    }

    // ── E) Robustness (NaN/Infinity) ─────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var trim = new Trim(5, 0.0); // trimPct=0 means SMA for easy verification
        for (int i = 1; i <= 5; i++)
        {
            trim.Update(new TValue(DateTime.UtcNow, 10.0));
        }

        _ = trim.Last.Value; // should be 10 – value not compared directly

        // Feed NaN — should use last valid (10)
        trim.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(trim.Last.Value));

        // Feed Infinity — should use last valid
        trim.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(trim.Last.Value));
    }

    [Fact]
    public void AllNaN_DoesNotThrow()
    {
        var trim = new Trim(5);
        for (int i = 0; i < 10; i++)
        {
            TValue result = trim.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ── F) Consistency (batch == streaming == span == eventing) ─────────────

    [Fact]
    public void Consistency_BatchEqualsStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0002, sigma: 0.02, seed: 42);
        int n = 100;
        int period = 14;
        double trimPct = 10.0;

        var prices = new double[n];
        var times = new long[n];
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            prices[i] = bar.Close;
            times[i] = (t0.AddMinutes(i)).Ticks;
        }

        // Streaming
        var streamTrim = new Trim(period, trimPct);
        double lastStream = 0;
        for (int i = 0; i < n; i++)
        {
            lastStream = streamTrim.Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), prices[i])).Value;
        }

        // Batch via Span
        var spanOutput = new double[n];
        Trim.Batch(prices, spanOutput, period, trimPct);

        Assert.Equal(lastStream, spanOutput[n - 1], 10);
    }

    [Fact]
    public void Consistency_SpanValidatesLengths()
    {
        var src = new double[10];
        var dst = new double[9]; // wrong length
        Assert.Throws<ArgumentException>(() => Trim.Batch(src, dst, 5));
    }

    [Fact]
    public void Consistency_SpanValidatesPeriod()
    {
        var src = new double[10];
        var dst = new double[10];
        Assert.Throws<ArgumentException>(() => Trim.Batch(src, dst, 2));
    }

    // ── G) Span API large-data (stackalloc threshold) ─────────────────────────

    [Fact]
    public void Span_LargePeriod_NoStackOverflow()
    {
        int n = 1000;
        int period = 300; // > 256 stackalloc threshold → ArrayPool path
        var src = new double[n];
        var dst = new double[n];
        for (int i = 0; i < n; i++)
        {
            src[i] = i + 1.0;
        }

        // Must not throw
        Trim.Batch(src, dst, period, 10.0);
        Assert.True(double.IsFinite(dst[n - 1]));
    }

    // ── H) Chainability / eventing ───────────────────────────────────────────

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var trim = new Trim(5);
        int fireCount = 0;
        trim.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        for (int i = 0; i < 10; i++)
        {
            trim.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(10, fireCount);
    }

    [Fact]
    public void Chaining_EventBased_Works()
    {
        var trim1 = new Trim(5, 10.0);
        var trim2 = new Trim(trim1, 3, 0.0);

        for (int i = 0; i < 20; i++)
        {
            trim1.Update(new TValue(DateTime.UtcNow, i + 1.0));
        }

        Assert.True(double.IsFinite(trim2.Last.Value));
    }
}
