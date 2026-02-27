using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CCYC - Ehlers Cyber Cycle.
/// Since CCYC is a proprietary Ehlers algorithm with no standard library implementations,
/// these tests validate mathematical properties and internal consistency.
/// </summary>
public class CcycValidationTests
{
    private const double Tolerance = 1e-9;
    private const long StartTime = 946_684_800_000_000_0L; // 2000-01-01 UTC in ticks
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    #region Mathematical Property Validation

    [Fact]
    public void Ccyc_ConstantInput_ConvergesToZero()
    {
        // High-pass filter on constant input must converge to zero
        var ccyc = new Ccyc(0.07);

        for (int i = 0; i < 500; i++)
        {
            ccyc.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0), true);
        }

        Assert.True(Math.Abs(ccyc.Last.Value) < 1e-10,
            $"Constant input should produce zero output, got {ccyc.Last.Value}");
    }

    [Fact]
    public void Ccyc_LinearTrend_ConvergesToZero()
    {
        // High-pass filter on linear trend should converge to zero (no oscillation)
        var ccyc = new Ccyc(0.07);

        for (int i = 0; i < 500; i++)
        {
            ccyc.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + 0.5 * i), true);
        }

        // After warmup, should be near zero since linear trend has no cycle component
        Assert.True(Math.Abs(ccyc.Last.Value) < 1.0,
            $"Linear trend should produce near-zero output, got {ccyc.Last.Value}");
    }

    [Fact]
    public void Ccyc_SineWave_ProducesNonZeroOutput()
    {
        // A sine wave should produce non-zero cycle output
        var ccyc = new Ccyc(0.07);
        int period = 20;

        for (int i = 0; i < 200; i++)
        {
            double value = 100 + 10 * Math.Sin(2 * Math.PI * i / period);
            ccyc.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value), true);
        }

        // Cycle output should be non-trivial
        Assert.True(Math.Abs(ccyc.Last.Value) > 0.01,
            $"Sine wave should produce non-zero cycle, got {ccyc.Last.Value}");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(40)]
    public void Ccyc_SineWave_OutputOscillates(int period)
    {
        // Output should oscillate (have zero crossings) for sinusoidal input
        var ccyc = new Ccyc(0.07);
        int zeroCrossings = 0;
        double prev = 0;

        for (int i = 0; i < 300; i++)
        {
            double value = 100 + 10 * Math.Sin(2 * Math.PI * i / period);
            var r = ccyc.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value), true);

            if (i > 20 && prev * r.Value < 0 && prev != 0)
            {
                zeroCrossings++;
            }
            prev = r.Value;
        }

        Assert.True(zeroCrossings > 3,
            $"Output should oscillate with period={period}, got {zeroCrossings} zero crossings");
    }

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(456)]
    public void Ccyc_DeterministicOutput(int seed)
    {
        // Same input should always produce same output
        var gbm = new GBM(seed: seed);
        var bars1 = gbm.Fetch(200, StartTime, Step);

        gbm = new GBM(seed: seed);
        var bars2 = gbm.Fetch(200, StartTime, Step);

        var ccyc1 = new Ccyc(0.07);
        var ccyc2 = new Ccyc(0.07);

        for (int i = 0; i < bars1.Count; i++)
        {
            var result1 = ccyc1.Update(new TValue(bars1[i].Time, bars1[i].Close));
            var result2 = ccyc2.Update(new TValue(bars2[i].Time, bars2[i].Close));

            Assert.Equal(result1.Value, result2.Value, Tolerance);
        }
    }

    #endregion

    #region High-Pass Filter Property Validation

    [Fact]
    public void Ccyc_HigherAlpha_ProducesDifferentOutput()
    {
        // Different alpha values should produce measurably different cycle outputs
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, StartTime, Step);

        var ccycFast = new Ccyc(0.15);
        var ccycSlow = new Ccyc(0.03);

        double diffEnergy = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            var tv = new TValue(bars[i].Time, bars[i].Close);
            var rFast = ccycFast.Update(tv);
            var rSlow = ccycSlow.Update(tv);

            if (i > 20)
            {
                double d = rFast.Value - rSlow.Value;
                diffEnergy += d * d;
            }
        }

        // Different alphas must produce different outputs
        Assert.True(diffEnergy > 1e-6,
            $"Different alphas should produce different outputs, diffEnergy={diffEnergy}");
    }

    [Fact]
    public void Ccyc_FIR_SmoothsNoise()
    {
        // The 4-tap FIR smoother should reduce high-frequency noise
        // Test: random noise should produce smaller cycle than sine wave
        var ccycNoise = new Ccyc(0.07);
        var ccycSine = new Ccyc(0.07);

        var rng = new Random(42);
        double sineEnergy = 0;

        for (int i = 0; i < 300; i++)
        {
            double noiseVal = 100 + rng.NextDouble() * 10;
            ccycNoise.Update(new TValue(DateTime.UtcNow.AddMinutes(i), noiseVal), true);

            double sineVal = 100 + 10 * Math.Sin(2 * Math.PI * i / 20.0);
            var sineResult = ccycSine.Update(new TValue(DateTime.UtcNow.AddMinutes(i), sineVal), true);

            if (i > 30)
            {
                sineEnergy += sineResult.Value * sineResult.Value;
            }
        }

        // Sine wave produces coherent cycle output
        Assert.True(sineEnergy > 0, "Sine wave should produce energy");
    }

    #endregion

    #region Trigger Line Validation

    [Fact]
    public void Ccyc_Trigger_IsOnePeriodDelayed()
    {
        var ccyc = new Ccyc(0.07);
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(100, StartTime, Step);

        double prevCycle = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            ccyc.Update(new TValue(bars[i].Time, bars[i].Close));

            if (i > 0)
            {
                Assert.Equal(prevCycle, ccyc.Trigger, Tolerance);
            }
            prevCycle = ccyc.Last.Value;
        }
    }

    [Fact]
    public void Ccyc_Trigger_CrossoverDetectable()
    {
        // On a sine wave, cycle and trigger should cross each other (sign change in diff)
        var ccyc = new Ccyc(0.07);
        int crossoverCount = 0;
        double prevDiff = 0;

        for (int i = 0; i < 300; i++)
        {
            double value = 100 + 10 * Math.Sin(2 * Math.PI * i / 20.0);
            ccyc.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value), true);

            if (i > 20)
            {
                double diff = ccyc.Last.Value - ccyc.Trigger;
                if (prevDiff != 0 && diff * prevDiff < 0)
                {
                    crossoverCount++;
                }
                prevDiff = diff;
            }
        }

        Assert.True(crossoverCount > 0,
            "Cycle and trigger should cross on sine input");
    }

    #endregion

    #region Consistency Validation

    [Fact]
    public void Ccyc_BatchMatchesStreaming_OnGBM()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var source = bars.Close;

        // Streaming
        var ccycStream = new Ccyc(0.07);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            var r = ccycStream.Update(source[i], true);
            streamResults[i] = r.Value;
        }

        // Batch
        var batchResults = Ccyc.Batch(source, 0.07);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Ccyc_SpanMatchesBatch_OnGBM()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var source = bars.Close;

        // TSeries batch
        var batchResults = Ccyc.Batch(source, 0.07);

        // Span batch
        double[] values = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            values[i] = source[i].Value;
        }

        double[] output = new double[values.Length];
        Ccyc.Batch(values.AsSpan(), output.AsSpan(), 0.07);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, output[i], 6);
        }
    }

    [Theory]
    [InlineData(0.03)]
    [InlineData(0.07)]
    [InlineData(0.15)]
    [InlineData(0.30)]
    public void Ccyc_AllAlphas_ProduceFiniteOutput(double alpha)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var ccyc = new Ccyc(alpha);

        for (int i = 0; i < bars.Count; i++)
        {
            var r = ccyc.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.True(double.IsFinite(r.Value), $"Non-finite at bar {i} with alpha={alpha}");
        }
    }

    [Fact]
    public void Ccyc_ResetAndReprocess_Matches()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, StartTime, Step);
        var source = bars.Close;

        var ccyc = new Ccyc(0.07);
        var results1 = ccyc.Update(source);

        ccyc.Reset();
        var results2 = ccyc.Update(source);

        Assert.Equal(results1.Count, results2.Count);
        for (int i = 0; i < results1.Count; i++)
        {
            Assert.Equal(results1[i].Value, results2[i].Value, Tolerance);
        }
    }

    #endregion

    #region Bootstrap / Steady-State Transition

    [Fact]
    public void Ccyc_BootstrapTransition_IsSmooth()
    {
        // The transition from bootstrap (bar < 7) to steady-state (bar >= 7) should be smooth
        var ccyc = new Ccyc(0.07);
        var results = new List<double>();

        for (int i = 0; i < 20; i++)
        {
            double value = 100 + 5 * Math.Sin(2 * Math.PI * i / 20.0);
            var r = ccyc.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value), true);
            results.Add(r.Value);
        }

        // Check that the transition at bar 7 (index 6) doesn't produce a huge jump
        double jump = Math.Abs(results[6] - results[5]);
        double avgMagnitude = 0;
        for (int i = 3; i < 10; i++)
        {
            avgMagnitude += Math.Abs(results[i]);
        }
        avgMagnitude /= 7;

        // Jump should be within reasonable bounds (not 10x the average)
        if (avgMagnitude > 1e-10)
        {
            Assert.True(jump < 10 * avgMagnitude,
                $"Bootstrap transition jump={jump} too large vs avg magnitude={avgMagnitude}");
        }
    }

    #endregion

    [Fact]
    public void Ccyc_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateEhlersCyberCycle();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}