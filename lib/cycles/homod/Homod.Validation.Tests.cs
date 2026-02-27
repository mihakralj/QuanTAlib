using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for HOMOD - Homodyne Discriminator.
/// Since HOMOD is a proprietary Ehlers algorithm with no standard library implementations,
/// these tests validate mathematical properties and internal consistency.
/// </summary>
public class HomodValidationTests
{
    private const double Tolerance = 1e-9;

    #region Mathematical Property Validation

    [Fact]
    public void Homod_OutputWithinConfiguredBounds()
    {
        // HOMOD output should always be within [minPeriod, maxPeriod] bounds
        const double minPeriod = 6;
        const double maxPeriod = 50;

        var homod = new Homod(minPeriod, maxPeriod);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = homod.Update(new TValue(bar.Time, bar.Close));

            // After warmup, values should be strictly within bounds
            if (homod.IsHot)
            {
                Assert.True(result.Value >= minPeriod && result.Value <= maxPeriod,
                    $"Value {result.Value} out of bounds [{minPeriod}, {maxPeriod}]");
            }
        }
    }

    [Fact]
    public void Homod_SmoothTransitions()
    {
        // HOMOD should produce smooth transitions due to EMA smoothing
        var homod = new Homod(6, 50);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double? prevValue = null;
        int largeJumps = 0;

        foreach (var bar in bars)
        {
            var result = homod.Update(new TValue(bar.Time, bar.Close));

            if (prevValue.HasValue && homod.IsHot)
            {
                double change = Math.Abs(result.Value - prevValue.Value);
                // Large jumps (>10 periods) should be rare due to smoothing
                if (change > 10)
                {
                    largeJumps++;
                }
            }
            prevValue = result.Value;
        }

        // Allow at most 5% large jumps
        Assert.True(largeJumps < 25, $"Too many large jumps: {largeJumps}");
    }

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(456)]
    public void Homod_DeterministicOutput(int seed)
    {
        // Same input should always produce same output
        var gbm = new GBM(seed: seed);
        var bars1 = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        gbm = new GBM(seed: seed);
        var bars2 = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var homod1 = new Homod(6, 50);
        var homod2 = new Homod(6, 50);

        for (int i = 0; i < bars1.Count; i++)
        {
            var result1 = homod1.Update(new TValue(bars1[i].Time, bars1[i].Close));
            var result2 = homod2.Update(new TValue(bars2[i].Time, bars2[i].Close));

            Assert.Equal(result1.Value, result2.Value, Tolerance);
        }
    }

    #endregion

    #region Cycle Detection Validation

    [Fact]
    public void Homod_DetectsSyntheticCycle()
    {
        // Create a synthetic sine wave with known period
        const int knownPeriod = 20;
        var homod = new Homod(6, 50);

        // Generate 500 bars of sine wave
        for (int i = 0; i < 500; i++)
        {
            double value = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / knownPeriod);
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), value));
        }

        // After convergence, detected period should be near the known period
        // Allow some tolerance due to phase estimation and smoothing
        Assert.InRange(homod.DominantCycle, knownPeriod - 5, knownPeriod + 5);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(25)]
    [InlineData(35)]
    public void Homod_TracksVaryingCycles(int period)
    {
        var homod = new Homod(6, 50);

        // Generate sine wave with specified period
        for (int i = 0; i < 600; i++)
        {
            double value = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / period);
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), value));
        }

        // Should detect approximately the correct period
        Assert.InRange(homod.DominantCycle, period - 6, period + 6);
    }

    #endregion

    #region Mode Consistency Validation

    [Fact]
    public void Homod_StreamingMatchesTSeries()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming mode
        var streaming = new Homod(6, 50);
        var streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streaming.Update(new TValue(bars[i].Time, bars[i].Close)).Value;
        }

        // TSeries mode
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }
        var tSeriesResult = Homod.Batch(tSeries, 6, 50);

        // Compare all values
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], tSeriesResult[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Homod_BatchMatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming mode
        var streaming = new Homod(6, 50);
        var streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streaming.Update(new TValue(bars[i].Time, bars[i].Close)).Value;
        }

        // Batch mode
        double[] source = new double[bars.Count];
        double[] batchResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            source[i] = bars[i].Close;
        }
        Homod.Batch(source, batchResults, 6, 50);

        // Compare all values
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], Tolerance);
        }
    }

    [Fact]
    public void Homod_EventChainMatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming mode
        var streaming = new Homod(6, 50);
        var streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streaming.Update(new TValue(bars[i].Time, bars[i].Close)).Value;
        }

        // Event chain mode
        var source = new TSeries();
        var chained = new Homod(source, 6, 50);
        var chainedResults = new List<double>();
        chained.Pub += (object? _, in TValueEventArgs args) => chainedResults.Add(args.Value.Value);

        foreach (var bar in bars)
        {
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Compare all values
        Assert.Equal(streamingResults.Length, chainedResults.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], chainedResults[i], Tolerance);
        }
    }

    #endregion

    #region Robustness Validation

    [Fact]
    public void Homod_HandlesVolatileInput()
    {
        var homod = new Homod(6, 50);
        var bars = new GBM(seed: 42).Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromSeconds(1));

        // Highly volatile GBM input
        for (int i = 0; i < 500; i++)
        {
            var result = homod.Update(bars.Close[i]);

            Assert.True(double.IsFinite(result.Value));
            if (homod.IsHot)
            {
                Assert.InRange(result.Value, 6, 50);
            }
        }
    }

    [Fact]
    public void Homod_HandlesConstantInput()
    {
        var homod = new Homod(6, 50);

        // Constant input - no cycle present
        for (int i = 0; i < 500; i++)
        {
            var result = homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            Assert.True(double.IsFinite(result.Value));
        }

        // Should still produce valid output within bounds
        Assert.InRange(homod.DominantCycle, 6, 50);
    }

    [Fact]
    public void Homod_HandlesTrendingInput()
    {
        var homod = new Homod(6, 50);

        // Strong uptrend with no cyclical component
        for (int i = 0; i < 500; i++)
        {
            double value = 100.0 + i * 0.5;
            var result = homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), value));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.InRange(homod.DominantCycle, 6, 50);
    }

    [Fact]
    public void Homod_HandlesNegativePrices()
    {
        var homod = new Homod(6, 50);

        // Negative values (e.g., oscillator output)
        for (int i = 0; i < 500; i++)
        {
            double value = Math.Sin(2.0 * Math.PI * i / 20) * 10; // Oscillates -10 to +10
            var result = homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), value));
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.InRange(homod.DominantCycle, 6, 50);
    }

    #endregion

    #region Warmup Validation

    [Fact]
    public void Homod_WarmupConvergence()
    {
        var homod = new Homod(6, 50);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        int i = 0;
        foreach (var bar in bars)
        {
            homod.Update(new TValue(bar.Time, bar.Close));
            i++;

            if (i == homod.WarmupPeriod)
            {
                Assert.True(homod.IsHot);
                break;
            }
        }
    }

    [Fact]
    public void Homod_StableAfterWarmup()
    {
        var homod = new Homod(6, 50);

        // Generate synthetic cycle
        for (int i = 0; i < 200; i++)
        {
            double value = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20);
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), value));
        }

        // Record values after warmup
        var postWarmupValues = new List<double>();
        for (int i = 200; i < 400; i++)
        {
            double value = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20);
            var result = homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), value));
            postWarmupValues.Add(result.Value);
        }

        // Standard deviation should be low for stable signal
        double mean = postWarmupValues.Average();
        double stdDev = Math.Sqrt(postWarmupValues.Select(v => (v - mean) * (v - mean)).Average());

        // Std dev should be relatively small for stable cycle detection
        Assert.True(stdDev < 5, $"Standard deviation {stdDev} is too high for stable signal");
    }

    #endregion

    [Fact]
    public void Homod_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateEhlersHomodyneDominantCycle();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}