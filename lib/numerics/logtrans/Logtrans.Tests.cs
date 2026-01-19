using Xunit;

namespace QuanTAlib.Tests;

public class LogtransTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Constructor_SetsProperties()
    {
        var indicator = new Logtrans();
        Assert.Equal("Logtrans", indicator.Name);
        Assert.Equal(0, indicator.WarmupPeriod);
        Assert.True(indicator.IsHot);  // Always hot (no warmup)
    }

    [Fact]
    public void Update_ReturnsNaturalLog()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 1.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);  // ln(1) = 0

        indicator.Update(new TValue(time.AddMinutes(1), Math.E));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);  // ln(e) = 1

        indicator.Update(new TValue(time.AddMinutes(2), Math.E * Math.E));
        Assert.Equal(2.0, indicator.Last.Value, Tolerance);  // ln(e^2) = 2

        indicator.Update(new TValue(time.AddMinutes(3), 10.0));
        Assert.Equal(Math.Log(10.0), indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_KnownValues()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        // ln(100) ≈ 4.605
        indicator.Update(new TValue(time, 100.0));
        Assert.Equal(Math.Log(100.0), indicator.Last.Value, Tolerance);

        // ln(0.5) ≈ -0.693
        indicator.Update(new TValue(time.AddMinutes(1), 0.5));
        Assert.Equal(Math.Log(0.5), indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsPreviousValue()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        indicator.Update(new TValue(time.AddMinutes(1), 20.0));
        Assert.Equal(Math.Log(20.0), indicator.Last.Value, Tolerance);

        // Correct last value
        indicator.Update(new TValue(time.AddMinutes(1), 100.0), isNew: false);
        Assert.Equal(Math.Log(100.0), indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_IterativeCorrection_RestoresState()
    {
        var indicator = new Logtrans();
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
            indicator.Update(new TValue(time, 1.0));
            // Correct it
            indicator.Update(new TValue(time, v), isNew: false);
            time = time.AddMinutes(1);
        }

        Assert.Equal(finalResult, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        double beforeNaN = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.NaN));
        Assert.Equal(beforeNaN, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 15.0));
        double beforeInf = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));
        Assert.Equal(beforeInf, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_NonPositive_UsesLastValidValue()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 10.0));
        double beforeZero = indicator.Last.Value;

        // Zero
        indicator.Update(new TValue(time.AddMinutes(1), 0.0));
        Assert.Equal(beforeZero, indicator.Last.Value, Tolerance);

        // Negative
        indicator.Update(new TValue(time.AddMinutes(2), -5.0));
        Assert.Equal(beforeZero, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        for (int i = 1; i <= 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), i * 2.0));
        }

        Assert.True(indicator.IsHot);
        indicator.Reset();
        Assert.True(indicator.IsHot);  // Still hot (no warmup)
        Assert.Equal(default, indicator.Last);
    }

    [Fact]
    public void Pub_EventFires()
    {
        var indicator = new Logtrans();
        int eventCount = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        indicator.Update(new TValue(DateTime.UtcNow, 10.0));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Chaining_Constructor_Works()
    {
        var source = new TSeries();
        var indicator = new Logtrans(source);

        source.Add(new TValue(DateTime.UtcNow, Math.E), true);
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);

        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), Math.E * Math.E), true);
        Assert.Equal(2.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Calculate_TSeries_MatchesStreaming()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 20000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Streaming
        var streaming = new Logtrans();
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streaming.Update(source[i]);
            streamingResults.Add(streaming.Last.Value);
        }

        // Batch
        var batch = Logtrans.Calculate(source);

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
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 20001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // TSeries batch
        var batchResult = Logtrans.Calculate(source);

        // Span calculation
        var values = source.Values.ToArray();
        var output = new double[count];
        Logtrans.Calculate(values, output);

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
            Logtrans.Calculate(ReadOnlySpan<double>.Empty, output);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[5];
            Logtrans.Calculate(source, output);
        });
    }

    [Fact]
    public void LogtransExptransInverse_ReturnsOriginal()
    {
        var logtrans = new Logtrans();
        var time = DateTime.UtcNow;
        double original = 42.0;

        logtrans.Update(new TValue(time, original));
        double logtransResult = logtrans.Last.Value;

        // exp(logtrans(x)) should equal x
        Assert.Equal(original, Math.Exp(logtransResult), Tolerance);
    }
}