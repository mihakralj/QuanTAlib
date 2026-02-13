using Xunit;

namespace QuanTAlib.Tests;

public sealed class WillrConstructorTests
{
    [Fact]
    public void DefaultPeriod_Is14()
    {
        var w = new Willr();
        Assert.Equal(14, w.Period);
    }

    [Fact]
    public void CustomPeriod_IsStored()
    {
        var w = new Willr(period: 20);
        Assert.Equal(20, w.Period);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void InvalidPeriod_Throws(int period)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Willr(period));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void MinimumPeriod_IsOne()
    {
        var w = new Willr(period: 1);
        Assert.Equal(1, w.Period);
    }

    [Fact]
    public void Name_IncludesPeriod()
    {
        var w = new Willr(period: 10);
        Assert.Contains("10", w.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        Assert.Equal(14, new Willr(14).WarmupPeriod);
        Assert.Equal(5, new Willr(5).WarmupPeriod);
    }
}

public sealed class WillrBasicTests
{
    [Fact]
    public void Update_Returns_TValue()
    {
        var w = new Willr();
        var result = w.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var w = new Willr();
        _ = w.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.True(double.IsFinite(w.Last.Value));
    }

    [Fact]
    public void IsHot_AfterWarmup()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 100), isNew: true);
        }
        Assert.False(w.IsHot);

        w.Update(new TBar(time.AddMinutes(4), 104, 109, 99, 106, 100), isNew: true);
        Assert.True(w.IsHot);
    }

    [Fact]
    public void Name_IsNotNull()
    {
        var w = new Willr();
        Assert.NotNull(w.Name);
        Assert.NotEmpty(w.Name);
    }
}

public sealed class WillrRangeTests
{
    [Fact]
    public void CloseAtHighest_ValueIsZero()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100, 110, 90, 100, 100), isNew: true);
        }

        // Close at highest high (110)
        w.Update(new TBar(time.AddMinutes(5), 110, 110, 90, 110, 100), isNew: true);
        Assert.Equal(0.0, w.Last.Value, 1e-10);
    }

    [Fact]
    public void CloseAtLowest_ValueIsNeg100()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100, 110, 90, 100, 100), isNew: true);
        }

        // Close at lowest low (90)
        w.Update(new TBar(time.AddMinutes(5), 90, 110, 90, 90, 100), isNew: true);
        Assert.Equal(-100.0, w.Last.Value, 1e-10);
    }

    [Fact]
    public void CloseAtMidpoint_ValueIsNeg50()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100, 110, 90, 100, 100), isNew: true);
        }

        // Close at midpoint of range (100 = midpoint of 90-110)
        w.Update(new TBar(time.AddMinutes(5), 100, 110, 90, 100, 100), isNew: true);
        Assert.Equal(-50.0, w.Last.Value, 1e-10);
    }

    [Fact]
    public void ConstantBars_ValueIsNeg50()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100, 100, 100, 100, 100), isNew: true);
        }

        // Range=0, should return -50
        Assert.Equal(-50.0, w.Last.Value, 1e-10);
    }

    [Fact]
    public void Rising_Produces_NearZero()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 100.0 + (i * 2.0);
            w.Update(new TBar(time.AddMinutes(i), price, price + 1, price - 1, price + 1, 100), isNew: true);
        }

        // Close at recent high → WillR should be near 0 (> -20)
        Assert.True(w.Last.Value > -20.0);
    }

    [Fact]
    public void Falling_Produces_NearNeg100()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double price = 200.0 - (i * 2.0);
            w.Update(new TBar(time.AddMinutes(i), price, price + 1, price - 1, price - 1, 100), isNew: true);
        }

        // Close at recent low → WillR should be near -100 (< -80)
        Assert.True(w.Last.Value < -80.0);
    }
}

