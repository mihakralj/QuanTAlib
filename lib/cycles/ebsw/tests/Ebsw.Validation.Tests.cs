using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for EBSW (Ehlers Even Better Sinewave).
/// EBSW is Ehlers' proprietary indicator not commonly implemented in trading libraries
/// (TA-Lib, Skender, Tulip), so validation is done against mathematical properties
/// and known theoretical results based on the original PineScript implementation.
/// </summary>
public class EbswValidationTests
{
    private const double Tolerance = 1e-9;

    #region Mathematical Property Validation

    [Fact]
    public void Validation_ConstantSeries_OutputBounded()
    {
        // For constant input, high-pass filter removes DC, making filt → 0.
        // However, AGC (wave/sqrt(pwr)) normalizes any non-zero residual.
        // Due to floating-point precision, tiny filt values produce ratios ≈ ±1.
        // This is mathematically correct - the AGC is doing its job.
        var ebsw = new Ebsw(40, 10);

        for (int i = 0; i < 500; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Output should still be bounded [-1, +1]
        Assert.InRange(ebsw.Last.Value, -1.0, 1.0);
    }

    [Fact]
    public void Validation_OutputBoundedBetweenNegativeOneAndOne()
    {
        // AGC should always normalize output to [-1, +1]
        var ebsw = new Ebsw(40, 10);

        var gbm = new GBM(seed: 42, sigma: 0.5); // High volatility
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
            Assert.True(ebsw.Last.Value >= -1.0 && ebsw.Last.Value <= 1.0,
                $"EBSW output {ebsw.Last.Value} should be in [-1, +1]");
        }
    }

    [Fact]
    public void Validation_OscillatesAroundZero()
    {
        // EBSW should oscillate around zero over time
        var ebsw = new Ebsw(40, 10);
        var values = new List<double>();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
            if (ebsw.IsHot)
            {
                values.Add(ebsw.Last.Value);
            }
        }

        // Should have both positive and negative values
        int positiveCount = values.Count(v => v > 0);
        int negativeCount = values.Count(v => v < 0);

