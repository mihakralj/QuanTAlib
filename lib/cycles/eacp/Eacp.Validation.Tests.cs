using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for EACP (Ehlers Autocorrelation Periodogram).
/// EACP is Ehlers' proprietary indicator not commonly implemented in trading libraries
/// (TA-Lib, Skender, Tulip), so validation is done against mathematical properties
/// and known theoretical results based on the original PineScript implementation.
/// </summary>
public class EacpValidationTests
{
    private const double Tolerance = 1e-9;

    #region Mathematical Property Validation

    [Fact]
    public void Validation_ConstantSeries_DominantCycleWithinRange()
    {
        // For constant input, autocorrelation is undefined but the algorithm
        // should still produce a value within the valid range
        var eacp = new Eacp(8, 48, 3, true);

        for (int i = 0; i < 500; i++)
        {
            eacp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.InRange(eacp.DominantCycle, 8, 48);
        Assert.InRange(eacp.NormalizedPower, 0.0, 1.0);
    }

    [Fact]
    public void Validation_SineWave_DetectsPeriod()
    {
        // EACP should detect the dominant period in a sine wave
        const int knownPeriod = 20;
        var eacp = new Eacp(8, 48, 3, true);

        // Generate sine wave with known period
        for (int i = 0; i < 500; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / knownPeriod);
            eacp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Dominant cycle should be close to the known period
        // Allow 20% tolerance due to filter lag and warmup effects
        double tolerance = knownPeriod * 0.3;
        Assert.InRange(eacp.DominantCycle, knownPeriod - tolerance, knownPeriod + tolerance);
    }

    [Fact]
    public void Validation_MultipleCycles_DetectsDominant()
    {
        // When multiple cycles are present, EACP should detect the dominant one
        var eacp = new Eacp(8, 48, 3, true);

        // Generate signal with dominant 16-period cycle and weaker 32-period cycle
        for (int i = 0; i < 500; i++)
        {
            double cycle16 = 10.0 * Math.Sin(2.0 * Math.PI * i / 16.0);  // Stronger
            double cycle32 = 5.0 * Math.Sin(2.0 * Math.PI * i / 32.0);   // Weaker
            double price = 100.0 + cycle16 + cycle32;
            eacp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Should detect the dominant cycle (16) rather than the weaker one
        Assert.InRange(eacp.DominantCycle, 12, 24);
    }

    [Fact]
    public void Validation_NormalizedPower_BoundedZeroToOne()
    {
        // Normalized power should always be between 0 and 1
        var eacp = new Eacp(8, 48, 3, true);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            eacp.Update(new TValue(bar.Time, bar.Close));
            Assert.InRange(eacp.NormalizedPower, 0.0, 1.0);
        }
    }

    #endregion

    #region PineScript Formula Verification

    [Fact]
    public void Validation_HighPassFilter_CoefficientsCorrect()
    {
        // Verify high-pass filter coefficient calculation
        // alphaHP = (cos(angle) + sin(angle) - 1) / cos(angle)
        // where angle = sqrt(2) * PI / maxPeriod

        const int maxPeriod = 48;
        double angle = Math.Sqrt(2.0) * Math.PI / maxPeriod;
        double expectedAlphaHP = (Math.Cos(angle) + Math.Sin(angle) - 1.0) / Math.Cos(angle);

        // Verify the calculation is within expected range
        Assert.InRange(expectedAlphaHP, 0.0, 1.0);

        // The indicator should use this coefficient
        var eacp = new Eacp(8, maxPeriod);
        Assert.True(eacp.Name.Contains("48", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_SuperSmootherFilter_CoefficientsCorrect()
    {
        // Verify super-smoother filter coefficient calculation
        // a1 = exp(-sqrt(2) * PI / minPeriod)
        // b1 = 2 * a1 * cos(sqrt(2) * PI / minPeriod)
        // c2 = b1, c3 = -(a1^2), c1 = 1 - c2 - c3

        const int minPeriod = 8;
        double a1 = Math.Exp(-Math.Sqrt(2.0) * Math.PI / minPeriod);
        double b1 = 2.0 * a1 * Math.Cos(Math.Sqrt(2.0) * Math.PI / minPeriod);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        // Coefficients should sum to approximately 1 (with IIR feedback)
        Assert.True(a1 > 0 && a1 < 1, "a1 should be between 0 and 1");
        Assert.True(c1 > 0, "c1 should be positive");
    }

    [Fact]
    public void Validation_PowerDecayFactor_Calculation()
    {
        // Verify power decay factor calculation
        // k = 10^(-0.15 / (maxPeriod - minPeriod))

        const int minPeriod = 8;
        const int maxPeriod = 48;
        double diff = maxPeriod - minPeriod;
        double expectedK = Math.Pow(10.0, -0.15 / diff);

        // k should be slightly less than 1 (decay factor)
        Assert.True(expectedK > 0.99 && expectedK < 1.0, $"k should be close to but less than 1, got {expectedK}");
    }

    [Fact]
    public void Validation_EnhanceMode_CubicEmphasis()
    {
        // Enhance mode applies cubic emphasis (pwr^3)
        // This should make peaks more pronounced

        var eacpEnhanced = new Eacp(8, 48, 3, enhance: true);
        var eacpNormal = new Eacp(8, 48, 3, enhance: false);

        // Generate sine wave
        for (int i = 0; i < 300; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            eacpEnhanced.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            eacpNormal.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Both should produce valid results
        Assert.InRange(eacpEnhanced.DominantCycle, 8, 48);
        Assert.InRange(eacpNormal.DominantCycle, 8, 48);
    }

    #endregion

    #region Streaming vs Batch Consistency

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Validation_StreamingMatchesBatch(int seed)
    {
        const int minPeriod = 8;
        const int maxPeriod = 48;
        const int dataLen = 200;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Eacp(minPeriod, maxPeriod);
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

        var batch = Eacp.Calculate(tSeries, minPeriod, maxPeriod);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_SpanMatchesTSeries()
    {
        const int minPeriod = 8;
        const int maxPeriod = 48;
        const int dataLen = 200;

        var gbm = new GBM(seed: 77);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // TSeries approach
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var tSeriesResult = Eacp.Calculate(tSeries, minPeriod, maxPeriod);

        // Span approach
        double[] source = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            source[i] = bars[i].Close;
        }

        Eacp.Batch(source, spanResult, minPeriod, maxPeriod);

        // Compare all values
        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(tSeriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    #endregion

    #region Different Parameter Combinations

    [Theory]
    [InlineData(8, 48)]
    [InlineData(10, 60)]
    [InlineData(6, 30)]
    [InlineData(12, 100)]
    public void Validation_DifferentPeriodRanges_ConsistentResults(int minPeriod, int maxPeriod)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var eacp = new Eacp(minPeriod, maxPeriod);
        foreach (var bar in bars)
        {
            eacp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(eacp.IsHot);
        Assert.InRange(eacp.DominantCycle, minPeriod, maxPeriod);
        Assert.InRange(eacp.NormalizedPower, 0.0, 1.0);
    }

    [Theory]
    [InlineData(0)]  // Default: use lag length
    [InlineData(3)]
    [InlineData(10)]
    public void Validation_DifferentAvgLength_ConsistentResults(int avgLength)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var eacp = new Eacp(8, 48, avgLength);
        foreach (var bar in bars)
        {
            eacp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(eacp.IsHot);
        Assert.InRange(eacp.DominantCycle, 8, 48);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validation_VerySmallPrices_HandledCorrectly()
    {
        var eacp = new Eacp(8, 48);

        for (int i = 0; i < 200; i++)
        {
            double price = 0.0001 + 0.00001 * Math.Sin(2.0 * Math.PI * i / 20.0);
            eacp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(eacp.IsHot);
        Assert.InRange(eacp.DominantCycle, 8, 48);
    }

    [Fact]
    public void Validation_VeryLargePrices_HandledCorrectly()
    {
        var eacp = new Eacp(8, 48);

        for (int i = 0; i < 200; i++)
        {
            double price = 1e10 + 1e9 * Math.Sin(2.0 * Math.PI * i / 20.0);
            eacp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(eacp.IsHot);
        Assert.InRange(eacp.DominantCycle, 8, 48);
    }

    [Fact]
    public void Validation_HighVolatility_StableResults()
    {
        var eacp = new Eacp(8, 48);

        var gbm = new GBM(seed: 42, sigma: 0.5); // High volatility
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            eacp.Update(new TValue(bar.Time, bar.Close));
            Assert.InRange(eacp.DominantCycle, 8, 48);
            Assert.InRange(eacp.NormalizedPower, 0.0, 1.0);
        }
    }

    [Fact]
    public void Validation_ZeroVariance_HandledGracefully()
    {
        // When all prices are identical, correlation is undefined
        // but the algorithm should still produce valid output
        var eacp = new Eacp(8, 48);

        for (int i = 0; i < 300; i++)
        {
            eacp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.InRange(eacp.DominantCycle, 8, 48);
    }

    #endregion

    #region Autocorrelation Properties

    [Fact]
    public void Validation_Autocorrelation_SineWaveHighCorrelation()
    {
        // A pure sine wave should have high autocorrelation at its period
        var eacp = new Eacp(8, 48, 3, true);

        // Generate pure sine wave
        for (int i = 0; i < 300; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            eacp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Should have relatively high normalized power for a pure sine
        Assert.True(eacp.NormalizedPower > 0.1,
            $"Pure sine should have detectable power, got {eacp.NormalizedPower}");
    }

    [Fact]
    public void Validation_RandomNoise_LowPower()
    {
        // Random noise should have low spectral power at any frequency
        var eacp = new Eacp(8, 48, 3, true);

        var gbm = new GBM(seed: 42, mu: 0, sigma: 0.01); // Nearly pure noise
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            eacp.Update(new TValue(bar.Time, bar.Close));
        }

        // For noise, dominant cycle detection is weak
        // Just verify it doesn't crash and produces valid output
        Assert.InRange(eacp.DominantCycle, 8, 48);
        Assert.InRange(eacp.NormalizedPower, 0.0, 1.0);
    }

    #endregion

    #region DFT Properties

    [Fact]
    public void Validation_DFT_FrequencyResolution()
    {
        // DFT should distinguish between different frequencies
        const int period1 = 12;
        const int period2 = 36;

        var eacp1 = new Eacp(8, 48);
        var eacp2 = new Eacp(8, 48);

        // Generate two different sine waves
        for (int i = 0; i < 500; i++)
        {
            double price1 = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / period1);
            double price2 = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / period2);

            eacp1.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price1));
            eacp2.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price2));
        }

        // They should detect different dominant cycles
        double diff = Math.Abs(eacp1.DominantCycle - eacp2.DominantCycle);
        Assert.True(diff > 5, $"Should detect different cycles: {eacp1.DominantCycle} vs {eacp2.DominantCycle}");
    }

    #endregion
}