public sealed class WillrBarCorrectionTests
{
    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time, 100, 105, 95, 100, 100);
        var bar2 = new TBar(time.AddMinutes(1), 102, 108, 98, 104, 100);

        w.Update(bar1, isNew: true);
        var v1 = w.Last.Value;

        w.Update(bar2, isNew: true);
        var v2 = w.Last.Value;

        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void IsNew_False_RewritesCurrent()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        w.Update(new TBar(time, 100, 105, 95, 100, 100), isNew: true);

        w.Update(new TBar(time.AddMinutes(1), 102, 108, 98, 104, 100), isNew: true);
        var beforeCorrection = w.Last.Value;

        // Correct current bar (isNew=false)
        w.Update(new TBar(time.AddMinutes(1), 110, 115, 98, 112, 100), isNew: false);
        var afterCorrection = w.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        // Feed 3 bars
        for (int i = 0; i < 3; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 100), isNew: true);
        }

        // Add a new bar
        w.Update(new TBar(time.AddMinutes(3), 103, 108, 98, 105, 100), isNew: true);
        var original = w.Last.Value;

        // Correct it several times (isNew=false)
        w.Update(new TBar(time.AddMinutes(3), 110, 115, 98, 112, 100), isNew: false);
        w.Update(new TBar(time.AddMinutes(3), 90, 115, 85, 88, 100), isNew: false);

        // Correct back to original data
        w.Update(new TBar(time.AddMinutes(3), 103, 108, 98, 105, 100), isNew: false);
        var restored = w.Last.Value;

        Assert.Equal(original, restored, 1e-10);
    }
}

public sealed class WillrResetTests
{
    [Fact]
    public void Reset_ClearsState()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 100), isNew: true);
        }

        Assert.True(w.IsHot);

        w.Reset();

        Assert.False(w.IsHot);
        Assert.Equal(default, w.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 100), isNew: true);
        }

        w.Reset();

        // Should be reusable after reset
        var result = w.Update(new TBar(time, 100, 105, 95, 100, 100), isNew: true);
        Assert.True(double.IsFinite(result.Value));
        Assert.False(w.IsHot);
    }
}

public sealed class WillrRobustnessTests
{
    [Fact]
    public void NaN_Uses_LastValid()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        // Feed valid data
        for (int i = 0; i < 5; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 100, 100), isNew: true);
        }

        _ = w.Last.Value;

        // Feed NaN bar
        w.Update(new TBar(time.AddMinutes(5), double.NaN, double.NaN, double.NaN, double.NaN, 100), isNew: true);

        Assert.True(double.IsFinite(w.Last.Value));
    }

    [Fact]
    public void Infinity_Uses_LastValid()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            w.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 100, 100), isNew: true);
        }

        // Feed Infinity bar
        w.Update(new TBar(time.AddMinutes(5), double.PositiveInfinity, double.PositiveInfinity,
            double.NegativeInfinity, double.PositiveInfinity, 100), isNew: true);

        Assert.True(double.IsFinite(w.Last.Value));
    }

    [Fact]
    public void AllNaN_Returns_NaN()
    {
        var w = new Willr(period: 5);
        var time = DateTime.UtcNow;

        // First data is NaN — no last-valid to substitute
        var result = w.Update(new TBar(time, double.NaN, double.NaN, double.NaN, double.NaN, 100), isNew: true);
        Assert.True(double.IsNaN(result.Value));
    }
}

public sealed class WillrBatchTests
{
    private static TBarSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Batch_TBarSeries_ProducesOutput()
    {
        var bars = GenerateSeries(100);
        var result = Willr.Batch(bars, period: 14);

        Assert.Equal(100, result.Count);
        Assert.True(double.IsFinite(result[^1].Value));
    }

    [Fact]
    public void Calculate_Returns_ResultsAndIndicator()
    {
        var bars = GenerateSeries(100);
        var (results, indicator) = Willr.Calculate(bars, period: 14);

        Assert.Equal(100, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Streaming_Matches_Batch()
    {
        var bars = GenerateSeries(200);
        const int period = 14;

        var w = new Willr(period);
        for (int i = 0; i < bars.Count; i++)
        {
            w.Update(bars[i]);
        }

        var batch = Willr.Batch(bars, period);

        Assert.Equal(w.Last.Value, batch[^1].Value, 1e-6);
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var output = Array.Empty<double>();
        Willr.Batch(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty,
            ReadOnlySpan<double>.Empty, output.AsSpan(), 14);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Willr.Batch(new double[10], new double[10], new double[10], new double[10], 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Willr.Batch(new double[10], new double[5], new double[10], new double[10], 14));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputTooSmall_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Willr.Batch(new double[10], new double[10], new double[10], new double[5], 14));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Update_TBarSeries_ProducesOutput()
    {
        var bars = GenerateSeries(100);
        var w = new Willr(14);
        var result = w.Update(bars);

        Assert.Equal(100, result.Count);
        Assert.True(w.IsHot);
    }

    [Fact]
    public void Batch_NullSource_ReturnsEmpty()
    {
        var result = Willr.Batch(null!, 14);
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_EmptySource_ReturnsEmpty()
    {
        var result = Willr.Batch(new TBarSeries(), 14);
        Assert.Empty(result);
    }
}

public sealed class WillrEventTests
{
    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var w = new Willr(period: 5);
        var eventRaised = false;

        w.Pub += (object? _, in TValueEventArgs e) => { eventRaised = true; };

        w.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 100), isNew: true);
        Assert.True(eventRaised);
    }

