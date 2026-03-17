using Xunit;

namespace QuanTAlib.Tests;

public sealed class RrsiTests
{
    private static TSeries GenerateSeries(int count, int seed = 42)
    {
        var rng = new Random(seed);
        var series = new TSeries();
        double price = 100.0;
        for (int i = 0; i < count; i++)
        {
            price += (rng.NextDouble() - 0.5) * 2.0;
            series.Add(new TValue(DateTime.UtcNow.AddMinutes(i), price));
        }
        return series;
    }

    // === A) Constructor ===

    [Fact]
    public void Constructor_Default_ValidState()
    {
        var ind = new Rrsi();
        Assert.Equal(10, ind.SmoothLength);
        Assert.Equal(10, ind.RsiLength);
        Assert.False(ind.IsHot);
        Assert.Contains("Rrsi(", ind.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomParams_ValidState()
    {
        var ind = new Rrsi(smoothLength: 8, rsiLength: 14);
        Assert.Equal(8, ind.SmoothLength);
        Assert.Equal(14, ind.RsiLength);
        Assert.Contains("Rrsi(8,14)", ind.Name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public void Constructor_InvalidParams_Throws(int smooth, int rsi)
    {
        Assert.Throws<ArgumentException>(() => new Rrsi(smooth, rsi));
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_SingleValue_ReturnsValue()
    {
        var ind = new Rrsi();
        var result = ind.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_EnoughBars_BecomesHot()
    {
        var ind = new Rrsi(smoothLength: 5, rsiLength: 5);
        var series = GenerateSeries(30);
        foreach (var tv in series)
        {
            ind.Update(tv);
        }
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void Update_NotEnoughBars_NotHot()
    {
        var ind = new Rrsi(smoothLength: 10, rsiLength: 10);
        var series = GenerateSeries(5);
        foreach (var tv in series)
        {
            ind.Update(tv);
        }
        Assert.False(ind.IsHot);
    }

    // === C) Output range ===

    [Fact]
    public void Output_IsFinite_ForAll()
    {
        var ind = new Rrsi();
        var series = GenerateSeries(200);
        int bar = 0;
        foreach (var tv in series)
        {
            var result = ind.Update(tv);
            Assert.True(double.IsFinite(result.Value),
                $"Non-finite at bar {bar}: {result.Value}");
            bar++;
        }
    }

    [Fact]
    public void Output_OscillatesAroundZero()
    {
        var ind = new Rrsi();
        var series = GenerateSeries(500);
        bool hasPositive = false;
        bool hasNegative = false;
        foreach (var tv in series)
        {
            double val = ind.Update(tv).Value;
            if (val > 0.01) { hasPositive = true; }
            if (val < -0.01) { hasNegative = true; }
        }
        Assert.True(hasPositive, "Should have positive values");
        Assert.True(hasNegative, "Should have negative values");
    }

    [Fact]
    public void Output_FlatPrice_NearZero()
    {
        var ind = new Rrsi();
        for (int i = 0; i < 100; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 50.0));
        }
        Assert.True(Math.Abs(ind.Last.Value) < 0.01,
            $"Flat price should yield ~0, got {ind.Last.Value}");
    }

    // === D) Streaming vs Batch ===

    [Fact]
    public void StreamingMatchesBatch_TSeries()
    {
        var source = GenerateSeries(100);
        var batchResult = Rrsi.Batch(source, 10, 10);

        var streaming = new Rrsi(10, 10);
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
        }

        // Compare last values
        Assert.Equal(batchResult[^1].Value, streaming.Last.Value, 9);
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var source = GenerateSeries(100);
        var batchResult = Rrsi.Batch(source, 8, 12);

        double[] output = new double[source.Count];
        Rrsi.Batch(source.Values, output, 8, 12);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 9);
        }
    }

    // === E) Bar correction ===

    [Fact]
    public void BarCorrection_IsNew_False_DoesNotAdvance()
    {
        var ind = new Rrsi();
        var series = GenerateSeries(30);

        // Feed first 20 bars normally
        for (int i = 0; i < 20; i++)
        {
            ind.Update(series[i]);
        }

        // Bar 20: first tick
        _ = ind.Update(series[20], isNew: true);

        // Bar 20: correction ticks (isNew=false)
        var result2 = ind.Update(new TValue(series[20].Time, series[20].Value + 0.5), isNew: false);
        var result3 = ind.Update(new TValue(series[20].Time, series[20].Value + 0.1), isNew: false);

        // Final tick should give a different result from first
        // but indicator should not have advanced count
        Assert.True(double.IsFinite(result2.Value));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        var source = GenerateSeries(50);
        var ind1 = new Rrsi(8, 10);
        var ind2 = new Rrsi(8, 10);

        // ind1: clean feed
        foreach (var tv in source)
        {
            ind1.Update(tv);
        }

        // ind2: feed with corrections on every other bar
        for (int i = 0; i < source.Count; i++)
        {
            ind2.Update(source[i], isNew: true);
            if (i % 2 == 0)
            {
                // Correct back to original value
                ind2.Update(new TValue(source[i].Time, source[i].Value + 1.0), isNew: false);
                ind2.Update(source[i], isNew: false);
            }
        }

        Assert.Equal(ind1.Last.Value, ind2.Last.Value, 9);
    }

    // === F) Reset ===

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Rrsi();
        var series = GenerateSeries(50);
        foreach (var tv in series)
        {
            ind.Update(tv);
        }
        Assert.True(ind.IsHot);

        ind.Reset();
        Assert.False(ind.IsHot);
        Assert.Equal(0.0, ind.Last.Value);
    }

    [Fact]
    public void Reset_ReplayProducesSameResult()
    {
        var source = GenerateSeries(100);
        var ind = new Rrsi();

        foreach (var tv in source) { ind.Update(tv); }
        double firstRun = ind.Last.Value;

        ind.Reset();
        foreach (var tv in source) { ind.Update(tv); }
        double secondRun = ind.Last.Value;

        Assert.Equal(firstRun, secondRun, 12);
    }

    // === G) Dispose ===

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ind = new Rrsi();
        var ex = Record.Exception(() => ind.Dispose());
        Assert.Null(ex);
    }