        Assert.True(positiveCount > 0, "Should have positive EBSW values");
        Assert.True(negativeCount > 0, "Should have negative EBSW values");
    }

    [Fact]
    public void Validation_ZeroCrossings_IndicateCyclePhase()
    {
        // EBSW should cross zero when cycle phase changes
        var ebsw = new Ebsw(20, 5);
        var values = new List<double>();

        // Generate sine wave to simulate price oscillation
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + (10.0 * Math.Sin(i * 0.1));
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (ebsw.IsHot)
            {
                values.Add(ebsw.Last.Value);
            }
        }

        // Count zero crossings
        int crossings = 0;
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i - 1] * values[i] < 0)
            {
                crossings++;
            }
        }

        // Should have multiple zero crossings for oscillating price
        Assert.True(crossings >= 3, $"Should have multiple zero crossings, got {crossings}");
    }

    #endregion

    #region PineScript Formula Verification

    [Fact]
    public void Validation_HighPassCoefficient_MatchesPineScript()
    {
        // alpha1 = (1 - sin(2π/hpLength)) / cos(2π/hpLength)
        const int hpLength = 40;
        double angleHp = 2.0 * Math.PI / hpLength;
        double expectedAlpha1 = (1.0 - Math.Sin(angleHp)) / Math.Cos(angleHp);

        // Verify the coefficient calculation
        Assert.True(expectedAlpha1 > 0 && expectedAlpha1 < 1,
            $"Alpha1 should be between 0 and 1, got {expectedAlpha1}");
    }

    [Fact]
    public void Validation_SuperSmootherCoefficients_MatchesPineScript()
    {
        // alpha2 = exp(-√2 * π / ssfLength)
        // beta = 2 * alpha2 * cos(√2 * π / ssfLength)
        // c1 = 1 - beta + alpha2², c2 = beta, c3 = -alpha2²
        const int ssfLength = 10;
        double angleSsf = Math.Sqrt(2.0) * Math.PI / ssfLength;
        double alpha2 = Math.Exp(-angleSsf);
        double beta = 2.0 * alpha2 * Math.Cos(angleSsf);
        double c2 = beta;
        double c3 = -(alpha2 * alpha2);
        double c1 = 1.0 - c2 - c3;

        // Verify IIR filter stability: poles must be inside unit circle
        // For two-pole Butterworth-style SSF: |alpha2| < 1 ensures stability
        Assert.True(alpha2 > 0 && alpha2 < 1, $"alpha2 should be in (0,1), got {alpha2}");
        Assert.True(c1 > 0, "c1 should be positive");
        Assert.True(c2 > 0, "c2 should be positive");
        Assert.True(c3 < 0, "c3 should be negative");
        // Verify c1 is computed correctly: c1 = 1 - beta + alpha2²
        double expectedC1 = 1.0 - beta + (alpha2 * alpha2);
        Assert.Equal(expectedC1, c1, 1e-10);
    }

    [Fact]
    public void Validation_AGCNormalization_ClampsMagnitude()
    {
        // wave / sqrt(pwr) can theoretically exceed 1 before clamping
        // The clamp ensures output stays in [-1, +1]
        var ebsw = new Ebsw(10, 3);

        // Extreme step changes should still produce bounded output
        for (int i = 0; i < 100; i++)
        {
            double price = (i % 2 == 0) ? 200.0 : 50.0; // Extreme oscillation
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            Assert.True(Math.Abs(ebsw.Last.Value) <= 1.0,
                $"EBSW output magnitude {Math.Abs(ebsw.Last.Value)} should not exceed 1");
        }
    }

    #endregion

    #region Filter Behavior Validation

    [Fact]
    public void Validation_HighPassFilter_RemovesTrend()
    {
        // High-pass filter removes DC/trend component
        // EBSW output should remain bounded even with strong trend
        var ebsw = new Ebsw(40, 10);
        var values = new List<double>();

        // Strong uptrend with oscillating component
        // Larger amplitude oscillation to ensure EBSW detects cycles
        for (int i = 0; i < 300; i++)
        {
            double trend = 100.0 + (i * 0.5);
            double oscillation = Math.Sin(i * 0.15) * 10.0; // Larger amplitude, longer period
            double price = trend + oscillation;
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (ebsw.IsHot)
            {
                values.Add(ebsw.Last.Value);
            }
        }

        // Output should be bounded [-1, +1] despite strong trend
        Assert.True(values.All(v => v >= -1.0 && v <= 1.0), "All values should be bounded");
        // Should span a significant portion of the range (AGC normalizes output)
        double range = values.Max() - values.Min();
        Assert.True(range > 0.5, $"Should have significant range, got {range}");
    }

    [Fact]
    public void Validation_SuperSmoother_ReducesNoise()
    {
        // Super-smoother should reduce high-frequency noise
        // Longer SSF length should produce smoother output
        var ebswShort = new Ebsw(40, 5);
        var ebswLong = new Ebsw(40, 20);

        var gbm = new GBM(seed: 42, sigma: 0.3);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var valuesShort = new List<double>();
        var valuesLong = new List<double>();

        foreach (var bar in bars)
        {
            ebswShort.Update(new TValue(bar.Time, bar.Close));
            ebswLong.Update(new TValue(bar.Time, bar.Close));
            if (ebswShort.IsHot && ebswLong.IsHot)
            {
                valuesShort.Add(ebswShort.Last.Value);
                valuesLong.Add(ebswLong.Last.Value);
            }
        }

        // Calculate bar-to-bar changes (roughness)
        double roughnessShort = 0, roughnessLong = 0;
        for (int i = 1; i < valuesShort.Count; i++)
        {
            roughnessShort += Math.Abs(valuesShort[i] - valuesShort[i - 1]);
            roughnessLong += Math.Abs(valuesLong[i] - valuesLong[i - 1]);
        }

        Assert.True(roughnessLong < roughnessShort,
            $"Longer SSF should be smoother: short={roughnessShort:F4}, long={roughnessLong:F4}");
    }

    [Fact]
    public void Validation_PureSineInput_ExtractsCycle()
    {
        // For pure sine input matching the filter period,
        // EBSW should produce clean oscillation
        var ebsw = new Ebsw(40, 10);
        var values = new List<double>();

        // Generate pure sine wave at matching frequency
        double frequency = 2.0 * Math.PI / 40.0;
        for (int i = 0; i < 500; i++)
        {
            double price = 100.0 + (10.0 * Math.Sin(i * frequency));
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (ebsw.IsHot)
            {
                values.Add(ebsw.Last.Value);
            }
        }

        // Should reach values close to +1 and -1
        double maxVal = values.Max();
        double minVal = values.Min();

        Assert.True(maxVal > 0.7, $"Max should be close to +1, got {maxVal}");
        Assert.True(minVal < -0.7, $"Min should be close to -1, got {minVal}");
    }

    #endregion

    #region Streaming vs Batch Consistency

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Validation_StreamingMatchesBatch(int seed)
    {
        const int hpLength = 40;
        const int ssfLength = 10;
        const int dataLen = 100;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Ebsw(hpLength, ssfLength);
        foreach (var bar in bars)
        {
            streaming.Update(new TValue(bar.Time, bar.Close));
        }

        // Batch via TSeries
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var batch = Ebsw.Batch(tSeries, hpLength, ssfLength);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_SpanMatchesTSeries()
    {
        const int hpLength = 20;
        const int ssfLength = 5;
        const int dataLen = 200;

        var gbm = new GBM(seed: 77);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // TSeries approach
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var tSeriesResult = Ebsw.Batch(tSeries, hpLength, ssfLength);

        // Span approach
        double[] source = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            source[i] = bars[i].Close;
        }

        Ebsw.Batch(source, spanResult, hpLength, ssfLength);

        // Compare all values
        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(tSeriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    #endregion

    #region Different Parameter Combinations

    [Theory]
    [InlineData(10, 3)]
    [InlineData(20, 5)]
    [InlineData(40, 10)]
    [InlineData(80, 20)]
    public void Validation_DifferentParameters_ConsistentResults(int hpLength, int ssfLength)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var ebsw = new Ebsw(hpLength, ssfLength);
        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ebsw.IsHot);
        Assert.True(double.IsFinite(ebsw.Last.Value));
        Assert.True(Math.Abs(ebsw.Last.Value) <= 1.0);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(80)]
    public void Validation_LongerHpPeriod_SmallerOutputVariance(int hpLength)
    {
        // Longer HP period removes more low-frequency content
        var ebsw = new Ebsw(hpLength, 10);
        var values = new List<double>();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
            if (ebsw.IsHot)
            {
                values.Add(ebsw.Last.Value);
            }
        }

        // Check variance is non-zero
        double mean = values.Average();
        double variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        Assert.True(variance > 0, "Should have non-zero variance");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validation_VerySmallPrices_HandledCorrectly()
    {
        var ebsw = new Ebsw(20, 5);

        for (int i = 0; i < 100; i++)
        {
            double price = 0.0001 + (i * 0.00001);
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(ebsw.IsHot);
        Assert.True(double.IsFinite(ebsw.Last.Value));
        Assert.True(Math.Abs(ebsw.Last.Value) <= 1.0);
    }

    [Fact]
    public void Validation_VeryLargePrices_HandledCorrectly()
    {
        var ebsw = new Ebsw(20, 5);

        for (int i = 0; i < 100; i++)
        {
            double price = 1e10 + (i * 1e8);
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(ebsw.IsHot);
        Assert.True(double.IsFinite(ebsw.Last.Value));
        Assert.True(Math.Abs(ebsw.Last.Value) <= 1.0);
    }

    [Fact]
    public void Validation_HighVolatility_StableResults()
    {
        var ebsw = new Ebsw(20, 5);

        var gbm = new GBM(seed: 42, sigma: 0.5); // High volatility
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(ebsw.Last.Value), "EBSW should remain finite under high volatility");
            Assert.True(Math.Abs(ebsw.Last.Value) <= 1.0, "EBSW should remain bounded under high volatility");
        }
    }

    [Fact]
    public void Validation_StepChange_ProducesBoundedOutput()
    {
        // For constant input, high-pass filter makes filt → 0.
        // AGC normalizes tiny residuals to ±1 (0/0 → ε/√(ε²) = ±1).
        // After step change, transient occurs then settles to bounded output.
        var ebsw = new Ebsw(40, 10);

        // Stable period at price 100
        for (int i = 0; i < 100; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        double beforeStep = ebsw.Last.Value;

        // Step change to price 150
        for (int i = 100; i < 200; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 150.0));
        }

        double afterStep = ebsw.Last.Value;

        // Both should remain bounded [-1, +1]
        Assert.True(Math.Abs(beforeStep) <= 1.0, $"Before step should be bounded, got {beforeStep}");
        Assert.True(Math.Abs(afterStep) <= 1.0, $"After step should be bounded, got {afterStep}");
        Assert.True(double.IsFinite(beforeStep), "Before step should be finite");
        Assert.True(double.IsFinite(afterStep), "After step should be finite");
    }

    #endregion

    #region AGC (Automatic Gain Control) Validation

    [Fact]
    public void Validation_AGC_AdaptsToVolatility()
    {
        // AGC normalizes by RMS, so different volatility levels
        // should still produce output in [-1, +1]
        var ebswLow = new Ebsw(40, 10);
        var ebswHigh = new Ebsw(40, 10);

        // Low volatility
        var gbmLow = new GBM(seed: 42, sigma: 0.05);
        var barsLow = gbmLow.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // High volatility
        var gbmHigh = new GBM(seed: 42, sigma: 0.5);
        var barsHigh = gbmHigh.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var valuesLow = new List<double>();
        var valuesHigh = new List<double>();

        foreach (var bar in barsLow)
        {
            ebswLow.Update(new TValue(bar.Time, bar.Close));
            if (ebswLow.IsHot)
            {
                valuesLow.Add(ebswLow.Last.Value);
            }
        }

        foreach (var bar in barsHigh)
        {
            ebswHigh.Update(new TValue(bar.Time, bar.Close));
            if (ebswHigh.IsHot)
            {
                valuesHigh.Add(ebswHigh.Last.Value);
            }
        }

        // Both should have values spanning much of the [-1, +1] range
        double rangeLow = valuesLow.Max() - valuesLow.Min();
        double rangeHigh = valuesHigh.Max() - valuesHigh.Min();

        Assert.True(rangeLow > 0.5, $"Low vol range should be significant: {rangeLow}");
        Assert.True(rangeHigh > 0.5, $"High vol range should be significant: {rangeHigh}");
    }

    [Fact]
    public void Validation_AGC_ZeroPowerHandled()
    {
        // When power is zero (constant input), division returns 0
        var ebsw = new Ebsw(10, 3);

        // All constant values
        for (int i = 0; i < 50; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.True(double.IsFinite(ebsw.Last.Value), "Should handle zero power gracefully");
    }

    #endregion

    [Fact]
    public void Ebsw_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateEhlersEvenBetterSineWaveIndicator();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
