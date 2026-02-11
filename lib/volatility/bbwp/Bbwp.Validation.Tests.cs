namespace QuanTAlib.Tests;
using Xunit;

/// <summary>
/// Validation tests for BBWP (Bollinger Band Width Percentile).
/// BBWP is a proprietary indicator, so we validate against internal consistency
/// and mathematical properties rather than external libraries.
/// </summary>
public class BbwpValidationTests
{
    private static TBarSeries GenerateTestData(int count = 500)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void BBWP_OutputRange_AlwaysValid()
    {
        var bars = GenerateTestData(500);
        var bbwp = new Bbwp(20, 2.0, 100);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwp.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
            Assert.True(result.Value >= 0.0, $"BBWP at {i} should be >= 0, got {result.Value}");
            Assert.True(result.Value <= 1.0, $"BBWP at {i} should be <= 1, got {result.Value}");
        }
    }

    [Fact]
    public void BBWP_StreamingVsBatch_Match()
    {
        var bars = GenerateTestData(200);
        var times = bars.Times;
        var close = bars.CloseValues;

        // Streaming calculation
        var bbwpStream = new Bbwp(10, 2.0, 50);
        var streamResults = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwpStream.Update(new TValue(times[i], close[i]));
            streamResults.Add(result.Value);
        }

        // Batch calculation
        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(times[i], close[i]));
        }
        var batchResults = Bbwp.Batch(ts, 10, 2.0, 50);

        // Compare results (should be identical)
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-10);
        }
    }

    [Fact]
    public void BBWP_DifferentPeriods_ProduceValidResults()
    {
        var bars = GenerateTestData(300);
        int[] periods = { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            var bbwp = new Bbwp(period, 2.0, 100);

            for (int i = 0; i < bars.Count; i++)
            {
                var result = bbwp.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
                Assert.True(double.IsFinite(result.Value), $"Period {period} at {i} should be finite");
                Assert.True(result.Value >= 0.0 && result.Value <= 1.0, $"Period {period} at {i} should be in [0,1]");
            }
        }
    }

    [Fact]
    public void BBWP_DifferentLookbacks_ProduceValidResults()
    {
        var bars = GenerateTestData(300);
        int[] lookbacks = { 20, 50, 100, 200 };

        foreach (int lookback in lookbacks)
        {
            var bbwp = new Bbwp(20, 2.0, lookback);

            for (int i = 0; i < bars.Count; i++)
            {
                var result = bbwp.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
                Assert.True(double.IsFinite(result.Value), $"Lookback {lookback} at {i} should be finite");
                Assert.True(result.Value >= 0.0 && result.Value <= 1.0, $"Lookback {lookback} at {i} should be in [0,1]");
            }
        }
    }

    [Fact]
    public void BBWP_DifferentMultipliers_ProduceValidResults()
    {
        var bars = GenerateTestData(200);
        double[] multipliers = { 1.0, 1.5, 2.0, 2.5, 3.0 };

        foreach (double mult in multipliers)
        {
            var bbwp = new Bbwp(20, mult, 100);

            for (int i = 0; i < bars.Count; i++)
            {
                var result = bbwp.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
                Assert.True(double.IsFinite(result.Value), $"Multiplier {mult} at {i} should be finite");
                Assert.True(result.Value >= 0.0 && result.Value <= 1.0, $"Multiplier {mult} at {i} should be in [0,1]");
            }
        }
    }

    [Fact]
    public void BBWP_ConstantInput_ProducesZeroPercentile()
    {
        var bbwp = new Bbwp(10, 2.0, 50);

        // Feed constant values - BBW will be 0, and percentile of 0 among 0s is 0
        for (int i = 0; i < 100; i++)
        {
            var result = bbwp.Update(new TValue(DateTime.UtcNow.Ticks + i, 100.0));
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }

        // With constant input, BBW=0 always, so percentile should be 0 (nothing below 0)
        Assert.Equal(0.0, bbwp.Last.Value, 1e-10);
    }

    [Fact]
    public void BBWP_HighVolatilitySpike_ProducesHighPercentile()
    {
        var bbwp = new Bbwp(5, 2.0, 20);

        // Feed low volatility data first
        for (int i = 0; i < 25; i++)
        {
            bbwp.Update(new TValue(DateTime.UtcNow.Ticks + i, 100.0 + (i % 2) * 0.1));
        }

        // Then introduce a high volatility spike
        bbwp.Update(new TValue(DateTime.UtcNow.Ticks + 25, 100.0));
        bbwp.Update(new TValue(DateTime.UtcNow.Ticks + 26, 110.0)); // Big move
        bbwp.Update(new TValue(DateTime.UtcNow.Ticks + 27, 105.0));

        // After high volatility, percentile should be elevated
        Assert.True(bbwp.Last.Value > 0.3, $"High volatility should produce elevated percentile, got {bbwp.Last.Value}");
    }

    [Fact]
    public void BBWP_PercentileDistribution_Reasonable()
    {
        var bars = GenerateTestData(500);
        var bbwp = new Bbwp(20, 2.0, 100);

        var results = new List<double>();
        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwp.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
            if (i >= 120) // After warmup
            {
                results.Add(result.Value);
            }
        }

        // Percentile values should be distributed - check quartiles
        results.Sort();
        int q1Idx = results.Count / 4;
        int q3Idx = 3 * results.Count / 4;

        double q1 = results[q1Idx];
        double q3 = results[q3Idx];

        // Should have meaningful spread
        Assert.True(q3 - q1 > 0.1, $"Percentile spread should be meaningful, Q1={q1:F3}, Q3={q3:F3}");
    }

    [Fact]
    public void BBWP_BarCorrection_Works()
    {
        var bbwp = new Bbwp(10, 2.0, 30);
        var bars = GenerateTestData(50);

        // Process all bars
        for (int i = 0; i < bars.Count; i++)
        {
            bbwp.Update(new TValue(bars.Times[i], bars.CloseValues[i]), isNew: true);
        }
        double originalValue = bbwp.Last.Value;

        // Correct the last bar with different value
        bbwp.Update(new TValue(bars.Times[bars.Count - 1], bars.CloseValues[bars.Count - 1] * 2), isNew: false);

        // Restore original value
        var restored = bbwp.Update(new TValue(bars.Times[bars.Count - 1], bars.CloseValues[bars.Count - 1]), isNew: false);

        Assert.Equal(originalValue, restored.Value, 1e-10);
    }

    [Fact]
    public void BBWP_SpanBatch_MatchesStreaming()
    {
        var bars = GenerateTestData(100);
        var close = bars.CloseValues.ToArray();

        // Streaming
        var bbwpStream = new Bbwp(10, 2.0, 30);
        for (int i = 0; i < close.Length; i++)
        {
            bbwpStream.Update(new TValue(DateTime.UtcNow.Ticks + i, close[i]));
        }

        // Batch via span
        var output = new double[close.Length];
        Bbwp.Batch(close, output, 10, 2.0, 30);

        Assert.Equal(bbwpStream.Last.Value, output[output.Length - 1], 1e-10);
    }
}