using Xunit;

namespace QuanTAlib.Tests;

public class ExptransTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Exptrans_Constructor_SetsProperties()
    {
        var indicator = new Exptrans();
        Assert.Equal("Exptrans", indicator.Name);
        Assert.Equal(0, indicator.WarmupPeriod);
        Assert.True(indicator.IsHot);  // Always hot (no warmup)
    }

    [Fact]
    public void Exptrans_Update_ReturnsExponential()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 0.0));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);  // exp(0) = 1

        indicator.Update(new TValue(time.AddMinutes(1), 1.0));
        Assert.Equal(Math.E, indicator.Last.Value, Tolerance);  // exp(1) = e

        indicator.Update(new TValue(time.AddMinutes(2), 2.0));
        Assert.Equal(Math.E * Math.E, indicator.Last.Value, Tolerance);  // exp(2) = e^2

        indicator.Update(new TValue(time.AddMinutes(3), -1.0));
        Assert.Equal(1.0 / Math.E, indicator.Last.Value, Tolerance);  // exp(-1) = 1/e
    }

    [Fact]
    public void Exptrans_Update_KnownValues()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        // exp(0) = 1
        indicator.Update(new TValue(time, 0.0));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);

        // exp(ln(10)) = 10
        indicator.Update(new TValue(time.AddMinutes(1), Math.Log(10.0)));
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);

        // exp(ln(0.5)) = 0.5
        indicator.Update(new TValue(time.AddMinutes(2), Math.Log(0.5)));
        Assert.Equal(0.5, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Exptrans_Update_IsNewFalse_CorrectsPreviousValue()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 1.0));
        indicator.Update(new TValue(time.AddMinutes(1), 2.0));
        Assert.Equal(Math.Exp(2.0), indicator.Last.Value, Tolerance);

        // Correct last value
        indicator.Update(new TValue(time.AddMinutes(1), 3.0), isNew: false);
        Assert.Equal(Math.Exp(3.0), indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Exptrans_Update_IterativeCorrection_RestoresState()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;
        double[] values = { 0.5, 1.0, 0.8, 1.2, 0.7, 1.5, 1.1 };

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
    public void Exptrans_Update_NaN_UsesLastValidValue()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 2.0));
        double beforeNaN = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.NaN));
        Assert.Equal(beforeNaN, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Exptrans_Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 1.5));
        double beforeInf = indicator.Last.Value;

        indicator.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));
        Assert.Equal(beforeInf, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Exptrans_Update_LargeInput_HandlesOverflow()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 5.0));
        double validResult = indicator.Last.Value;

        // exp(1000) overflows to infinity
        indicator.Update(new TValue(time.AddMinutes(1), 1000.0));
        // Should use last valid value
        Assert.Equal(validResult, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Exptrans_Reset_ClearsState()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), i * 0.1));
        }

        Assert.True(indicator.IsHot);
        indicator.Reset();
        Assert.True(indicator.IsHot);  // Still hot (no warmup)
        Assert.Equal(default, indicator.Last);
    }

    [Fact]
    public void Exptrans_Pub_EventFires()
    {
        var indicator = new Exptrans();
        int eventCount = 0;
        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        indicator.Update(new TValue(DateTime.UtcNow, 1.0));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Exptrans_Chaining_Constructor_Works()
    {
        var source = new TSeries();
        var indicator = new Exptrans(source);

        source.Add(new TValue(DateTime.UtcNow, 0.0), true);
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);  // exp(0) = 1

        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), 1.0), true);
        Assert.Equal(Math.E, indicator.Last.Value, Tolerance);  // exp(1) = e
    }

    [Fact]
    public void Exptrans_Calculate_TSeries_MatchesStreaming()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 40000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        // Use log of close prices to stay in reasonable exp range
        var logSource = Logtrans.Batch(bars.Close);

        // Streaming
        var streaming = new Exptrans();
        var streamingResults = new List<double>();
        for (int i = 0; i < logSource.Count; i++)
        {
            streaming.Update(logSource[i]);
            streamingResults.Add(streaming.Last.Value);
        }

        // Batch
        var batch = Exptrans.Batch(logSource);

        // Compare all values
        for (int i = 0; i < logSource.Count; i++)
        {
            Assert.Equal(streamingResults[i], batch[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Exptrans_Calculate_Span_MatchesTSeries()
    {
        int count = 50;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 40001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var logSource = Logtrans.Batch(bars.Close);

        // TSeries batch
        var batchResult = Exptrans.Batch(logSource);

        // Span calculation
        var values = logSource.Values.ToArray();
        var output = new double[count];
        Exptrans.Batch(values, output);

        for (int i = 0; i < logSource.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], Tolerance);
        }
    }

    [Fact]
    public void Exptrans_Calculate_Span_ValidatesArguments()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Span<double> output = stackalloc double[10];
            Exptrans.Batch(ReadOnlySpan<double>.Empty, output);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[10];
            Span<double> output = stackalloc double[5];
            Exptrans.Batch(source, output);
        });
    }

    [Fact]
    public void Exptrans_LogInverse_ReturnsOriginal()
    {
        var exp = new Exptrans();
        var time = DateTime.UtcNow;
        double logValue = 3.5;

        exp.Update(new TValue(time, logValue));
        double expResult = exp.Last.Value;

        // log(exp(x)) should equal x
        Assert.Equal(logValue, Math.Log(expResult), Tolerance);
    }

    [Fact]
    public void Exptrans_Negative_ReturnsPositive()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        // exp(x) is always positive for any finite x
        for (int i = -10; i <= 10; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i + 10), i));
            Assert.True(indicator.Last.Value > 0);
        }
    }
}
