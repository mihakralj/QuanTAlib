namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ADXVMA (ADX Variable Moving Average).
/// ADXVMA is a unique adaptive IIR filter using ADX as the smoothing constant.
/// No standard external library implements this exact algorithm, so we validate
/// mathematical properties and internal consistency.
/// </summary>
public class AdxvmaValidationTests
{
    private const double Tolerance = 1e-10;

    // ==================== Property Validation ====================

    /// <summary>
    /// When input is constant, ADXVMA output should equal the input value.
    /// With constant bars (O=H=L=C), TR=0, DM=0, ADX→0, sc→0.
    /// Result should converge to the constant close.
    /// </summary>
    [Fact]
    public void Adxvma_ConstantInput_OutputEqualsInput()
    {
        var adxvma = new Adxvma();
        const double constantValue = 42.5;

        for (int i = 0; i < 200; i++)
        {
            adxvma.Update(new TValue(DateTime.UtcNow, constantValue), isNew: true);
        }

        Assert.Equal(constantValue, adxvma.Last.Value, Tolerance);
    }

    /// <summary>
    /// With constant OHLC bars, ADXVMA should converge to the close price.
    /// </summary>
    [Fact]
    public void Adxvma_ConstantOHLC_OutputEqualsClose()
    {
        var adxvma = new Adxvma();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 200; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 100, 100, 100, 1000);
            adxvma.Update(bar, isNew: true);
        }

        Assert.Equal(100.0, adxvma.Last.Value, Tolerance);
    }

    /// <summary>
    /// ADXVMA output should always be within the range of input values (no overshoot).
    /// </summary>
    [Fact]
    public void Adxvma_OutputWithinInputRange()
    {
        var adxvma = new Adxvma();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 123);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double minInput = double.MaxValue;
        double maxInput = double.MinValue;
        var outputs = new List<double>();

        foreach (var bar in bars)
        {
            minInput = Math.Min(minInput, bar.Close);
            maxInput = Math.Max(maxInput, bar.Close);
            var result = adxvma.Update(bar, isNew: true);
            outputs.Add(result.Value);
        }

        // Skip warmup period
        var hotOutputs = outputs.Skip(28).ToList();

        foreach (var output in hotOutputs)
        {
            Assert.True(output >= minInput - 1 && output <= maxInput + 1,
                $"Output {output} should be within input range [{minInput}, {maxInput}]");
        }
    }

    /// <summary>
    /// ADXVMA should be continuous - no sudden jumps in output.
    /// </summary>
    [Fact]
    public void Adxvma_OutputIsContinuous()
    {
        var adxvma = new Adxvma();
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.1, seed: 456);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var outputs = new List<double>();
        foreach (var bar in bars)
        {
            var result = adxvma.Update(bar, isNew: true);
            outputs.Add(result.Value);
        }

        // After warmup, consecutive outputs should not jump more than input range
        for (int i = 29; i < outputs.Count; i++)
        {
            double delta = Math.Abs(outputs[i] - outputs[i - 1]);
            Assert.True(delta < 50,
                $"Jump of {delta} at index {i} is too large for a smoothed indicator");
        }
    }

    // ==================== Streaming/Batch Equivalence ====================

    /// <summary>
    /// Batch and streaming calculations should produce identical results for TBarSeries.
    /// </summary>
    [Fact]
    public void Adxvma_BatchAndStreaming_TBarSeries_Match()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Batch
        var batchResults = Adxvma.Batch(bars, period: 14);

        // Streaming
        var streaming = new Adxvma(period: 14);
        var streamResults = new List<double>();
        foreach (var bar in bars)
        {
            streamResults.Add(streaming.Update(bar, isNew: true).Value);
        }

        Assert.Equal(batchResults.Count, streamResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, streamResults[i], Tolerance);
        }
    }

    /// <summary>
    /// Batch and streaming calculations should produce identical results for TSeries.
    /// </summary>
    [Fact]
    public void Adxvma_BatchAndStreaming_TSeries_Match()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Batch
        var batchResults = Adxvma.Batch(series, period: 14);

        // Streaming
        var streaming = new Adxvma(period: 14);
        var streamResults = new List<double>();
        foreach (var tv in series)
        {
            streamResults.Add(streaming.Update(tv, isNew: true).Value);
        }

        Assert.Equal(batchResults.Count, streamResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, streamResults[i], Tolerance);
        }
    }

    // ==================== ADX-Specific Behavior ====================

    /// <summary>
    /// In a strong consistent trend, ADX rises, sc approaches 1, ADXVMA tracks price.
    /// </summary>
    [Fact]
    public void Adxvma_StrongTrend_TracksPrice()
    {
        var adxvma = new Adxvma(period: 14);
        var time = DateTime.UtcNow;

        // Strong uptrend: each bar H > prev H, L > prev L, consistent +DM
        for (int i = 0; i < 100; i++)
        {
            double basePrice = 100 + i * 1.5;
            var bar = new TBar(time.AddMinutes(i), basePrice, basePrice + 2, basePrice - 1, basePrice + 1, 1000);
            adxvma.Update(bar, isNew: true);
        }

        double adxvmaVal = adxvma.Last.Value;

        // In a strong uptrend after 100 bars, ADXVMA should be reasonably close to recent prices
        Assert.True(adxvmaVal > 130, $"In strong uptrend, ADXVMA ({adxvmaVal:F2}) should be well above 130");
    }

    /// <summary>
    /// In a choppy/range-bound market, ADX is low, sc approaches 0, ADXVMA barely moves.
    /// </summary>
    [Fact]
    public void Adxvma_ChoppyMarket_FlattensOutput()
    {
        var adxvma = new Adxvma(period: 14);
        var time = DateTime.UtcNow;

        // Warm up with some data
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 102, 98, 100, 1000);
            adxvma.Update(bar, isNew: true);
        }

        // Feed choppy bars: alternating up/down moves cancel out → ADX stays low
        for (int i = 50; i < 150; i++)
        {
            double price = 100 + Math.Sin(i * 0.5) * 2; // oscillating around 100
            var bar = new TBar(time.AddMinutes(i), price, price + 1, price - 1, price, 1000);
            adxvma.Update(bar, isNew: true);
        }

        double choppyValue = adxvma.Last.Value;

        // In a choppy market, ADXVMA should stay near the center and not deviate much
        Assert.True(Math.Abs(choppyValue - 100) < 10,
            $"In choppy market, ADXVMA ({choppyValue:F2}) should stay near 100");
    }

    // ==================== Different Period Validation ====================

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    [InlineData(28)]
    public void Adxvma_DifferentPeriods_AllProduceValidResults(int period)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 789);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = Adxvma.Batch(bars, period: period);

        Assert.Equal(300, result.Count);
        Assert.All(result, tv => Assert.True(double.IsFinite(tv.Value)));
    }

    /// <summary>
    /// Longer periods should produce smoother output (lower variance in consecutive changes).
    /// </summary>
    [Fact]
    public void Adxvma_LongerPeriod_SmootherOutput()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results7 = Adxvma.Batch(bars, period: 7);
        var results28 = Adxvma.Batch(bars, period: 28);

        // Calculate variance of consecutive changes for each
        static double ChangeVariance(TSeries s, int skip)
        {
            double sum = 0;
            double sumSq = 0;
            int count = 0;
            for (int i = skip + 1; i < s.Count; i++)
            {
                double d = s[i].Value - s[i - 1].Value;
                sum += d;
                sumSq += d * d;
                count++;
            }
            double mean = sum / count;
            return (sumSq / count) - (mean * mean);
        }

        double var7 = ChangeVariance(results7, 14);
        double var28 = ChangeVariance(results28, 56);

        // Longer period should have smaller change variance
        Assert.True(var28 < var7,
            $"Period 28 variance ({var28:F6}) should be less than period 7 ({var7:F6})");
    }

    /// <summary>
    /// TBar and TValue (with same close data) should produce different results
    /// since TBar provides actual OHLC data while TValue creates synthetic bars with TR=0.
    /// </summary>
    [Fact]
    public void Adxvma_TBarVsTValue_DifferentResults()
    {
        var adxvmaTBar = new Adxvma(period: 14);
        var adxvmaTValue = new Adxvma(period: 14);
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            adxvmaTBar.Update(bar, isNew: true);
            adxvmaTValue.Update(new TValue(bar.Time, bar.Close), isNew: true);
        }

        // TBar has real OHLC → real TR/DM/ADX
        // TValue creates synthetic bar with TR=0 → ADX→0 → sc→0 → flat
        // They should differ
        Assert.NotEqual(adxvmaTBar.Last.Value, adxvmaTValue.Last.Value);
    }
}
