using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for SAM - Smoothed Adaptive Momentum.
/// Since SAM is a proprietary Ehlers algorithm with no standard library implementations,
/// these tests validate mathematical properties and internal consistency.
/// </summary>
public class SamValidationTests
{
    private const double Tolerance = 1e-9;

    #region Mathematical Property Validation

    [Fact]
    public void Sam_OutputIsFinite_ForAllGBMData()
    {
        var sam = new Sam();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = sam.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value),
                $"Non-finite SAM value at {bar.Time}: {result.Value}");
        }
    }

    [Fact]
    public void Sam_ConstantPrice_ConvergesToZero()
    {
        // With constant price, momentum is zero → Super Smoother converges to zero
        var sam = new Sam();
        TValue result = default;

        for (int i = 0; i < 500; i++)
        {
            result = sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }

        Assert.Equal(0.0, result.Value, 8);
    }

    [Fact]
    public void Sam_SmoothTransitions()
    {
        // SAM output should be smooth due to Super Smoother filter
        var sam = new Sam();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double? prevValue = null;
        int largeJumps = 0;

        foreach (var bar in bars)
        {
            var result = sam.Update(new TValue(bar.Time, bar.Close));

            if (prevValue.HasValue && sam.IsHot)
            {
                double change = Math.Abs(result.Value - prevValue.Value);
                // Super Smoother should prevent extremely large jumps
                if (change > 50)
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
    public void Sam_DeterministicOutput(int seed)
    {
        // Same input should always produce same output
        var gbm = new GBM(seed: seed);
        var bars1 = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        gbm = new GBM(seed: seed);
        var bars2 = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var sam1 = new Sam();
        var sam2 = new Sam();

        for (int i = 0; i < 200; i++)
        {
            var r1 = sam1.Update(new TValue(bars1[i].Time, bars1[i].Close));
            var r2 = sam2.Update(new TValue(bars2[i].Time, bars2[i].Close));

            Assert.Equal(r1.Value, r2.Value, 12);
        }
    }

    [Fact]
    public void Sam_DominantCycle_WithinBounds()
    {
        // Dominant cycle should always be within [6, 50] (MinCyclePeriod, MaxCyclePeriod)
        var sam = new Sam();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            sam.Update(new TValue(bar.Time, bar.Close));

            if (sam.IsHot)
            {
                Assert.True(sam.DominantCycle >= 6 && sam.DominantCycle <= 50,
                    $"DominantCycle {sam.DominantCycle} out of bounds [6, 50]");
            }
        }
    }

    #endregion

    #region Alpha Parameter Sensitivity

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.07)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Sam_DifferentAlphas_ProduceFiniteResults(double alpha)
    {
        var sam = new Sam(alpha: alpha);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = sam.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value),
                $"Non-finite SAM(alpha={alpha}) at {bar.Time}: {result.Value}");
        }
    }

    [Fact]
    public void Sam_DifferentAlphas_ProduceDivergentOutputs()
    {
        // Different alpha values affect cycle detection EMA smoothing,
        // producing different dominant cycle estimates and thus different outputs
        var samSlow = new Sam(alpha: 0.01);
        var samFast = new Sam(alpha: 0.5);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double sumAbsDivergence = 0;
        int hotCount = 0;

        foreach (var bar in bars)
        {
            var rSlow = samSlow.Update(new TValue(bar.Time, bar.Close));
            var rFast = samFast.Update(new TValue(bar.Time, bar.Close));

            if (samSlow.IsHot && samFast.IsHot)
            {
                sumAbsDivergence += Math.Abs(rSlow.Value - rFast.Value);
                hotCount++;
            }
        }

        // Different alphas should produce meaningfully different outputs
        double avgDivergence = sumAbsDivergence / hotCount;
        Assert.True(avgDivergence > 0.01,
            $"Average divergence ({avgDivergence:F6}) too small — alpha should affect output");
    }

    #endregion

    #region Cutoff Parameter Sensitivity

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(30)]
    public void Sam_DifferentCutoffs_ProduceFiniteResults(int cutoff)
    {
        var sam = new Sam(cutoff: cutoff);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = sam.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value),
                $"Non-finite SAM(cutoff={cutoff}) at {bar.Time}: {result.Value}");
        }
    }

    [Fact]
    public void Sam_LargerCutoff_SmoothesMore()
    {
        // Larger Super Smoother cutoff = more smoothing = less bar-to-bar variation
        var samSharp = new Sam(cutoff: 2);
        var samSmooth = new Sam(cutoff: 30);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double sumAbsDiffSharp = 0;
        double sumAbsDiffSmooth = 0;
        double? prevSharp = null;
        double? prevSmooth = null;

        foreach (var bar in bars)
        {
            var rSharp = samSharp.Update(new TValue(bar.Time, bar.Close));
            var rSmooth = samSmooth.Update(new TValue(bar.Time, bar.Close));

            if (samSharp.IsHot && samSmooth.IsHot)
            {
                if (prevSharp.HasValue)
                {
                    sumAbsDiffSharp += Math.Abs(rSharp.Value - prevSharp.Value);
                    sumAbsDiffSmooth += Math.Abs(rSmooth.Value - prevSmooth!.Value);
                }
                prevSharp = rSharp.Value;
                prevSmooth = rSmooth.Value;
            }
        }

        // Larger cutoff should produce smoother (less variable) output
        Assert.True(sumAbsDiffSmooth < sumAbsDiffSharp,
            $"Smooth SAM variation ({sumAbsDiffSmooth:F4}) should be less than sharp ({sumAbsDiffSharp:F4})");
    }

    #endregion

    #region Batch vs Streaming Consistency

    [Fact]
    public void Sam_Batch_MatchesStreaming()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var close = bars.Close;

        // Batch
        var batchResult = Sam.Batch(close);

        // Streaming
        var sam = new Sam();
        for (int i = 0; i < close.Count; i++)
        {
            var result = sam.Update(close[i]);
            Assert.Equal(batchResult[i].Value, result.Value, Tolerance);
        }
    }

    [Fact]
    public void Sam_SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var close = bars.Close;

        var batchResult = Sam.Batch(close);

        double[] spanOutput = new double[close.Count];
        Sam.Batch(close.Values, spanOutput);

        for (int i = 0; i < close.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void Sam_Calculate_MatchesBatch()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var close = bars.Close;

        var batchResult = Sam.Batch(close);
        var (calcResult, indicator) = Sam.Calculate(close);

        Assert.Equal(batchResult.Count, calcResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, calcResult[i].Value, Tolerance);
        }
        Assert.True(indicator.IsHot);
    }

    #endregion

    #region Oscillator Properties

    [Fact]
    public void Sam_MeanRevertingBehavior()
    {
        // SAM is a momentum oscillator; over long series it should oscillate around zero
        var sam = new Sam();
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.3, seed: 42);
        var bars = gbm.Fetch(2000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double sum = 0;
        int hotCount = 0;

        foreach (var bar in bars)
        {
            var result = sam.Update(new TValue(bar.Time, bar.Close));
            if (sam.IsHot)
            {
                sum += result.Value;
                hotCount++;
            }
        }

        // Mean of oscillator should be near zero for zero-drift GBM
        double mean = sum / hotCount;
        Assert.True(Math.Abs(mean) < 5.0,
            $"SAM mean ({mean:F4}) too far from zero for zero-drift GBM");
    }

    [Fact]
    public void Sam_UptrendProducesPositiveBias()
    {
        // Strong uptrend should produce positive SAM values
        var sam = new Sam();
        int positiveCount = 0;
        int hotCount = 0;

        for (int i = 0; i < 300; i++)
        {
            var result = sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 3.0), true);
            if (sam.IsHot)
            {
                hotCount++;
                if (result.Value > 0)
                {
                    positiveCount++;
                }
            }
        }

        // Uptrend should produce mostly positive momentum
        double ratio = (double)positiveCount / hotCount;
        Assert.True(ratio > 0.5, $"Positive ratio {ratio:P} too low for uptrend");
    }

    [Fact]
    public void Sam_DowntrendProducesNegativeBias()
    {
        // Strong downtrend should produce negative SAM values
        var sam = new Sam();
        int negativeCount = 0;
        int hotCount = 0;

        for (int i = 0; i < 300; i++)
        {
            var result = sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 500.0 - i * 3.0), true);
            if (sam.IsHot)
            {
                hotCount++;
                if (result.Value < 0)
                {
                    negativeCount++;
                }
            }
        }

        // Downtrend should produce mostly negative momentum
        double ratio = (double)negativeCount / hotCount;
        Assert.True(ratio > 0.5, $"Negative ratio {ratio:P} too low for downtrend");
    }

    #endregion

    #region Reset Consistency

    [Fact]
    public void Sam_ResetAndRecalculate_MatchesOriginal()
    {
        var sam = new Sam();
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // First pass
        double lastValue1 = 0;
        foreach (var bar in bars)
        {
            var result = sam.Update(new TValue(bar.Time, bar.Close));
            lastValue1 = result.Value;
        }

        // Reset and replay
        sam.Reset();
        double lastValue2 = 0;
        foreach (var bar in bars)
        {
            var result = sam.Update(new TValue(bar.Time, bar.Close));
            lastValue2 = result.Value;
        }

        Assert.Equal(lastValue1, lastValue2, Tolerance);
    }

    #endregion
}
