namespace QuanTAlib.Tests;

public class WavgTests
{
    // ── A) Constructor validation ────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsOnZeroPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Wavg(0));
        Assert.Throws<ArgumentException>(() => new Wavg(-1));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var wavg = new Wavg(14);
        Assert.Equal("Wavg(14)", wavg.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var wavg = new Wavg(20);
        Assert.Equal(20, wavg.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ValidPeriod1()
    {
        var wavg = new Wavg(1);
        Assert.NotNull(wavg);
    }

    // ── B) Basic calculation ─────────────────────────────────────────────────

    [Fact]
    public void Update_ReturnsValue()
    {
        var wavg = new Wavg(5);
        TValue result = wavg.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result.Value, wavg.Last.Value);
    }

    [Fact]
    public void IsHot_FalseUntilWindowFull()
    {
        var wavg = new Wavg(5);
        for (int i = 0; i < 4; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, i + 1.0));
            Assert.False(wavg.IsHot);
        }

        wavg.Update(new TValue(DateTime.UtcNow, 5.0));
        Assert.True(wavg.IsHot);
    }

    [Fact]
    public void SingleValue_ReturnsThatValue()
    {
        var wavg = new Wavg(5);
        TValue result = wavg.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, 10);
    }

    [Fact]
    public void KnownValue_CorrectWeightedAverage()
    {
        // period=4, values=[1,2,3,4] (oldest→newest)
        // weights = [1,2,3,4], denom = 4*5/2 = 10
        // WAVG = (1*1 + 2*2 + 3*3 + 4*4) / 10 = (1+4+9+16)/10 = 30/10 = 3.0
        var wavg = new Wavg(4);
        wavg.Update(new TValue(DateTime.UtcNow, 1.0));
        wavg.Update(new TValue(DateTime.UtcNow, 2.0));
        wavg.Update(new TValue(DateTime.UtcNow, 3.0));
        TValue result = wavg.Update(new TValue(DateTime.UtcNow, 4.0));

        Assert.Equal(3.0, result.Value, 10);
    }

    [Fact]
    public void AllSameValues_ReturnsValue()
    {
        // All weights × same value / sum_weights = value
        var wavg = new Wavg(10);
        for (int i = 0; i < 10; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, 5.0));
        }

        Assert.Equal(5.0, wavg.Last.Value, 10);
    }

    [Fact]
    public void SlidingWindow_DropsOldest()
    {
        // Fill with [1,2,3,4,5], then slide in 6
        // After sliding: window=[2,3,4,5,6]
        // WAVG = (1*2 + 2*3 + 3*4 + 4*5 + 5*6)/15 = (2+6+12+20+30)/15 = 70/15
        var wavg = new Wavg(5);
        for (int i = 1; i <= 5; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, i));
        }

        TValue result = wavg.Update(new TValue(DateTime.UtcNow, 6.0));
        Assert.Equal(70.0 / 15.0, result.Value, 10);
    }

    // ── C) State + bar correction ────────────────────────────────────────────

    [Fact]
    public void BarCorrection_IsNewFalse_RewritesLastBar()
    {
        var wavg = new Wavg(4);
        var t = DateTime.UtcNow;

        wavg.Update(new TValue(t, 1.0));
        wavg.Update(new TValue(t, 2.0));
        wavg.Update(new TValue(t, 3.0));
        wavg.Update(new TValue(t, 4.0));

        double before = wavg.Last.Value; // WAVG([1,2,3,4]) = (1+4+9+16)/10 = 3.0

        // Correct last bar to different value
        wavg.Update(new TValue(t, 10.0), isNew: false);
        double corrected = wavg.Last.Value;
        Assert.NotEqual(before, corrected); // correction changes result ✓

        // Next new bar with value=4: window slides from corrected state [1,2,3,10] to [2,3,10,4]
        // WAVG([2,3,10,4]) = (1*2+2*3+3*10+4*4)/10 = (2+6+30+16)/10 = 54/10 = 5.4
        wavg.Update(new TValue(t, 4.0), isNew: true);
        Assert.True(double.IsFinite(wavg.Last.Value)); // finite result
        Assert.NotEqual(corrected, wavg.Last.Value);   // new bar shifts the result
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var wavg = new Wavg(5);
        for (int i = 0; i < 5; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        Assert.True(wavg.IsHot);
        wavg.Reset();
        Assert.False(wavg.IsHot);
        Assert.Equal(0, wavg.Last.Value);
    }

    // ── D) Warmup/convergence ────────────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        int period = 8;
        var wavg = new Wavg(period);
        for (int i = 0; i < period - 1; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, i));
            Assert.False(wavg.IsHot);
        }

        wavg.Update(new TValue(DateTime.UtcNow, period));
        Assert.True(wavg.IsHot);
    }

    // ── E) Robustness ───────────────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var wavg = new Wavg(5);
        for (int i = 0; i < 5; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, 10.0));
        }

        wavg.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(wavg.Last.Value));

        wavg.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(wavg.Last.Value));
    }

    [Fact]
    public void AllNaN_DoesNotThrow()
    {
        var wavg = new Wavg(5);
        for (int i = 0; i < 10; i++)
        {
            TValue result = wavg.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ── F) Consistency ────────────────────────────────────────────────────────

    [Fact]
    public void Consistency_BatchEqualsStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.0002, sigma: 0.02, seed: 99);
        int n = 100;
        int period = 14;

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
        var streamWavg = new Wavg(period);
        double lastStream = 0;
        for (int i = 0; i < n; i++)
        {
            lastStream = streamWavg.Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), prices[i])).Value;
        }

        // Span batch
        var spanOutput = new double[n];
        Wavg.Batch(prices, spanOutput, period);

        Assert.Equal(lastStream, spanOutput[n - 1], 6);
    }

    [Fact]
    public void Consistency_SpanValidatesLengths()
    {
        var src = new double[10];
        var dst = new double[9];
        Assert.Throws<ArgumentException>(() => Wavg.Batch(src, dst, 5));
    }

    [Fact]
    public void Consistency_SpanValidatesPeriod()
    {
        var src = new double[10];
        var dst = new double[10];
        Assert.Throws<ArgumentException>(() => Wavg.Batch(src, dst, 0));
    }

    // ── G) Eventing ──────────────────────────────────────────────────────────

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var wavg = new Wavg(5);
        int fireCount = 0;
        wavg.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        for (int i = 0; i < 10; i++)
        {
            wavg.Update(new TValue(DateTime.UtcNow, i));
        }

        Assert.Equal(10, fireCount);
    }

    [Fact]
    public void Chaining_EventBased_Works()
    {
        var wavg1 = new Wavg(5);
        var wavg2 = new Wavg(wavg1, 3);

        for (int i = 0; i < 20; i++)
        {
            wavg1.Update(new TValue(DateTime.UtcNow, i + 1.0));
        }

        Assert.True(double.IsFinite(wavg2.Last.Value));
    }
}