    [Fact]
    public void Chaining_Works()
    {
        var bars = new TBarSeries();
        var w = new Willr(bars, period: 5);

        TValue? lastValue = null;
        w.Pub += (object? _, in TValueEventArgs e) => { lastValue = e.Value; };

        var time = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 100));
        }

        Assert.NotNull(lastValue);
        Assert.True(double.IsFinite(lastValue.Value.Value));
    }
}

public sealed class WillrPrimeTests
{
    [Fact]
    public void Prime_TBarSeries_SetsState()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), 100 + i, 105 + i, 95 + i, 102 + i, 100));
        }

        var w = new Willr(period: 5);
        w.Prime(bars);

        Assert.True(w.IsHot);
        Assert.True(double.IsFinite(w.Last.Value));
    }

    [Fact]
    public void Prime_Span_SetsState()
    {
        var data = new double[50];
        for (int i = 0; i < 50; i++)
        {
            data[i] = 100.0 + i;
        }

        var w = new Willr(period: 5);
        w.Prime(data.AsSpan());

        Assert.True(w.IsHot);
        Assert.True(double.IsFinite(w.Last.Value));
    }

    [Fact]
    public void Prime_EmptySeries_NoError()
    {
        var w = new Willr(period: 5);
        w.Prime(new TBarSeries());

        Assert.False(w.IsHot);
    }

    [Fact]
    public void Prime_EmptySpan_NoError()
    {
        var w = new Willr(period: 5);
        w.Prime(ReadOnlySpan<double>.Empty);

        Assert.False(w.IsHot);
    }
}

public sealed class WillrConsistencyTests
{
    private static TBarSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Span_Matches_TBarSeries()
    {
        var bars = GenerateSeries(200);
        const int period = 14;

        var batchResult = Willr.Batch(bars, period);

        var output = new double[bars.Count];
        Willr.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, output.AsSpan(), period);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], output[i], 12);
        }
    }

    [Fact]
    public void Different_Periods_Produce_Different_Results()
    {
        var bars = GenerateSeries(100);

        var r5 = Willr.Batch(bars, period: 5);
        var r20 = Willr.Batch(bars, period: 20);

        bool anyDifferent = false;
        for (int i = 20; i < 100; i++)
        {
            if (Math.Abs(r5.Values[i] - r20.Values[i]) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void Deterministic_Across_Runs()
    {
        var bars = GenerateSeries(200, seed: 99);
        const int period = 14;

        var r1 = Willr.Batch(bars, period);
        var r2 = Willr.Batch(bars, period);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(r1.Values[i], r2.Values[i], 15);
        }
    }

    [Fact]
    public void WillR_Is_Inverse_Stoch()
    {
        var bars = GenerateSeries(200);
        const int period = 14;

        var willr = Willr.Batch(bars, period);
        var (stochK, _) = Stoch.Batch(bars, kLength: period);

        // WillR = -(100 - Stoch%K) = Stoch%K - 100
        // But only when range>0 (when range=0, Stoch returns 0, WillR returns -50)
        for (int i = period; i < bars.Count; i++)
        {
            double stochVal = stochK.Values[i];
            double willrVal = willr.Values[i];

            if (Math.Abs(stochVal) > 1e-10 || Math.Abs(willrVal + 50.0) > 1e-10)
            {
                // Only compare when not at the degenerate range=0 case
                Assert.Equal(stochVal - 100.0, willrVal, 1e-9);
            }
        }
    }
}
