using Xunit;

namespace QuanTAlib.Tests;

public class HighestTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Highest(0));
        Assert.Throws<ArgumentException>(() => new Highest(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var indicator = new Highest(14);
        Assert.Equal("Highest(14)", indicator.Name);
        Assert.Equal(14, indicator.WarmupPeriod);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Update_ReturnsHighestInWindow()
    {
        var indicator = new Highest(3);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 5.0));
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);

        indicator.Update(new TValue(time.AddMinutes(1), 8.0));
        Assert.Equal(8.0, indicator.Last.Value, Tolerance);

        indicator.Update(new TValue(time.AddMinutes(2), 3.0));
        Assert.Equal(8.0, indicator.Last.Value, Tolerance);

        // 5 drops out of window
        indicator.Update(new TValue(time.AddMinutes(3), 2.0));
        Assert.Equal(8.0, indicator.Last.Value, Tolerance);

        // 8 drops out of window
        indicator.Update(new TValue(time.AddMinutes(4), 4.0));
        Assert.Equal(4.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Period1_ReturnsSameValue()
    {
        var indicator = new Highest(1);
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
        var indicator = new Highest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        indicator.Update(new TValue(time.AddMinutes(1), 20.0));
        indicator.Update(new TValue(time.AddMinutes(2), 15.0));
        Assert.Equal(20.0, indicator.Last.Value, Tolerance);

        // Correct last value to be the new max
        indicator.Update(new TValue(time.AddMinutes(2), 25.0), isNew: false);
        Assert.Equal(25.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var indicator = new Highest(5);
        var time = DateTime.UtcNow;
        double[] values = { 5.0, 10.0, 8.0, 12.0, 7.0, 15.0, 11.0 };

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
            indicator.Update(new TValue(time, 0.0));
            // Correct it
            indicator.Update(new TValue(time, v), isNew: false);
            time = time.AddMinutes(1);
        }

        Assert.Equal(finalResult, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Highest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        indicator.Update(new TValue(time.AddMinutes(1), 20.0));
        double beforeNaN = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(2), double.NaN));
        // Should use last valid, which was 20.0
        Assert.Equal(beforeNaN, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Highest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 15.0));
        indicator.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));
        // Should use last valid value (15.0) instead of infinity
        Assert.Equal(15.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new Highest(5);
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
        var indicator = new Highest(5);
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
        var indicator = new Highest(5);
        int eventCount = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        indicator.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        var source = new TSeries();
        var indicator = new Highest(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);

        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), 20.0), true);
        Assert.Equal(20.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Calculate_TSeries_MatchesStreaming()
    {
        int period = 5;
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 10000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Highest(period);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamingResults.Add(streaming.Last.Value);
        }

        // Batch
        var batch = Highest.Calculate(source, period);

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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 10001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // TSeries batch
        var batchResult = Highest.Calculate(source, period);

        // Span calculation
        var values = source.Values.ToArray();
        var output = new double[count];
        Highest.Calculate(values, output, period);

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
            Highest.Calculate(ReadOnlySpan<double>.Empty, output, 5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[5];
            Highest.Calculate(source, output, 5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[10];
            Highest.Calculate(source, output, 0);
        });
    }

    [Fact]
    public void MonotonicSequence_Ascending_ReturnsLatest()
    {
        var indicator = new Highest(5);
        var time = DateTime.UtcNow;

        for (int i = 1; i <= 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), i));
            Assert.Equal(i, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void MonotonicSequence_Descending_ReturnsFirst()
    {
        var indicator = new Highest(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);

        for (int i = 1; i < 5; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 10.0 - i));
            Assert.Equal(10.0, indicator.Last.Value, Tolerance);
        }

        // After 5 values, 10.0 drops out
        indicator.Update(new TValue(time.AddMinutes(5), 5.0));
        Assert.Equal(9.0, indicator.Last.Value, Tolerance);
    }
}