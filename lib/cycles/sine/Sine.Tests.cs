namespace QuanTAlib.Tests;

using Xunit;

public class SineTests
{
    private const double Tolerance = 1e-9;
    private readonly GBM _gbm;

    public SineTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
    }

    private TBarSeries GenerateBars(int count)
    {
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromDays(1));
    }

    [Fact]
    public void Sine_ConstructorDefaults()
    {
        var sine = new Sine();
        Assert.Equal("SINE", sine.Name);
        Assert.Equal(40, sine.HpPeriod);
        Assert.Equal(10, sine.SsfPeriod);
        Assert.Equal(48, sine.WarmupPeriod); // max(40, 10) + 8
        Assert.False(sine.IsHot);
    }

    [Fact]
    public void Sine_ConstructorCustomParameters()
    {
        var sine = new Sine(hpPeriod: 20, ssfPeriod: 5);
        Assert.Equal(20, sine.HpPeriod);
        Assert.Equal(5, sine.SsfPeriod);
        Assert.Equal(28, sine.WarmupPeriod); // max(20, 5) + 8
    }

    [Fact]
    public void Sine_ConstructorValidation_ThrowsOnInvalidHpPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sine(hpPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sine(hpPeriod: -1));
    }

    [Fact]
    public void Sine_ConstructorValidation_ThrowsOnInvalidSsfPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sine(ssfPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sine(ssfPeriod: -1));
    }

    [Fact]
    public void Sine_Update_ReturnsValidRange()
    {
        var sine = new Sine();
        var bars = GenerateBars(200);

        foreach (var bar in bars)
        {
            var result = sine.Update(new TValue(bar.Time, bar.Close));
            Assert.True(result.Value >= -1.0 && result.Value <= 1.0,
                $"Sine value {result.Value} out of range [-1, 1]");
        }
    }

    [Fact]
    public void Sine_IsHot_AfterWarmup()
    {
        var sine = new Sine(hpPeriod: 20, ssfPeriod: 5);
        var bars = GenerateBars(50);

        for (int i = 0; i < bars.Count; i++)
        {
            sine.Update(new TValue(bars[i].Time, bars[i].Close));

            if (i + 1 < sine.WarmupPeriod)
            {
                Assert.False(sine.IsHot, $"Should not be hot at index {i}");
            }
            else
            {
                Assert.True(sine.IsHot, $"Should be hot at index {i}");
            }
        }
    }

    [Fact]
    public void Sine_IsNew_AdvancesState()
    {
        var sine = new Sine();
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result1 = sine.Update(input, isNew: true);
        var result2 = sine.Update(new TValue(DateTime.UtcNow.AddDays(1), 101.0), isNew: true);

        // With isNew=true, each call should advance state
        // Values might be the same early on, but state should advance
        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Sine_IsNew_False_UpdatesCurrentBar()
    {
        var sine = new Sine();
        var bars = GenerateBars(60);

        // Process first 50 bars normally
        for (int i = 0; i < 50; i++)
        {
            sine.Update(new TValue(bars[i].Time, bars[i].Close), isNew: true);
        }

        // Get result at bar 50
        var newBarResult = sine.Update(new TValue(bars[50].Time, bars[50].Close), isNew: true);

        // Reset and replay to bar 49, then update bar 50 with different value
        var sine2 = new Sine();
        for (int i = 0; i < 50; i++)
        {
            sine2.Update(new TValue(bars[i].Time, bars[i].Close), isNew: true);
        }

        // First update bar 50
        sine2.Update(new TValue(bars[50].Time, bars[50].Close), isNew: true);

        // Update same bar with different value (bar correction)
        var correctedResult = sine2.Update(new TValue(bars[50].Time, bars[50].Close * 1.1), isNew: false);

        // Results should differ due to different input
        Assert.NotEqual(newBarResult.Value, correctedResult.Value);
    }

    [Fact]
    public void Sine_Reset_ClearsState()
    {
        var sine = new Sine();
        var bars = GenerateBars(100);

        foreach (var bar in bars)
        {
            sine.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(sine.IsHot);

        sine.Reset();

        Assert.False(sine.IsHot);
        Assert.Equal(0, sine.Last.Value);
    }

    [Fact]
    public void Sine_TSeries_Update()
    {
        var bars = GenerateBars(100);
        var series = new TSeries(100);

        foreach (var bar in bars)
        {
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var sine = new Sine();
        var result = sine.Update(series);

        Assert.Equal(100, result.Count);

        // Verify all values are in range
        foreach (var val in result)
        {
            Assert.True(val.Value >= -1.0 && val.Value <= 1.0);
        }
    }

    [Fact]
    public void Sine_StaticCalculate_TSeries()
    {
        var bars = GenerateBars(100);
        var series = new TSeries(100);

        foreach (var bar in bars)
        {
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var result = Sine.Calculate(series);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void Sine_StaticCalculate_WithCustomParams()
    {
        var bars = GenerateBars(100);
        var series = new TSeries(100);

        foreach (var bar in bars)
        {
            series.Add(new TValue(bar.Time, bar.Close));
        }

        var result = Sine.Calculate(series, hpPeriod: 20, ssfPeriod: 5);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public void Sine_Chaining_Works()
    {
        var source = new Sma(10);
        var sine = new Sine(source);

        bool eventFired = false;
        sine.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var input = new TValue(DateTime.UtcNow, 100.0);
        source.Update(input);

        Assert.True(eventFired);
    }

    [Fact]
    public void Sine_EmptyTSeries_ReturnsEmpty()
    {
        var sine = new Sine();
        var empty = new TSeries();
        var result = sine.Update(empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Sine_Streaming_MatchesBatch()
    {
        var bars = GenerateBars(200);
        var series = new TSeries(200);

        foreach (var bar in bars)
        {
            series.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming calculation
        var streamingSine = new Sine();
        var streamingResults = new List<double>();
        foreach (var bar in bars)
        {
            var result = streamingSine.Update(new TValue(bar.Time, bar.Close));
            streamingResults.Add(result.Value);
        }

        // Batch calculation
        var batchResult = Sine.Calculate(series);

        // Compare last 100 values (after warmup)
        for (int i = 100; i < 200; i++)
        {
            Assert.Equal(streamingResults[i], batchResult[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Sine_NaN_HandledGracefully()
    {
        var sine = new Sine();
        var bars = GenerateBars(60);

        // Process some bars
        for (int i = 0; i < 50; i++)
        {
            sine.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed NaN - should substitute with last valid value
        var nanResult = sine.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should NOT be NaN (last-valid substitution) and in valid range
        Assert.False(double.IsNaN(nanResult.Value), "NaN should not propagate");
        Assert.True(nanResult.Value >= -1.0 && nanResult.Value <= 1.0,
            $"Value {nanResult.Value} should be in [-1, 1]");
    }

    [Fact]
    public void Sine_Prime_InitializesState()
    {
        var bars = GenerateBars(100);
        var primeData = bars.Select(b => b.Close).ToArray();

        var sine = new Sine();
        sine.Prime(primeData);

        Assert.True(sine.IsHot);
    }

    [Fact]
    public void Sine_WithCyclingData_ProducesOscillation()
    {
        var sine = new Sine(hpPeriod: 20, ssfPeriod: 5);

        // Generate sinusoidal price data
        var results = new List<double>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 200; i++)
        {
            // Create a price with embedded 30-bar cycle
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 30.0);
            var result = sine.Update(new TValue(baseTime.AddDays(i), price));
            results.Add(result.Value);
        }

        // After warmup, check that we have both positive and negative values
        var afterWarmup = results.Skip(30).ToList();
        Assert.True(afterWarmup.Any(v => v > 0.5), "Should have positive cycle values");
        Assert.True(afterWarmup.Any(v => v < -0.5), "Should have negative cycle values");
    }

    [Fact]
    public void Sine_ConstantInput_ProducesValidOutput()
    {
        var sine = new Sine();
        var baseTime = DateTime.UtcNow;

        // Feed constant values
        for (int i = 0; i < 200; i++)
        {
            var result = sine.Update(new TValue(baseTime.AddDays(i), 100.0));
            // Output should always be in valid range regardless of input
            Assert.True(result.Value >= -1.0 && result.Value <= 1.0,
                $"Value {result.Value} out of range at index {i}");
        }
    }

    [Fact]
    public void Sine_TrendingInput_ProducesValidOutput()
    {
        var sine = new Sine(hpPeriod: 40, ssfPeriod: 10);
        var baseTime = DateTime.UtcNow;

        // Feed trending data (very low frequency)
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + i * 0.1; // Slow uptrend
            var result = sine.Update(new TValue(baseTime.AddDays(i), price));
            // Output should always be in valid range regardless of input
            Assert.True(result.Value >= -1.0 && result.Value <= 1.0,
                $"Value {result.Value} out of range at index {i}");
        }
    }
}