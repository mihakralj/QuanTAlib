using Xunit;

namespace QuanTAlib.Tests;

public class MidpointTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Midpoint(0));
        Assert.Throws<ArgumentException>(() => new Midpoint(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var indicator = new Midpoint(14);
        Assert.Equal("Midpoint(14)", indicator.Name);
        Assert.Equal(14, indicator.WarmupPeriod);
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Update_ReturnsMidpointInWindow()
    {
        var indicator = new Midpoint(3);
        var time = DateTime.UtcNow;

        // Single value: midpoint = (5+5)/2 = 5
        indicator.Update(new TValue(time, 5.0));
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);

        // Window [5, 8]: midpoint = (8+5)/2 = 6.5
        indicator.Update(new TValue(time.AddMinutes(1), 8.0));
        Assert.Equal(6.5, indicator.Last.Value, Tolerance);

        // Window [5, 8, 3]: midpoint = (8+3)/2 = 5.5
        indicator.Update(new TValue(time.AddMinutes(2), 3.0));
        Assert.Equal(5.5, indicator.Last.Value, Tolerance);

        // Window [8, 3, 2]: midpoint = (8+2)/2 = 5.0
        indicator.Update(new TValue(time.AddMinutes(3), 2.0));
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);

        // Window [3, 2, 10]: midpoint = (10+2)/2 = 6.0
        indicator.Update(new TValue(time.AddMinutes(4), 10.0));
        Assert.Equal(6.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Period1_ReturnsSameValue()
    {
        var indicator = new Midpoint(1);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            double value = i * 2.5;
            indicator.Update(new TValue(time.AddMinutes(i), value));
            // Midpoint of single value = that value
            Assert.Equal(value, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsPreviousValue()
    {
        var indicator = new Midpoint(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        indicator.Update(new TValue(time.AddMinutes(1), 20.0));
        indicator.Update(new TValue(time.AddMinutes(2), 15.0));
        // Window [10, 20, 15]: midpoint = (20+10)/2 = 15.0
        Assert.Equal(15.0, indicator.Last.Value, Tolerance);

        // Correct last value to 5.0
        // Window [10, 20, 5]: midpoint = (20+5)/2 = 12.5
        indicator.Update(new TValue(time.AddMinutes(2), 5.0), isNew: false);
        Assert.Equal(12.5, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var indicator = new Midpoint(5);
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
        var indicator = new Midpoint(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        indicator.Update(new TValue(time.AddMinutes(1), 20.0));
        double beforeNaN = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(2), double.NaN));
        // Should use last valid value
        Assert.Equal(beforeNaN, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Midpoint(5);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 15.0));
        indicator.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new Midpoint(5);
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
        var indicator = new Midpoint(5);
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
        var indicator = new Midpoint(5);
        int eventCount = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        indicator.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        var source = new TSeries();
        var indicator = new Midpoint(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 10.0), true);
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);

        // Window [10, 20]: midpoint = (20+10)/2 = 15
        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), 20.0), true);
        Assert.Equal(15.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Calculate_TSeries_MatchesStreaming()
    {
        int period = 5;
        int count = 50;
        var gbm = new GBM(10000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Midpoint(period);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamingResults.Add(streaming.Last.Value);
        }

        // Batch
        var batch = Midpoint.Batch(source, period);

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
        var gbm = new GBM(10001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // TSeries batch
        var batchResult = Midpoint.Batch(source, period);

        // Span calculation
        var sourceArray = source.Values.ToArray();
        var output = new double[count];
        Midpoint.Batch(sourceArray.AsSpan(), output.AsSpan(), period);

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
            Midpoint.Batch(ReadOnlySpan<double>.Empty, output, 5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[5];
            Midpoint.Batch(source, output, 5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[10];
            Midpoint.Batch(source, output, 0);
        });
    }

    [Fact]
    public void ConstantSequence_ReturnsSameValue()
    {
        var indicator = new Midpoint(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), 7.5));
            // Midpoint of constant sequence = that constant
            Assert.Equal(7.5, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Midpoint_EqualsAverageOfHighestAndLowest()
    {
        int period = 5;
        var gbm = new GBM(12345);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var midpoint = new Midpoint(period);
        var highest = new Highest(period);
        var lowest = new Lowest(period);

        for (int i = 0; i < source.Count; i++)
        {
            midpoint.Update(source[i]);
            highest.Update(source[i]);
            lowest.Update(source[i]);

            double expected = (highest.Last.Value + lowest.Last.Value) * 0.5;
            Assert.Equal(expected, midpoint.Last.Value, Tolerance);
        }
    }
}
