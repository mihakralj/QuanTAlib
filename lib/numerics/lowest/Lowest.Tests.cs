using Xunit;

namespace QuanTAlib.Tests;

public class LowestTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Lowest(0));
        Assert.Throws<ArgumentException>(() => new Lowest(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var indicator = new Lowest(14);
        Assert.Equal("Lowest(14)", indicator.Name);
        Assert.Equal(14, indicator.WarmupPeriod);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Update_ReturnsLowestInWindow()
    {
        var indicator = new Lowest(3);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 5.0));
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);

        indicator.Update(new TValue(time.AddMinutes(1), 3.0));
        Assert.Equal(3.0, indicator.Last.Value, Tolerance);

        indicator.Update(new TValue(time.AddMinutes(2), 8.0));
        Assert.Equal(3.0, indicator.Last.Value, Tolerance);

        // 5 drops out of window
        indicator.Update(new TValue(time.AddMinutes(3), 10.0));
        Assert.Equal(3.0, indicator.Last.Value, Tolerance);

        // 3 drops out of window
        indicator.Update(new TValue(time.AddMinutes(4), 7.0));
        Assert.Equal(7.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Period1_ReturnsSameValue()
    {
        var indicator = new Lowest(1);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double value = i * 2.5;
            indicator.Update(new TValue(time.AddMinutes(i), value));
            Assert.Equal(value, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsPreviousValue()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        indicator.Update(new TValue(time.AddMinutes(1), 5.0));
        indicator.Update(new TValue(time.AddMinutes(2), 15.0));
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);

        // Correct last value to be the new min
        indicator.Update(new TValue(time.AddMinutes(2), 2.0), isNew: false);
        Assert.Equal(2.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;
        double[] values = { 15.0, 10.0, 12.0, 8.0, 13.0, 5.0, 11.0 };

        // Process all values
        foreach (var v in values)
        {
            indicator.Update(new TValue(time, v));
            time = time.AddMinutes(1);
        }
        double finalResult = indicator.Last.Value;

        // Reset and process with corrections
        indicator.Reset();
        time = DateTime.UtcNow;
        foreach (var v in values)
        {
            // Submit wrong value first
            indicator.Update(new TValue(time, 100.0));
            // Correct it
            indicator.Update(new TValue(time, v), isNew: false);
            time = time.AddMinutes(1);
        }

        Assert.Equal(finalResult, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        indicator.Update(new TValue(time.AddMinutes(1), 5.0));
        double beforeNaN = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(2), double.NaN));
        Assert.Equal(beforeNaN, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 15.0));
        indicator.Update(new TValue(time.AddMinutes(1), double.NegativeInfinity));
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 4; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), i));
            Assert.False(indicator.IsHot);
        }

        indicator.Update(new TValue(time.AddMinutes(4), 4));
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), i * 2));
        }

        Assert.True(indicator.IsHot);
        indicator.Reset();
        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    [Fact]
    public void Pub_EventFires()
    {
        var indicator = new Lowest(5);
        int eventCount = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        indicator.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        var source = new TSeries();
        var indicator = new Lowest(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);

        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), 5.0), true);
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Calculate_TSeries_MatchesStreaming()
    {
        int period = 5;
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 10002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Lowest(period);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamingResults.Add(streaming.Last.Value);
        }

        // Batch
        var batch = Lowest.Batch(source, period);

        // Compare last values (after warmup)
        for (int i = period; i < source.Count; i++)
        {
            Assert.Equal(streamingResults[i], batch[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        int period = 5;
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 10003);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // TSeries batch
        var batchResult = Lowest.Batch(source, period);

        // Span calculation
        var values = source.Values.ToArray();
        var output = new double[count];
        Lowest.Batch(values, output, period);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], Tolerance);
        }
    }

    [Fact]
    public void Calculate_Span_ValidatesArguments()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Span<double> output = stackalloc double[10];
            Lowest.Batch(ReadOnlySpan<double>.Empty, output, 5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[5];
            Lowest.Batch(source, output, 5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[10];
            Lowest.Batch(source, output, 0);
        });
    }

    [Fact]
    public void MonotonicSequence_Descending_ReturnsLatest()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;

        for (int i = 10; i >= 1; i--)
        {
            indicator.Update(new TValue(time.AddMinutes(10 - i), i));
            Assert.Equal(i, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void MonotonicSequence_Ascending_ReturnsFirst()
    {
        var indicator = new Lowest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 1.0));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);

        for (int i = 1; i < 5; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 1.0 + i));
            Assert.Equal(1.0, indicator.Last.Value, Tolerance);
        }

        // After 5 values, 1.0 drops out
        indicator.Update(new TValue(time.AddMinutes(5), 6.0));
        Assert.Equal(2.0, indicator.Last.Value, Tolerance);
    }
}
