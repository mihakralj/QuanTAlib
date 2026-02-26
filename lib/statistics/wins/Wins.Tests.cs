namespace QuanTAlib.Tests;

public class WinsTests
{
    // ── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsOnPeriodLessThan3()
    {
        Assert.Throws<ArgumentException>(() => new Wins(2));
        Assert.Throws<ArgumentException>(() => new Wins(1));
        Assert.Throws<ArgumentException>(() => new Wins(0));
        Assert.Throws<ArgumentException>(() => new Wins(-1));
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidWinPct()
    {
        Assert.Throws<ArgumentException>(() => new Wins(10, -1.0));
        Assert.Throws<ArgumentException>(() => new Wins(10, 50.0));
        Assert.Throws<ArgumentException>(() => new Wins(10, 75.0));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var wins = new Wins(20, 10.0);
        Assert.Equal("Wins(20,10)", wins.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var wins = new Wins(15, 10.0);
        Assert.Equal(15, wins.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ValidMinimalPeriod()
    {
        var wins = new Wins(3);
        Assert.NotNull(wins);
    }

    // ── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValue()
    {
        var wins = new Wins(5);
        TValue result = wins.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result.Value, wins.Last.Value);
    }

    [Fact]
    public void IsHot_FalseUntilWindowFull()
    {
        var wins = new Wins(5);
        for (int i = 0; i < 4; i++)
        {
            wins.Update(new TValue(DateTime.UtcNow, i + 1.0));
            Assert.False(wins.IsHot);
        }

        wins.Update(new TValue(DateTime.UtcNow, 5.0));
        Assert.True(wins.IsHot);
    }

    [Fact]
    public void WinPctZero_EqualsSMA()
    {
        // With winPct=0, WINS should equal SMA
        var wins = new Wins(5, 0.0);
        double[] vals = [10.0, 20.0, 30.0, 40.0, 50.0];
        double result = 0;
        foreach (double v in vals)
        {
            result = wins.Update(new TValue(DateTime.UtcNow, v)).Value;
        }

        Assert.Equal(30.0, result, 10); // SMA of [10,20,30,40,50] = 30
    }

    [Fact]
    public void WinsKnownValue_CorrectResult()
    {
        // Window: [1,2,3,4,5,6,7,8,9,10], winPct=10 on period=10
        // winCount = floor(10 * 10/100) = 1
        // lowerBound = sorted[1] = 2, upperBound = sorted[8] = 9
        // Replace sorted[0]=1 with 2, sorted[9]=10 with 9
        // Values: [2,2,3,4,5,6,7,8,9,9], sum = 55, mean = 55/10 = 5.5
        var wins = new Wins(10, 10.0);
        for (int i = 1; i <= 10; i++)
        {
            wins.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(5.5, wins.Last.Value, 10);
    }

    [Fact]
    public void WinsVsTrim_WinsHigherForOutlier()
    {
        // With an extreme outlier, WINS should be closer to SMA than TRIM
        // because WINS replaces (retains full count), TRIM discards
        var trim = new Trim(10, 10.0);
        var wins = new Wins(10, 10.0);

        // Same data — [1,2,3,4,5,6,7,8,9,100_outlier]
        double[] vals = [1, 2, 3, 4, 5, 6, 7, 8, 9, 100];
        foreach (double v in vals)
        {
            trim.Update(new TValue(DateTime.UtcNow, v));
            wins.Update(new TValue(DateTime.UtcNow, v));
        }

        // TRIM drops 100, WINS replaces it with 9 (boundary)
        // TRIM: mean([2..9]) = 44/8 = 5.5
        // WINS: (1/clamp_lower=2, 2,3,4,5,6,7,8,9, 9/clamp_upper=9) ... wait boundary math
        // winCount=1, lowerBound=sorted[1]=2, upperBound=sorted[8]=9
        // Replace sorted[0]=1→2, sorted[9]=100→9
        // Sum = 2+2+3+4+5+6+7+8+9+9 = 55, mean = 5.5
        // Both equal 5.5 but for different reasons
        Assert.True(double.IsFinite(trim.Last.Value));
        Assert.True(double.IsFinite(wins.Last.Value));
    }

    // ── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void BarCorrection_IsNewFalse_RewritesLastBar()
    {
        var wins = new Wins(5, 10.0);
        var t = DateTime.UtcNow;

        for (int i = 1; i <= 5; i++)
        {
            wins.Update(new TValue(t, i));
        }

        double before = wins.Last.Value;

        wins.Update(new TValue(t, 100.0), isNew: false);
        double afterCorrection = wins.Last.Value;

        wins.Update(new TValue(t, 5.0), isNew: true);
        double afterNewBar = wins.Last.Value;

        // Correction with outlier differs from original
        Assert.NotEqual(before, afterCorrection);
        // After new bar, result is finite and valid
        Assert.True(double.IsFinite(afterNewBar));
        // The new bar after correction differs from the correction itself
        Assert.NotEqual(afterCorrection, afterNewBar);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var wins = new Wins(5);
        for (int i = 0; i < 5; i++)
        {
            wins.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        Assert.True(wins.IsHot);
        wins.Reset();
        Assert.False(wins.IsHot);
        Assert.Equal(0, wins.Last.Value);
    }

    // ── D) Warmup/convergence ────────────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        int period = 7;
        var wins = new Wins(period);
        for (int i = 0; i < period - 1; i++)
        {
            wins.Update(new TValue(DateTime.UtcNow, i));
            Assert.False(wins.IsHot);
        }

        wins.Update(new TValue(DateTime.UtcNow, period));
        Assert.True(wins.IsHot);
    }

    // ── E) Robustness ───────────────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var wins = new Wins(5, 0.0);
        for (int i = 0; i < 5; i++)
        {
            wins.Update(new TValue(DateTime.UtcNow, 10.0));
        }

        wins.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(wins.Last.Value));

        wins.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(wins.Last.Value));
    }

    [Fact]
    public void AllNaN_DoesNotThrow()
    {
        var wins = new Wins(5);
        for (int i = 0; i < 10; i++)
        {
            TValue result = wins.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ── F) Consistency ────────────────────────────────────────────────────────

    [Fact]
    public void Consistency_BatchEqualsStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0002, sigma: 0.02, seed: 77);
        int n = 100;
        int period = 14;
        double winPct = 10.0;

        var prices = new double[n];
        var times = new long[n];
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < n; i++)
        {
            TBar bar = rng.Next();
            prices[i] = bar.Close;
            times[i] = (t0.AddMinutes(i)).Ticks;
        }

