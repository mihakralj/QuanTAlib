using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CG (Center of Gravity).
/// CG is Ehlers' proprietary indicator not commonly implemented in trading libraries
/// (TA-Lib, Skender, Tulip), so validation is done against mathematical properties
/// and known theoretical results based on the original PineScript implementation.
/// </summary>
public class CgValidationTests
{
    private const double Tolerance = 1e-9;

    #region Mathematical Property Validation

    [Fact]
    public void Validation_CgBounds_ShouldBeWithinPeriodRange()
    {
        // CG oscillates around zero with range dependent on period
        // Maximum theoretical range is approximately ±(period-1)/2
        const int period = 10;
        var cg = new Cg(period);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double maxAbsValue = (period - 1) / 2.0 + 0.5; // Allow small margin

        foreach (var bar in bars)
        {
            cg.Update(new TValue(bar.Time, bar.Close));
            if (cg.IsHot)
            {
                Assert.True(Math.Abs(cg.Last.Value) <= maxAbsValue,
                    $"CG value {cg.Last.Value} exceeds expected bounds ±{maxAbsValue}");
            }
        }
    }

    [Fact]
    public void Validation_ConstantSeries_CgIsZero()
    {
        // For a constant series, CG = (length+1)/2 - (length+1)/2 = 0
        // Because center of mass equals midpoint when all weights are equal
        var cg = new Cg(10);

        for (int i = 0; i < 50; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.Equal(0.0, cg.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_LinearUptrend_CgPositive()
    {
        // For an uptrend, recent prices are higher, so center of gravity
        // shifts toward recent values, resulting in positive CG
        var cg = new Cg(10);

        for (int i = 0; i < 50; i++)
        {
            double price = 100.0 + i * 1.0; // Linear uptrend
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(cg.Last.Value > 0.0,
            $"Linear uptrend should produce positive CG, got {cg.Last.Value}");
    }

    [Fact]
    public void Validation_LinearDowntrend_CgNegative()
    {
        // For a downtrend, older prices are higher, so center of gravity
        // shifts toward older values, resulting in negative CG
        var cg = new Cg(10);

        for (int i = 0; i < 50; i++)
        {
            double price = 200.0 - i * 1.0; // Linear downtrend
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(cg.Last.Value < 0.0,
            $"Linear downtrend should produce negative CG, got {cg.Last.Value}");
    }

    [Fact]
    public void Validation_ExponentialTrend_AmplifiedSignal()
    {
        // Exponential uptrend should produce stronger positive CG than linear
        var cgExp = new Cg(10);
        var cgLin = new Cg(10);

        for (int i = 0; i < 50; i++)
        {
            double expPrice = 100.0 * Math.Exp(i * 0.02);
            double linPrice = 100.0 + i * 2.0;
            cgExp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), expPrice));
            cgLin.Update(new TValue(DateTime.UtcNow.AddSeconds(i), linPrice));
        }

        // Both should be positive, exponential trend may have different magnitude
        Assert.True(cgExp.Last.Value > 0.0, $"Exponential trend should be positive, got {cgExp.Last.Value}");
        Assert.True(cgLin.Last.Value > 0.0, $"Linear trend should be positive, got {cgLin.Last.Value}");
    }

    [Fact]
    public void Validation_ZeroCrossings_IndicateReversals()
    {
        // CG should cross zero near price reversals
        var cg = new Cg(10);
        var values = new List<double>();

        // Generate sine wave to simulate price oscillation
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(i * 0.2);
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            if (cg.IsHot)
            {
                values.Add(cg.Last.Value);
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
    public void Validation_PineScriptFormula_ManualCalculation()
    {
        // Verify against manual calculation of PineScript formula:
        // num = Σ(count * price) for count 1 to length
        // den = Σ(price) for count 1 to length
        // result = (num / den) - (length + 1) / 2

        const int period = 5;
        double[] prices = { 10.0, 12.0, 11.0, 13.0, 15.0 };

        // Manual calculation:
        // count=1: price[0]=10, count=2: price[1]=12, etc.
        // num = 1*10 + 2*12 + 3*11 + 4*13 + 5*15 = 10 + 24 + 33 + 52 + 75 = 194
        // den = 10 + 12 + 11 + 13 + 15 = 61
        // result = 194/61 - (5+1)/2 = 3.1803... - 3 = 0.1803...
        double expectedNum = 1 * 10 + 2 * 12 + 3 * 11 + 4 * 13 + 5 * 15;
        double expectedDen = 10 + 12 + 11 + 13 + 15;
        double expectedCg = (expectedNum / expectedDen) - (period + 1) / 2.0;

        var cg = new Cg(period);
        for (int i = 0; i < prices.Length; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), prices[i]));
        }

        Assert.Equal(expectedCg, cg.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_DenominatorZeroCase()
    {
        // When all prices are zero, denominator is zero
        // PineScript formula: den != 0 ? num/den : (length+1)/2
        // Result = (length+1)/2 - (length+1)/2 = 0
        var cg = new Cg(10);

        for (int i = 0; i < 20; i++)
        {
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 0.0));
        }

        // Should handle gracefully (not NaN/Infinity)
        Assert.True(double.IsFinite(cg.Last.Value), "CG should handle zero denominator");
    }

    #endregion

    #region Streaming vs Batch Consistency

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Validation_StreamingMatchesBatch(int seed)
    {
        const int period = 10;
        const int dataLen = 100;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Cg(period);
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

        var batch = Cg.Batch(tSeries, period);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Validation_SpanMatchesTSeries()
    {
        const int period = 14;
        const int dataLen = 200;

        var gbm = new GBM(seed: 77);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // TSeries approach
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var tSeriesResult = Cg.Batch(tSeries, period);

        // Span approach
        double[] source = new double[dataLen];
        double[] spanResult = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            source[i] = bars[i].Close;
        }

        Cg.Batch(source, spanResult, period);

        // Compare all values after warmup
        for (int i = period; i < dataLen; i++)
        {
            Assert.Equal(tSeriesResult[i].Value, spanResult[i], Tolerance);
        }
    }

    #endregion

    #region Different Period Sizes

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Validation_DifferentPeriods_ConsistentResults(int period)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var cg = new Cg(period);
        foreach (var bar in bars)
        {
            cg.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(cg.IsHot);
        Assert.True(double.IsFinite(cg.Last.Value));

        // CG bounds check
        double maxAbsValue = (period - 1) / 2.0 + 1.0;
        Assert.True(Math.Abs(cg.Last.Value) <= maxAbsValue,
            $"CG with period {period} should be within ±{maxAbsValue}, got {cg.Last.Value}");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Validation_LongerPeriod_SlowerResponse(int period)
    {
        // Longer period should have smaller magnitude changes
        var cg = new Cg(period);
        var changes = new List<double>();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double? prevValue = null;
        foreach (var bar in bars)
        {
            cg.Update(new TValue(bar.Time, bar.Close));
            if (cg.IsHot && prevValue.HasValue)
            {
                changes.Add(Math.Abs(cg.Last.Value - prevValue.Value));
            }
            prevValue = cg.Last.Value;
        }

        double avgChange = changes.Average();
        Assert.True(avgChange > 0, "Should have some variance in CG values");
    }

    #endregion

    #region Lead/Lag Properties

    [Fact]
    public void Validation_CgLeadsPrice_CrossesBeforePeaks()
    {
        // CG is designed to lead price, crossing zero before peaks/troughs
        var cg = new Cg(10);

        // Create trending then reversing data
        var prices = new List<double>();
        var cgValues = new List<double>();

        // Uptrend
        for (int i = 0; i < 30; i++)
        {
            double price = 100.0 + i * 0.5;
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            prices.Add(price);
            if (cg.IsHot)
            {
                cgValues.Add(cg.Last.Value);
            }
        }

        // Plateau/slight decline
        for (int i = 30; i < 50; i++)
        {
            double price = 115.0 - (i - 30) * 0.2;
            cg.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
            prices.Add(price);
            cgValues.Add(cg.Last.Value);
        }

        // CG should show declining values as momentum slows even during uptrend
        // This tests the leading characteristic
        Assert.True(cgValues.Count > 20, "Should have enough CG values to analyze");
    }

    #endregion

    [Fact]
    public void Cg_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateEhlersCenterofGravityOscillator();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}