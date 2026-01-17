using Xunit;

namespace QuanTAlib.Tests;

public class ReluTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_SetsProperties()
    {
        var indicator = new Relu();
        Assert.Equal("ReLU", indicator.Name);
        Assert.Equal(0, indicator.WarmupPeriod);
        Assert.True(indicator.IsHot);  // Always hot (no warmup)
    }

    [Fact]
    public void Update_ReturnsRelu()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 5.0));
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);  // max(0, 5) = 5

        indicator.Update(new TValue(time.AddMinutes(1), -3.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);  // max(0, -3) = 0

        indicator.Update(new TValue(time.AddMinutes(2), 0.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);  // max(0, 0) = 0

        indicator.Update(new TValue(time.AddMinutes(3), 100.5));
        Assert.Equal(100.5, indicator.Last.Value, Tolerance);  // max(0, 100.5) = 100.5
    }

    [Fact]
    public void Update_KnownValues()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        // Positive values pass through
        indicator.Update(new TValue(time, 10.0));
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);

        // Negative values become zero
        indicator.Update(new TValue(time.AddMinutes(1), -10.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);

        // Zero stays zero
        indicator.Update(new TValue(time.AddMinutes(2), 0.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);

        // Small positive value
        indicator.Update(new TValue(time.AddMinutes(3), 0.001));
        Assert.Equal(0.001, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsPreviousValue()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 5.0));
        indicator.Update(new TValue(time.AddMinutes(1), -2.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);

        // Correct last value
        indicator.Update(new TValue(time.AddMinutes(1), 3.0), isNew: false);
        Assert.Equal(3.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;
        double[] values = { 5.0, -3.0, 2.5, -1.0, 0.0, 8.0, -5.0 };

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
            indicator.Update(new TValue(time, 999.0));
            // Correct it
            indicator.Update(new TValue(time, v), isNew: false);
            time = time.AddMinutes(1);
        }

        Assert.Equal(finalResult, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 7.0));
        double beforeNaN = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.NaN));
        Assert.Equal(beforeNaN, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 3.5));
        double beforeInf = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));
        Assert.Equal(beforeInf, indicator.Last.Value, Tolerance);

        indicator.Update(new TValue(time.AddMinutes(2), double.NegativeInfinity));
        Assert.Equal(beforeInf, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), i - 5));
        }

        Assert.True(indicator.IsHot);
        indicator.Reset();
        Assert.True(indicator.IsHot);  // Still hot (no warmup)
        Assert.Equal(default, indicator.Last);
    }

    [Fact]
    public void Pub_EventFires()
    {
        var indicator = new Relu();
        int eventCount = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        indicator.Update(new TValue(DateTime.UtcNow, 5.0));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        var source = new TSeries();
        var indicator = new Relu(source);

        source.Add(new TValue(DateTime.UtcNow, 5.0), true);
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);

        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), -3.0), true);
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Calculate_TSeries_MatchesStreaming()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        // Use returns (which can be negative) for meaningful ReLU test
        var source = Change.Calculate(bars.Close);

        // Streaming
        var streaming = new Relu();
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamingResults.Add(streaming.Last.Value);
        }

        // Batch
        var batch = Relu.Calculate(source);

        // Compare all values
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamingResults[i], batch[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = Change.Calculate(bars.Close);

        // TSeries batch
        var batchResult = Relu.Calculate(source);

        // Span calculation
        var values = source.Values.ToArray();
        var output = new double[count];
        Relu.Calculate(values, output);

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
            Relu.Calculate(ReadOnlySpan<double>.Empty, output);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[5];
            Relu.Calculate(source, output);
        });
    }

    [Fact]
    public void Relu_PositivePassthrough()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        // All positive values should pass through unchanged
        for (double v = 0.1; v <= 100.0; v += 10.0)
        {
            indicator.Update(new TValue(time, v));
            Assert.Equal(v, indicator.Last.Value, Tolerance);
            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Relu_NegativeBecomesZero()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        // All negative values should become zero
        for (double v = -0.1; v >= -100.0; v -= 10.0)
        {
            indicator.Update(new TValue(time, v));
            Assert.Equal(0.0, indicator.Last.Value, Tolerance);
            time = time.AddMinutes(1);
        }
    }

    [Fact]
    public void Relu_AlwaysNonNegative()
    {
        var indicator = new Relu();
        var time = DateTime.UtcNow;

        // ReLU output should always be >= 0
        for (int i = -50; i <= 50; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i + 50), i));
            Assert.True(indicator.Last.Value >= 0);
        }
    }
}