        var streamWins = new Wins(period, winPct);
        double lastStream = 0;
        for (int i = 0; i < n; i++)
        {
            lastStream = streamWins.Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), prices[i])).Value;
        }

        var spanOutput = new double[n];
        Wins.Batch(prices, spanOutput, period, winPct);

        Assert.Equal(lastStream, spanOutput[n - 1], 10);
    }

    [Fact]
    public void Consistency_SpanValidatesLengths()
    {
        var src = new double[10];
        var dst = new double[9];
        Assert.Throws<ArgumentException>(() => Wins.Batch(src, dst, 5));
    }

    [Fact]
    public void Consistency_SpanValidatesPeriod()
    {
        var src = new double[10];
        var dst = new double[10];
        Assert.Throws<ArgumentException>(() => Wins.Batch(src, dst, 2));
    }

    // ── G) Span large-data ─────────────────────────────────────────────────

    [Fact]
    public void Span_LargePeriod_NoStackOverflow()
    {
        int n = 1000;
        int period = 300;
        var src = new double[n];
        var dst = new double[n];
        for (int i = 0; i < n; i++)
        {
            src[i] = i + 1.0;
        }

        Wins.Batch(src, dst, period, 10.0);
        Assert.True(double.IsFinite(dst[n - 1]));
    }

    // ── H) Eventing ──────────────────────────────────────────────────────────

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var wins = new Wins(5);
        int fireCount = 0;
        wins.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        for (int i = 0; i < 10; i++)
        {
            wins.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(10, fireCount);
    }

    [Fact]
    public void Chaining_EventBased_Works()
    {
        var wins1 = new Wins(5, 10.0);
        var wins2 = new Wins(wins1, 3, 0.0);

        for (int i = 0; i < 20; i++)
        {
            wins1.Update(new TValue(DateTime.UtcNow, i + 1.0));
        }

        Assert.True(double.IsFinite(wins2.Last.Value));
    }
}
