using Xunit;

namespace QuanTAlib.Tests;

public class SqrttransTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_SetsProperties()
    {
        var indicator = new Sqrttrans();
        Assert.Equal("Sqrttrans", indicator.Name);
        Assert.Equal(0, indicator.WarmupPeriod);
        Assert.True(indicator.IsHot);  // Always hot (no warmup)
    }

    [Fact]
    public void Update_ReturnsSquareRoot()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 0.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);  // sqrt(0) = 0

        indicator.Update(new TValue(time.AddMinutes(1), 1.0));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);  // sqrt(1) = 1

        indicator.Update(new TValue(time.AddMinutes(2), 4.0));
        Assert.Equal(2.0, indicator.Last.Value, Tolerance);  // sqrt(4) = 2

        indicator.Update(new TValue(time.AddMinutes(3), 9.0));
        Assert.Equal(3.0, indicator.Last.Value, Tolerance);  // sqrt(9) = 3
    }

    [Fact]
    public void Update_KnownValues()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        // sqrt(0) = 0
        indicator.Update(new TValue(time, 0.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);

        // sqrt(2) ≈ 1.414
        indicator.Update(new TValue(time.AddMinutes(1), 2.0));
        Assert.Equal(Math.Sqrt(2.0), indicator.Last.Value, Tolerance);

        // sqrt(100) = 10
        indicator.Update(new TValue(time.AddMinutes(2), 100.0));
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);

        // sqrt(0.25) = 0.5
        indicator.Update(new TValue(time.AddMinutes(3), 0.25));
        Assert.Equal(0.5, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsPreviousValue()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 4.0));
        indicator.Update(new TValue(time.AddMinutes(1), 9.0));
        Assert.Equal(3.0, indicator.Last.Value, Tolerance);

        // Correct last value
        indicator.Update(new TValue(time.AddMinutes(1), 16.0), isNew: false);
        Assert.Equal(4.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;
        double[] values = { 1.0, 4.0, 9.0, 16.0, 25.0, 36.0, 49.0 };

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
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 16.0));
        double beforeNaN = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.NaN));
        Assert.Equal(beforeNaN, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 25.0));
        double beforeInf = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));
        Assert.Equal(beforeInf, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NegativeInput_UsesLastValidValue()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 25.0));
        double beforeNeg = indicator.Last.Value;

        // sqrt of negative is undefined - should use last valid
        indicator.Update(new TValue(time.AddMinutes(1), -4.0));
        Assert.Equal(beforeNeg, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), (i + 1) * (i + 1)));
        }

        Assert.True(indicator.IsHot);
        indicator.Reset();
        Assert.True(indicator.IsHot);  // Still hot (no warmup)
        Assert.Equal(default, indicator.Last);
    }

    [Fact]
    public void Pub_EventFires()
    {
        var indicator = new Sqrttrans();
        int eventCount = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        indicator.Update(new TValue(DateTime.UtcNow, 4.0));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        var source = new TSeries();
        var indicator = new Sqrttrans(source);

        source.Add(new TValue(DateTime.UtcNow, 0.0), true);
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);  // sqrt(0) = 0

        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), 4.0), true);
        Assert.Equal(2.0, indicator.Last.Value, Tolerance);  // sqrt(4) = 2
    }

    [Fact]
    public void Calculate_TSeries_MatchesStreaming()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 40000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;  // Prices are always positive

        // Streaming
        var streaming = new Sqrttrans();
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamingResults.Add(streaming.Last.Value);
        }

        // Batch
        var batch = Sqrttrans.Calculate(source);

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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 40001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // TSeries batch
        var batchResult = Sqrttrans.Calculate(source);

        // Span calculation
        var values = source.Values.ToArray();
        var output = new double[count];
        Sqrttrans.Calculate(values, output);

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
            Sqrttrans.Calculate(ReadOnlySpan<double>.Empty, output);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[5];
            Sqrttrans.Calculate(source, output);
        });
    }

    [Fact]
    public void Sqrttrans_Squared_ReturnsOriginal()
    {
        var sqrt = new Sqrttrans();
        var time = DateTime.UtcNow;
        double value = 25.0;

        sqrt.Update(new TValue(time, value));
        double sqrtResult = sqrt.Last.Value;

        // (sqrt(x))^2 should equal x
        Assert.Equal(value, sqrtResult * sqrtResult, Tolerance);
    }

    [Fact]
    public void Sqrttrans_ProductRule()
    {
        // sqrt(a * b) = sqrt(a) * sqrt(b)
        double a = 4.0;
        double b = 9.0;

        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double sqrtA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double sqrtB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a * b));
        double sqrtAB = indicator.Last.Value;

        Assert.Equal(sqrtA * sqrtB, sqrtAB, Tolerance);
    }

    [Fact]
    public void Sqrttrans_QuotientRule()
    {
        // sqrt(a / b) = sqrt(a) / sqrt(b)
        double a = 16.0;
        double b = 4.0;

        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double sqrtA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double sqrtB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a / b));
        double sqrtAOverB = indicator.Last.Value;

        Assert.Equal(sqrtA / sqrtB, sqrtAOverB, Tolerance);
    }

    [Fact]
    public void Sqrttrans_AlwaysPositive()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        // sqrt(x) is always non-negative for valid inputs
        for (int i = 0; i <= 100; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), i));
            Assert.True(indicator.Last.Value >= 0);
        }
    }
}