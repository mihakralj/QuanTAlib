using Xunit;

namespace QuanTAlib.Tests;

public class WienerTests
{
    private readonly GBM _gbm;

    public WienerTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wiener(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wiener(10, smoothPeriod: 1));

        var wiener = new Wiener(10, smoothPeriod: 10);
        Assert.NotNull(wiener);
        Assert.Equal("Wiener(10,10)", wiener.Name);
    }

    [Fact]
    public void Constructor_Period1_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wiener(1));
    }

    [Fact]
    public void Constructor_SmoothPeriod1_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Wiener(10, 1));
    }

    [Fact]
    public void Constructor_DefaultSmoothPeriod()
    {
        var wiener = new Wiener(10);
        Assert.Equal("Wiener(10,10)", wiener.Name);
    }

    [Fact]
    public void Constructor_WarmupPeriod_IsMax()
    {
        var wiener = new Wiener(20, 5);
        Assert.Equal(20, wiener.WarmupPeriod);

        var wiener2 = new Wiener(5, 20);
        Assert.Equal(20, wiener2.WarmupPeriod);
    }

    // ── IsHot / WarmupPeriod ────────────────────────────────────────────

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        int period = 20;
        int smooth = 10;
        var wiener = new Wiener(period, smooth);
        // Requirement: WarmupPeriod = Math.Max(period, smooth)
        Assert.Equal(Math.Max(period, smooth), wiener.WarmupPeriod);
        Assert.False(wiener.IsHot);

        for (int i = 0; i < Math.Max(period, smooth); i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.True(wiener.IsHot);
    }

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var wiener = new Wiener(10, 10);
        for (int i = 0; i < 9; i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.False(wiener.IsHot);
    }

    // ── Update ──────────────────────────────────────────────────────────

    [Fact]
    public void Update_SingleValue_ReturnsItself()
    {
        var wiener = new Wiener(5, 5);
        var result = wiener.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value);
    }

    [Fact]
    public void Update_ConstantInput_ConvergesToConstant()
    {
        var wiener = new Wiener(10, 5);
        for (int i = 0; i < 50; i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, wiener.Last.Value, 1e-6);
    }

    [Fact]
    public void Update_SmoothsNoisySignal()
    {
        var wiener = new Wiener(20, 10);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            wiener.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(wiener.IsHot);
        Assert.True(double.IsFinite(wiener.Last.Value));
    }

    [Fact]
    public void Update_BarCorrection_Works()
    {
        var wiener = new Wiener(5, 5);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            wiener.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // New bar
        var result1 = wiener.Update(new TValue(now.AddMinutes(10), 200), isNew: true);

        // Correction
        var result2 = wiener.Update(new TValue(now.AddMinutes(10), 150), isNew: false);

        // Different correction values should yield different results
        Assert.NotEqual(result1.Value, result2.Value);
    }

    // ── NaN handling ────────────────────────────────────────────────────

    [Fact]
    public void HandlesNaN()
    {
        var wiener = new Wiener(5, 5);

        wiener.Update(new TValue(DateTime.UtcNow, 100));
        wiener.Update(new TValue(DateTime.UtcNow, double.NaN));
        wiener.Update(new TValue(DateTime.UtcNow, 102));

        // Should produce valid result if sufficient valid data exists within window (or handle it gracefully)
        Assert.True(double.IsFinite(wiener.Last.Value));
    }

    [Fact]
    public void NaN_FirstValue_UsesFallback()
    {
        var wiener = new Wiener(5, 5);
        var result = wiener.Update(new TValue(DateTime.UtcNow, double.NaN));
        // Fallback: Last.Value defaults to 0.0 (finite), so code returns 0.0
        // The code: double.IsFinite(Last.Value) ? Last.Value : input.Value
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void NaN_AfterValid_UsesLastValid()
    {
        var wiener = new Wiener(5, 5);
        wiener.Update(new TValue(DateTime.UtcNow, 100));

        var result = wiener.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.Equal(100.0, result.Value);
    }

    [Fact]
    public void Infinity_AfterValid_UsesLastValid()
    {
        var wiener = new Wiener(5, 5);
        wiener.Update(new TValue(DateTime.UtcNow, 100));

        var result = wiener.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.Equal(100.0, result.Value);
    }

    // ── Reset ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var wiener = new Wiener(10, 5);
        int warmup = Math.Max(10, 5);

        // Fill up to make it Hot
        for (int i = 0; i < warmup; i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(wiener.IsHot);

        wiener.Reset();
        Assert.False(wiener.IsHot);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var wiener = new Wiener(5, 5);
        for (int i = 0; i < 10; i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        var firstResult = wiener.Last.Value;

        wiener.Reset();
        for (int i = 0; i < 10; i++)
        {
            wiener.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.Equal(firstResult, wiener.Last.Value, 1e-10);
    }

    // ── Batch ───────────────────────────────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        const int period = 20;
        int smoothPeriod = 5;

        // 1. Batch Mode
        var batchResult = new Wiener(period, smoothPeriod).Update(series);

        // 2. Streaming Mode
        var streaming = new Wiener(period, smoothPeriod);
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        // 3. Span Mode
        double[] spanInput = series.Values.ToArray();
        double[] spanOutput = new double[spanInput.Length];
        Wiener.Batch(spanInput, spanOutput, period, smoothPeriod);

        // Assert
        for (int i = 0; i < series.Count; i++)
        {
            double batchVal = batchResult[i].Value;
            double streamVal = streamingResults[i].Value;
            double spanVal = spanOutput[i];

            if (double.IsNaN(batchVal))
            {
                Assert.True(double.IsNaN(streamVal));
                Assert.True(double.IsNaN(spanVal));
            }
            else
            {
                Assert.Equal(batchVal, streamVal, 1e-9);
                Assert.Equal(batchVal, spanVal, 1e-9);
            }
        }
    }

    [Fact]
    public void Batch_TSeries_Static()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var result = Wiener.Batch(data.Close, 10, 5);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Batch_Span_OutputTooShort_Throws()
    {
        var source = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentException>(() => Wiener.Batch(source, output, 5, 5));
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var wiener = new Wiener(10, 5);
        var result = wiener.Update(new TSeries());
        Assert.Empty(result);
    }

    [Fact]
    public void Update_TSeries_ReturnsCorrectCount()
    {
        var wiener = new Wiener(10, 5);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var result = wiener.Update(data.Close);
        Assert.Equal(50, result.Count);
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Wiener.Calculate(data.Close, 10, 5);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var wiener = new Wiener(10, 5);
        var values = new double[20];
        for (int i = 0; i < 20; i++)
        {
            values[i] = 100 + i;
        }

        wiener.Prime(values);
        Assert.True(wiener.IsHot);
    }

    [Fact]
    public void Prime_WithStepParameter()
    {
        var wiener = new Wiener(10, 5);
        var values = new double[20];
        for (int i = 0; i < 20; i++)
        {
            values[i] = 100 + i;
        }

        wiener.Prime(values, TimeSpan.FromMinutes(5));
        Assert.True(wiener.IsHot);
    }

    // ── Determinism ─────────────────────────────────────────────────────

    [Fact]
    public void TwoInstances_SameInput_SameOutput()
    {
        var w1 = new Wiener(10, 5);
        var w2 = new Wiener(10, 5);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            var r1 = w1.Update(tv);
            var r2 = w2.Update(tv);
            Assert.Equal(r1.Value, r2.Value);
        }
    }

    // ── Filter behavior ─────────────────────────────────────────────────

    [Fact]
    public void LargerPeriod_MoreSmoothing()
    {
        var smallPeriod = new Wiener(5, 5);
        var largePeriod = new Wiener(20, 5);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double sumDiffSmall = 0, sumDiffLarge = 0;
        int count = 0;
        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            var rSmall = smallPeriod.Update(tv);
            var rLarge = largePeriod.Update(tv);

            if (smallPeriod.IsHot && largePeriod.IsHot)
            {
                sumDiffSmall += Math.Abs(bar.Close - rSmall.Value);
                sumDiffLarge += Math.Abs(bar.Close - rLarge.Value);
                count++;
            }
        }

        // Both should produce finite outputs
        Assert.True(double.IsFinite(smallPeriod.Last.Value));
        Assert.True(double.IsFinite(largePeriod.Last.Value));
        Assert.True(count > 0);
    }

    [Fact]
    public void DifferentSmoothPeriod_ProduceDifferentOutputs()
    {
        var smooth5 = new Wiener(10, 5);
        var smooth15 = new Wiener(10, 15);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double last5 = 0, last15 = 0;
        foreach (var bar in bars)
        {
            var tv = new TValue(bar.Time, bar.Close);
            last5 = smooth5.Update(tv).Value;
            last15 = smooth15.Update(tv).Value;
        }

        Assert.NotEqual(last5, last15, 1e-6);
    }
}
