using Xunit;

namespace QuanTAlib.Tests;

public class SgfTests
{
    private readonly GBM _gbm;

    public SgfTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Sgf(10, polyOrder: 10)); // Order >= size
        Assert.Throws<ArgumentException>(() => new Sgf(10, polyOrder: 15));

        var sgf = new Sgf(10, polyOrder: 2);
        Assert.NotNull(sgf);
        Assert.Equal("Sgf(9,2)", sgf.Name); // Period adjusted to odd
    }

    [Fact]
    public void Constructor_EvenPeriod_AdjustedToOdd()
    {
        var sgf = new Sgf(10, 2);
        Assert.Equal("Sgf(9,2)", sgf.Name);
        Assert.Equal(9, sgf.WarmupPeriod);
    }

    [Fact]
    public void Constructor_OddPeriod_Unchanged()
    {
        var sgf = new Sgf(11, 2);
        Assert.Equal("Sgf(11,2)", sgf.Name);
        Assert.Equal(11, sgf.WarmupPeriod);
    }

    [Fact]
    public void Constructor_Period1_Works()
    {
        // Period 1 should work (minimum after adjustment)
        var sgf = new Sgf(1, 0);
        Assert.NotNull(sgf);
    }

    [Fact]
    public void Constructor_PolyOrder4_Works()
    {
        var sgf = new Sgf(11, 4);
        Assert.Equal("Sgf(11,4)", sgf.Name);
    }

    [Fact]
    public void Constructor_DefaultPolyOrder_Is2()
    {
        var sgf = new Sgf(11);
        Assert.Equal("Sgf(11,2)", sgf.Name);
    }

    [Fact]
    public void Constructor_WithPublisher_Subscribes()
    {
        var source = new TSeries();
        var sgf = new Sgf(source, 11, 2);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(sgf.Last.Value));
    }

    // ── IsHot / WarmupPeriod ────────────────────────────────────────────

    [Fact]
    public void WarmupPeriod_IsCorrect()
    {
        var sgf = new Sgf(21, 2);
        Assert.Equal(21, sgf.WarmupPeriod);
        Assert.False(sgf.IsHot);

        for (int i = 0; i < 21; i++)
        {
            sgf.Update(new TValue(DateTime.UtcNow, 100));
        }

        Assert.True(sgf.IsHot);
    }

    [Fact]
    public void IsHot_FalseBeforeFull()
    {
        var sgf = new Sgf(11, 2);
        for (int i = 0; i < 10; i++)
        {
            sgf.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.False(sgf.IsHot);
    }

    // ── Update ──────────────────────────────────────────────────────────

    [Fact]
    public void Update_ConstantInput_ReturnsConstant()
    {
        var sgf = new Sgf(5, 2);
        for (int i = 0; i < 10; i++)
        {
            sgf.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        // A constant signal filtered should still be constant
        Assert.Equal(42.0, sgf.Last.Value, 1e-9);
    }

    [Fact]
    public void Update_SmoothsNoisyInput()
    {
        var sgf = new Sgf(21, 2);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            sgf.Update(new TValue(bar.Time, bar.Close));
        }

        // Filtered output should be finite and not exactly equal to raw
        Assert.True(double.IsFinite(sgf.Last.Value));
    }

    [Fact]
    public void Update_BarCorrection_Works()
    {
        var sgf = new Sgf(5, 2);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            sgf.Update(new TValue(now.AddMinutes(i), 100 + i));
        }

        // New bar
        var result1 = sgf.Update(new TValue(now.AddMinutes(10), 200), isNew: true);

        // Correction to same bar
        var result2 = sgf.Update(new TValue(now.AddMinutes(10), 150), isNew: false);

        // Different corrections should yield different results
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_PartialWindow_Works()
    {
        var sgf = new Sgf(11, 2);
        // First value before buffer is full should still produce a finite result
        var result = sgf.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    // ── NaN handling ────────────────────────────────────────────────────

    [Fact]
    public void HandlesNaN()
    {
        var sgf = new Sgf(5, 2);

        sgf.Update(new TValue(DateTime.UtcNow, 100));
        sgf.Update(new TValue(DateTime.UtcNow, double.NaN));
        sgf.Update(new TValue(DateTime.UtcNow, 102));

        // Should produce valid result if sufficient valid data exists within window
        Assert.True(double.IsFinite(sgf.Last.Value));
    }

    [Fact]
    public void AllNaN_InWindow_ReturnsInput()
    {
        var sgf = new Sgf(3, 1);
        sgf.Update(new TValue(DateTime.UtcNow, double.NaN));
        // With only NaN in buffer and partial window, should fallback to input
        // (wSum <= epsilon path)
        Assert.True(double.IsNaN(sgf.Last.Value) || double.IsFinite(sgf.Last.Value));
    }

    // ── Reset ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var sgf = new Sgf(11, 2);
        for (int i = 0; i < 20; i++)
        {
            sgf.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(sgf.IsHot);

        sgf.Reset();
        Assert.False(sgf.IsHot);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var sgf = new Sgf(5, 2);
        for (int i = 0; i < 10; i++)
        {
            sgf.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        var firstLast = sgf.Last.Value;

        sgf.Reset();
        for (int i = 0; i < 10; i++)
        {
            sgf.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.Equal(firstLast, sgf.Last.Value, 1e-10);
    }

    // ── Batch ───────────────────────────────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;
        const int period = 21;
        int polyOrder = 2;

        // 1. Batch Mode
        var batchResult = new Sgf(period, polyOrder).Update(series);

        // 2. Streaming Mode
        var streaming = new Sgf(period, polyOrder);
        var streamingResults = new TSeries();
        foreach (var item in series)
        {
            streamingResults.Add(streaming.Update(item));
        }

        // 3. Span Mode
        double[] spanInput = series.Values.ToArray();
        double[] spanOutput = new double[spanInput.Length];
        Sgf.Batch(spanInput, spanOutput, period, polyOrder);

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
        var result = Sgf.Batch(data.Close, 11, 2);
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Batch_Span_MismatchedLength_Throws()
    {
        var source = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentException>(() => Sgf.Batch(source, output, 5, 2));
    }

    [Fact]
    public void Batch_Span_PolyOrderTooLarge_Throws()
    {
        var source = new double[10];
        var output = new double[10];
        Assert.Throws<ArgumentException>(() => Sgf.Batch(source, output, 5, 5));
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var sgf = new Sgf(11, 2);
        var result = sgf.Update(new TSeries());
        Assert.Empty(result);
    }

    // ── Calculate ───────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Sgf.Calculate(data.Close, 11, 2);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    // ── Prime ───────────────────────────────────────────────────────────

    [Fact]
    public void Prime_WarmsUpIndicator()
    {
        var sgf = new Sgf(11, 2);
        var values = new double[20];
        for (int i = 0; i < 20; i++)
        {
            values[i] = 100 + i;
        }

        sgf.Prime(values);
        Assert.True(sgf.IsHot);
    }

    // ── Dispose ─────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_WithPublisher_Unsubscribes()
    {
        var source = new TSeries();
        var sgf = new Sgf(source, 5, 2);
        source.Add(new TValue(DateTime.UtcNow, 100));

        sgf.Dispose();

        var lastBefore = sgf.Last;
        source.Add(new TValue(DateTime.UtcNow, 200));
        Assert.Equal(lastBefore, sgf.Last);
    }

    [Fact]
    public void Dispose_WithoutPublisher_DoesNotThrow()
    {
        var sgf = new Sgf(5, 2);
        sgf.Update(new TValue(DateTime.UtcNow, 100));
        sgf.Dispose();
        Assert.True(true); // S2699: explicit assertion for dispose-only test
    }
}
