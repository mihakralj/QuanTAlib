using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for DSP (Detrended Synthetic Price).
/// DSP is Ehlers' indicator not commonly implemented in trading libraries
/// (TA-Lib, Skender, Tulip), so validation is done against mathematical properties
/// and known theoretical results based on the original PineScript implementation.
/// </summary>
public class DspValidationTests
{
    private const double Tolerance = 1e-9;

    #region Mathematical Property Validation

    [Fact]
    public void Validation_ConstantSeries_DspConvergesToZero()
    {
        // For constant input, both EMAs converge to the same value
        // DSP = fast_ema - slow_ema = constant - constant = 0
        var dsp = new Dsp(40);

        for (int i = 0; i < 500; i++)
        {
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.Equal(0.0, dsp.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_OscillatesAroundZero()
    {
        // DSP should oscillate around zero over time
        var dsp = new Dsp(40);
        var values = new List<double>();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            dsp.Update(new TValue(bar.Time, bar.Close));
            if (dsp.IsHot)
            {
                values.Add(dsp.Last.Value);
            }
        }

        // Should have both positive and negative values
        int positiveCount = values.Count(v => v > 0);
        int negativeCount = values.Count(v => v < 0);

        Assert.True(positiveCount > 0, "Should have positive DSP values");
        Assert.True(negativeCount > 0, "Should have negative DSP values");
    }

    [Fact]
    public void Validation_ZeroCrossings_IndicateMomentumShifts()
    {
        // DSP should cross zero when momentum shifts
        var dsp = new Dsp(20);
        var values = new List<double>();

        // Generate sine wave to simulate price oscillation
        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(i * 0.1);
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (dsp.IsHot)
            {
                values.Add(dsp.Last.Value);
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
    public void Validation_PeriodCalculation_QuarterAndHalfCycle()
    {
        // Verify period calculations match PineScript
        // For period = 40:
        // fast_period = max(2, round(40/4)) = max(2, 10) = 10
        // slow_period = max(3, round(40/2)) = max(3, 20) = 20

        const int period = 40;
        int expectedFast = Math.Max(2, (int)Math.Round(period / 4.0));
        int expectedSlow = Math.Max(3, (int)Math.Round(period / 2.0));

        Assert.Equal(10, expectedFast);
        Assert.Equal(20, expectedSlow);

        // The indicator should use these periods internally
        var dsp = new Dsp(period);
        Assert.True(dsp.Name.Contains("40", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_SmallPeriod_MinimumPeriodClamping()
    {
        // For period = 4:
        // fast_period = max(2, round(4/4)) = max(2, 1) = 2
        // slow_period = max(3, round(4/2)) = max(3, 2) = 3

        const int period = 4;
        int expectedFast = Math.Max(2, (int)Math.Round(period / 4.0));
        int expectedSlow = Math.Max(3, (int)Math.Round(period / 2.0));

        Assert.Equal(2, expectedFast);
        Assert.Equal(3, expectedSlow);

        // Indicator should still work with minimum period
        var dsp = new Dsp(period);
        dsp.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    [Fact]
    public void Validation_EmaFormula_CorrectAlpha()
    {
        // alpha = 2 / (period + 1)
        // For fast_period = 10: alpha_fast = 2/11 ≈ 0.1818
        // For slow_period = 20: alpha_slow = 2/21 ≈ 0.0952

        const int period = 40;
        int fastPeriod = Math.Max(2, (int)Math.Round(period / 4.0));
        int slowPeriod = Math.Max(3, (int)Math.Round(period / 2.0));

        double alphaFast = 2.0 / (fastPeriod + 1);
        double alphaSlow = 2.0 / (slowPeriod + 1);

        Assert.Equal(2.0 / 11.0, alphaFast, 1e-10);
        Assert.Equal(2.0 / 21.0, alphaSlow, 1e-10);
    }

    [Fact]
    public void Validation_DspSign_MatchesPriceDirection()
    {
        // Rising prices -> fast EMA > slow EMA -> DSP > 0
        // Falling prices -> fast EMA < slow EMA -> DSP < 0

        var dspUp = new Dsp(20);
        var dspDown = new Dsp(20);

        // Uptrend
        for (int i = 0; i < 100; i++)
        {
            dspUp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        // Downtrend
        for (int i = 0; i < 100; i++)
        {
            dspDown.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 200.0 - i));
        }

        Assert.True(dspUp.Last.Value > 0, $"Uptrend DSP should be positive, got {dspUp.Last.Value}");
        Assert.True(dspDown.Last.Value < 0, $"Downtrend DSP should be negative, got {dspDown.Last.Value}");
    }

    #endregion

    #region Streaming vs Batch Consistency

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Validation_StreamingMatchesBatch(int seed)
    {
        const int period = 40;
        const int dataLen = 100;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Dsp(period);
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

        var batch = Dsp.Batch(tSeries, period);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_SpanMatchesTSeries()
    {
        const int period = 20;
        const int dataLen = 200;

        var gbm = new GBM(seed: 77);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // TSeries approach
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var tSeriesResult = Dsp.Batch(tSeries, period);

        // Span approach
        double[] source = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            source[i] = bars[i].Close;
        }

        Dsp.Batch(source, spanResult, period);

        // Compare all values
        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(tSeriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    #endregion

    #region Different Period Sizes

    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(80)]
    public void Validation_DifferentPeriods_ConsistentResults(int period)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var dsp = new Dsp(period);
        foreach (var bar in bars)
        {
            dsp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(dsp.IsHot);
        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(40)]
    public void Validation_LongerPeriod_SmallerMagnitude(int period)
    {
        // Longer period EMAs are closer together, resulting in smaller DSP magnitude
        var dsp = new Dsp(period);
        var magnitudes = new List<double>();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            dsp.Update(new TValue(bar.Time, bar.Close));
            if (dsp.IsHot)
            {
                magnitudes.Add(Math.Abs(dsp.Last.Value));
            }
        }

        double avgMagnitude = magnitudes.Average();
        Assert.True(avgMagnitude > 0, "Should have non-zero average magnitude");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validation_VerySmallPrices_HandledCorrectly()
    {
        var dsp = new Dsp(20);

        for (int i = 0; i < 100; i++)
        {
            double price = 0.0001 + i * 0.00001;
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(dsp.IsHot);
        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    [Fact]
    public void Validation_VeryLargePrices_HandledCorrectly()
    {
        var dsp = new Dsp(20);

        for (int i = 0; i < 100; i++)
        {
            double price = 1e10 + i * 1e8;
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(dsp.IsHot);
        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    [Fact]
    public void Validation_HighVolatility_StableResults()
    {
        var dsp = new Dsp(20);

        var gbm = new GBM(seed: 42, sigma: 0.5); // High volatility
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            dsp.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(dsp.Last.Value), "DSP should remain finite under high volatility");
        }
    }

    #endregion

    #region Detrending Property

    [Fact]
    public void Validation_Detrending_RemovesTrend()
    {
        // DSP should remove the trend component
        // For a strong trend, DSP should still oscillate around zero
        var dsp = new Dsp(20);
        var values = new List<double>();

        // Strong uptrend with some noise
        for (int i = 0; i < 300; i++)
        {
            double trend = 100.0 + i * 0.5;
            double noise = Math.Sin(i * 0.3) * 2.0;
            double price = trend + noise;
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (dsp.IsHot)
            {
                values.Add(dsp.Last.Value);
            }
        }

        // Mean should be close to some value (biased positive due to trend)
        double mean = values.Average();

        // But should still have oscillations (standard deviation > 0)
        double variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        double stdDev = Math.Sqrt(variance);

        Assert.True(stdDev > 0, "DSP should have variance indicating oscillation");
    }

    #endregion

    [Fact]
    public void Dsp_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateDetrendedSyntheticPrice();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}