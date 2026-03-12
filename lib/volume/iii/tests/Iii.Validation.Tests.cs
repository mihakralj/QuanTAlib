using Xunit;

namespace QuanTAlib.Tests;

public class IiiValidationTests
{
    private const int DataPoints = 5000;
    private const int DefaultPeriod = 14;

    private static readonly double SkenderTolerance = ValidationHelper.SkenderTolerance;

    private static TBarSeries GenerateTestData(int seed = 42)
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: seed);

        for (int i = 0; i < DataPoints; i++)
        {
            bars.Add(gbm.Next());
        }

        return bars;
    }

    [Fact]
    public void Iii_BatchMode_MatchesStreamingMode()
    {
        var bars = GenerateTestData();
        var iii = new Iii(DefaultPeriod);

        // Streaming mode
        var streamingResults = new List<double>();
        foreach (var bar in bars)
        {
            streamingResults.Add(iii.Update(bar).Value);
        }

        // Batch mode
        var batchResults = Iii.Batch(bars, DefaultPeriod);

        // Compare results
        Assert.Equal(bars.Count, batchResults.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, 8);
        }
    }

    [Fact]
    public void Iii_SpanMode_MatchesStreamingMode()
    {
        var bars = GenerateTestData();
        var iii = new Iii(DefaultPeriod);

        // Streaming mode
        var streamingResults = new List<double>();
        foreach (var bar in bars)
        {
            streamingResults.Add(iii.Update(bar).Value);
        }

        // Span mode
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanResults = new double[bars.Count];
        Iii.Batch(high, low, close, volume, spanResults, DefaultPeriod);

        // Compare results
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 8);
        }
    }

    [Fact]
    public void Iii_CumulativeMode_BatchMatchesStreaming()
    {
        var bars = GenerateTestData();
        var iii = new Iii(DefaultPeriod, cumulative: true);

        // Streaming mode
        var streamingResults = new List<double>();
        foreach (var bar in bars)
        {
            streamingResults.Add(iii.Update(bar).Value);
        }

        // Batch mode
        var batchResults = Iii.Batch(bars, DefaultPeriod, cumulative: true);

        // Compare results
        Assert.Equal(bars.Count, batchResults.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, 8);
        }
    }

    [Fact]
    public void Iii_CumulativeMode_SpanMatchesStreaming()
    {
        var bars = GenerateTestData();
        var iii = new Iii(DefaultPeriod, cumulative: true);

        // Streaming mode
        var streamingResults = new List<double>();
        foreach (var bar in bars)
        {
            streamingResults.Add(iii.Update(bar).Value);
        }

        // Span mode
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanResults = new double[bars.Count];
        Iii.Batch(high, low, close, volume, spanResults, DefaultPeriod, cumulative: true);

        // Compare results
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], spanResults[i], 8);
        }
    }

    [Fact]
    public void Iii_AllThreeModesMatch_WithinTolerance()
    {
        var bars = GenerateTestData();
        var iii = new Iii(DefaultPeriod);

        // Streaming mode
        var streamingResults = new List<double>();
        foreach (var bar in bars)
        {
            streamingResults.Add(iii.Update(bar).Value);
        }

        // Batch mode
        var batchResults = Iii.Batch(bars, DefaultPeriod);

        // Span mode
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanResults = new double[bars.Count];
        Iii.Batch(high, low, close, volume, spanResults, DefaultPeriod);

        // All three should match
        for (int i = 0; i < bars.Count; i++)
        {
            double streaming = streamingResults[i];
            double batch = batchResults[i].Value;
            double span = spanResults[i];

            Assert.Equal(streaming, batch, 8);
            Assert.Equal(streaming, span, 8);
            Assert.Equal(batch, span, 8);
        }
    }

    [Fact]
    public void Iii_Last100Values_AllModesMatch()
    {
        var bars = GenerateTestData();
        var iii = new Iii(DefaultPeriod);

        // Streaming mode
        var streamingResults = new List<double>();
        foreach (var bar in bars)
        {
            streamingResults.Add(iii.Update(bar).Value);
        }

        // Batch mode
        var batchResults = Iii.Batch(bars, DefaultPeriod);

        // Span mode
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanResults = new double[bars.Count];
        Iii.Batch(high, low, close, volume, spanResults, DefaultPeriod);

        // Focus on last 100 values (well past warmup)
        int startIdx = bars.Count - 100;
        for (int i = startIdx; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, SkenderTolerance);
            Assert.Equal(streamingResults[i], spanResults[i], SkenderTolerance);
        }
    }

    [Fact]
    public void Iii_DifferentPeriods_ProduceDifferentResults()
    {
        var bars = GenerateTestData();

        var iii10 = new Iii(10);
        var iii20 = new Iii(20);
        var iii50 = new Iii(50);

        var results10 = new List<double>();
        var results20 = new List<double>();
        var results50 = new List<double>();

        foreach (var bar in bars)
        {
            results10.Add(iii10.Update(bar).Value);
            results20.Add(iii20.Update(bar).Value);
            results50.Add(iii50.Update(bar).Value);
        }

        // After warmup, results should differ
        int testIdx = 100;
        Assert.NotEqual(results10[testIdx], results20[testIdx]);
        Assert.NotEqual(results20[testIdx], results50[testIdx]);
        Assert.NotEqual(results10[testIdx], results50[testIdx]);
    }

    [Fact]
    public void Iii_SmoothedVsCumulative_ProduceDifferentResults()
    {
        var bars = GenerateTestData();

        var iiiSmoothed = new Iii(DefaultPeriod, cumulative: false);
        var iiiCumulative = new Iii(DefaultPeriod, cumulative: true);

        var smoothedResults = new List<double>();
        var cumulativeResults = new List<double>();

        foreach (var bar in bars)
        {
            smoothedResults.Add(iiiSmoothed.Update(bar).Value);
            cumulativeResults.Add(iiiCumulative.Update(bar).Value);
        }

        // After first bar, results should differ (cumulative grows, smoothed averages)
        for (int i = DefaultPeriod; i < bars.Count; i++)
        {
            Assert.NotEqual(smoothedResults[i], cumulativeResults[i]);
        }
    }

    [Fact]
    public void Iii_PositionMultiplier_ValuesBounded()
    {
        // III raw values should be bounded by volume since position multiplier is [-1, +1]
        var bars = GenerateTestData();
        var iii = new Iii(period: 1); // Period 1 to see raw values

        foreach (var bar in bars)
        {
            var result = iii.Update(bar);
            double vol = Math.Max(bar.Volume, 1.0);

            // With period 1, result equals raw III
            // Position multiplier bounded [-1, +1], so result bounded [-vol, +vol]
            Assert.True(result.Value <= vol && result.Value >= -vol,
                $"III value {result.Value} exceeds volume bounds {vol}");
        }
    }

    [Fact]
    public void Iii_ConsistentResults_MultipleSeedTests()
    {
        // Test with multiple seeds to ensure consistency
        int[] seeds = { 42, 123, 456, 789, 1000 };

        foreach (int seed in seeds)
        {
            var bars = GenerateTestData(seed);
            var iii = new Iii(DefaultPeriod);

            // Streaming
            var streamingResults = new List<double>();
            foreach (var bar in bars)
            {
                streamingResults.Add(iii.Update(bar).Value);
            }

            // Batch
            var batchResults = Iii.Batch(bars, DefaultPeriod);

            // Should match for any seed
            for (int i = bars.Count - 50; i < bars.Count; i++)
            {
                Assert.Equal(streamingResults[i], batchResults[i].Value, 8);
            }
        }
    }
}