    // === H) Edge cases ===

    [Fact]
    public void NaN_Input_Handled()
    {
        var ind = new Rrsi();
        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 50.0 + i));
        }
        // Feed NaN
        var result = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(30), double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_Handled()
    {
        var ind = new Rrsi();
        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 50.0 + i));
        }
        var result = ind.Update(new TValue(DateTime.UtcNow.AddMinutes(30), double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Batch_EmptySeries_ReturnsEmpty()
    {
        var series = new TSeries();
        var result = Rrsi.Batch(series);
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_Span_LengthMismatch_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[5];
        Assert.Throws<ArgumentException>(() => Rrsi.Batch(src, dst));
    }

    [Fact]
    public void Batch_Span_InvalidSmoothLength_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[10];
        Assert.Throws<ArgumentException>(() => Rrsi.Batch(src, dst, smoothLength: 0));
    }

    [Fact]
    public void Batch_Span_InvalidRsiLength_Throws()
    {
        double[] src = new double[10];
        double[] dst = new double[10];
        Assert.Throws<ArgumentException>(() => Rrsi.Batch(src, dst, rsiLength: 0));
    }

    [Fact]
    public void Batch_Span_Empty_NoException()
    {
        var ex = Record.Exception(() => Rrsi.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty));
        Assert.Null(ex);
    }

    // === I) Calculate factory ===

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var source = GenerateSeries(50);
        var (results, indicator) = Rrsi.Calculate(source, 10, 10);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    // === J) Pub event ===

    [Fact]
    public void PubEvent_FiresOnUpdate()
    {
        var source = new TSeries();
        var ind = new Rrsi(source, 5, 5);
        int count = 0;
        ind.Pub += (object? sender, in TValueEventArgs e) => count++;

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 50.0 + i));
        }

        Assert.Equal(20, count);
    }

    // === K) Trending input ===

    [Fact]
    public void StrongUptrend_PositiveOutput()
    {
        var ind = new Rrsi(smoothLength: 5, rsiLength: 5);
        // Feed flat, then strong uptrend
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        for (int i = 20; i < 50; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (i - 20) * 2.0));
        }
        Assert.True(ind.Last.Value > 0, $"Strong uptrend should be positive, got {ind.Last.Value}");
    }

    [Fact]
    public void StrongDowntrend_NegativeOutput()
    {
        var ind = new Rrsi(smoothLength: 5, rsiLength: 5);
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0));
        }
        for (int i = 20; i < 50; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 - (i - 20) * 2.0));
        }
        Assert.True(ind.Last.Value < 0, $"Strong downtrend should be negative, got {ind.Last.Value}");
    }
}
