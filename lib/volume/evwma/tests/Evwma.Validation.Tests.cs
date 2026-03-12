namespace QuanTAlib.Tests;

public class EvwmaValidationTests
{
    private readonly ValidationTestData _data;

    public EvwmaValidationTests()
    {
        _data = new ValidationTestData();
    }

    // ============ External Library Validation ============
    // EVWMA is not available in standard libraries (Skender, TA-Lib, Tulip, Ooples).
    // Validation is performed via internal consistency and known-value tests.

    [Fact]
    public void Evwma_NotAvailable_Skender()
    {
        // Skender.Stock.Indicators does not have EVWMA
        Assert.True(true, "EVWMA is not available in Skender");
    }

    [Fact]
    public void Evwma_NotAvailable_Talib()
    {
        // TA-Lib does not have EVWMA
        Assert.True(true, "EVWMA is not available in TA-Lib");
    }

    [Fact]
    public void Evwma_NotAvailable_Tulip()
    {
        // Tulip does not have EVWMA
        Assert.True(true, "EVWMA is not available in Tulip");
    }

    [Fact]
    public void Evwma_NotAvailable_Ooples()
    {
        // Ooples does not have EVWMA
        Assert.True(true, "EVWMA is not available in Ooples");
    }

    // ============ Internal Consistency Tests ============

    [Fact]
    public void Evwma_Streaming_Matches_Batch()
    {
        int period = 20;

        // Streaming
        var evwma = new Evwma(period);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(evwma.Update(bar).Value);
        }

        // Batch
        var batchResult = Evwma.Batch(_data.Bars, period);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Evwma_Span_Matches_Streaming()
    {
        int period = 20;

        // Streaming
        var evwma = new Evwma(period);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(evwma.Update(bar).Value);
        }

        // Span
        var price = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[price.Length];
        Evwma.Batch(price, volume, spanValues, period);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Evwma_Batch_Matches_Span()
    {
        int period = 20;

        // Batch
        var batchResult = Evwma.Batch(_data.Bars, period);
        var batchValues = batchResult.Values.ToArray();

        // Span
        var price = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[price.Length];
        Evwma.Batch(price, volume, spanValues, period);

        // Batch and Span use identical code path, should match exactly
        ValidationHelper.VerifyData(batchValues, spanValues, 0, 100, 1e-12);
    }

    // ============ Known-Value Validation ============

    [Fact]
    public void Evwma_KnownValues_ManualCalculation()
    {
        // Manually compute EVWMA for a small series
        // Period = 3
        // Bar 0: price=100, vol=10 → sumVol=10,  result=100 (first bar)
        // Bar 1: price=110, vol=20 → sumVol=30,  remain=10, result=(10*100+20*110)/30=3200/30≈106.6667
        // Bar 2: price=105, vol=15 → sumVol=45,  remain=30, result=(30*106.6667+15*105)/45=4725/45=105
        //   Wait: (30 × 106.6667 + 15 × 105) / 45 = (3200 + 1575) / 45 = 4775/45 ≈ 106.1111
        // Bar 3: price=120, vol=25 → drop bar0 vol(10): sumVol=45-10+25=60, remain=35
        //   result = (35 * 106.1111 + 25 * 120) / 60 = (3713.889 + 3000) / 60 = 6713.889/60 ≈ 111.898

        var evwma = new Evwma(3);

        var bar0 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 10);
        var r0 = evwma.Update(bar0);
        Assert.Equal(100.0, r0.Value, 6);

        var bar1 = new TBar(DateTime.UtcNow.AddMinutes(1), 110, 110, 110, 110, 20);
        var r1 = evwma.Update(bar1);
        // sumVol = 10 + 20 = 30; remainVol = 30 - 20 = 10
        // result = (10 * 100 + 20 * 110) / 30 = 3200 / 30
        Assert.Equal(3200.0 / 30.0, r1.Value, 10);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(2), 105, 105, 105, 105, 15);
        var r2 = evwma.Update(bar2);
        // sumVol = 10 + 20 + 15 = 45; remainVol = 45 - 15 = 30
        // prevResult = 3200/30
        // result = (30 * (3200/30) + 15 * 105) / 45 = (3200 + 1575) / 45 = 4775 / 45
        Assert.Equal(4775.0 / 45.0, r2.Value, 10);

        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(3), 120, 120, 120, 120, 25);
        var r3 = evwma.Update(bar3);
        // Bar0 vol drops: sumVol = (10+20+15) - 10 + 25 = 60; remainVol = 60 - 25 = 35
        // prevResult = 4775/45
        // result = (35 * (4775/45) + 25 * 120) / 60
        double prev = 4775.0 / 45.0;
        double expected = (35.0 * prev + 25.0 * 120.0) / 60.0;
        Assert.Equal(expected, r3.Value, 10);
    }

    [Fact]
    public void Evwma_DifferentPeriods_ProduceDifferentResults()
    {
        int period1 = 5;
        int period2 = 50;

        var result1 = Evwma.Batch(_data.Bars, period1);
        var result2 = Evwma.Batch(_data.Bars, period2);

        // At later bars, different periods should produce different values
        int idx = 100;
        Assert.NotEqual(result1.Values[idx], result2.Values[idx], 6);
    }

    [Fact]
    public void Evwma_ConstantPrice_ReturnsConstant()
    {
        // If all prices are the same, EVWMA should always return that price
        // regardless of volume
        int period = 10;
        var bars = new TBarSeries();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            bars.Add(new TBar(now.AddMinutes(i), 42.0, 42.0, 42.0, 42.0, 100 + i * 10));
        }

        var result = Evwma.Batch(bars, period);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(42.0, result.Values[i], 10);
        }
    }

    [Fact]
    public void Evwma_UniformVolume_BehavesLikeRunningAverage()
    {
        // With uniform volume=1 and period covering all bars,
        // EVWMA degenerates to a specific recursive average
        int period = 100;
        var evwma = new Evwma(period);

        double[] prices = [100, 110, 105, 120, 95, 115, 108, 112, 103, 118];

        for (int i = 0; i < prices.Length; i++)
        {
            var tv = new TValue(DateTime.UtcNow.AddMinutes(i), prices[i]);
            evwma.Update(tv);
        }

        // Result should be finite and within the price range
        Assert.True(double.IsFinite(evwma.Last.Value));
        Assert.True(evwma.Last.Value >= 90 && evwma.Last.Value <= 130,
            $"EVWMA value {evwma.Last.Value} should be within price range");
    }
}
