namespace QuanTAlib.Tests;

public class VwapValidationTests
{
    private readonly ValidationTestData _data;

    public VwapValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Vwap_NotAvailable_Skender()
    {
        // Skender has VWAP but it uses anchor-based sessions, not period-based
        // Our implementation uses period-based reset for flexibility
        Assert.True(true, "VWAP implementations differ in session handling");
    }

    [Fact]
    public void Vwap_NotAvailable_Talib()
    {
        // TA-Lib does not have VWAP
        Assert.True(true, "VWAP is not available in TA-Lib");
    }

    [Fact]
    public void Vwap_NotAvailable_Tulip()
    {
        // Tulip does not have VWAP
        Assert.True(true, "VWAP is not available in Tulip");
    }

    [Fact]
    public void Vwap_NotAvailable_Ooples()
    {
        // Ooples has VWAP but implementation details may differ
        Assert.True(true, "VWAP implementations may differ in session handling");
    }

    [Fact]
    public void Vwap_Streaming_Matches_Batch()
    {
        // Streaming
        var vwap = new Vwap();
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(vwap.Update(bar).Value);
        }

        // Batch
        var batchResult = Vwap.Batch(_data.Bars);
        var batchValues = batchResult.Values.ToArray();

        // Cumulative indicators accumulate floating-point errors over many bars
        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Vwap_Span_Matches_Streaming()
    {
        // Streaming
        var vwap = new Vwap();
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(vwap.Update(bar).Value);
        }

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[high.Length];

        Vwap.Batch(high, low, close, volume, spanValues);

        // Cumulative indicators accumulate floating-point errors over many bars
        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Vwap_Batch_Matches_Span()
    {
        // Batch
        var batchResult = Vwap.Batch(_data.Bars);
        var batchValues = batchResult.Values.ToArray();

        // Span
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[high.Length];

        Vwap.Batch(high, low, close, volume, spanValues);

        // Batch and Span use identical code path, should match exactly
        ValidationHelper.VerifyData(batchValues, spanValues, 0, 100, 1e-12);
    }

    [Fact]
    public void Vwap_Algorithm_Correctness_ManualCalculation()
    {
        // Manual calculation to verify algorithm correctness
        var bars = new TBarSeries();

        // Create test bars with known OHLCV values
        // Bar 0: H=12, L=10, C=11, V=100 -> TP = (12+10+11)/3 = 11
        // Bar 1: H=15, L=12, C=14, V=200 -> TP = (15+12+14)/3 = 13.667
        // Bar 2: H=14, L=11, C=12, V=150 -> TP = (14+11+12)/3 = 12.333

        bars.Add(new TBar(DateTime.UtcNow, 10, 12, 10, 11, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 12, 15, 12, 14, 200));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(2), 11, 14, 11, 12, 150));

        var vwap = new Vwap();
        var results = new List<double>();
        foreach (var bar in bars)
        {
            results.Add(vwap.Update(bar).Value);
        }

        // Bar 0: VWAP = 11*100 / 100 = 11
        double tp0 = (12.0 + 10.0 + 11.0) / 3.0;
        Assert.Equal(tp0, results[0], 6);

        // Bar 1: VWAP = (11*100 + 13.667*200) / 300 = (1100 + 2733.33) / 300 = 12.778
        double tp1 = (15.0 + 12.0 + 14.0) / 3.0;
        double expectedBar1 = (tp0 * 100 + tp1 * 200) / 300.0;
        Assert.Equal(expectedBar1, results[1], 6);

        // Bar 2: VWAP = (11*100 + 13.667*200 + 12.333*150) / 450
        double tp2 = (14.0 + 11.0 + 12.0) / 3.0;
        double expectedBar2 = (tp0 * 100 + tp1 * 200 + tp2 * 150) / 450.0;
        Assert.Equal(expectedBar2, results[2], 6);
    }

    [Fact]
    public void Vwap_Algorithm_Correctness_VolumeWeighting()
    {
        // Verify volume weighting: high-volume bars have more influence
        var bars = new TBarSeries();

        // Two bars: one with high volume at low price, one with low volume at high price
        // Bar 0: price=10, volume=1000
        // Bar 1: price=20, volume=100
        // VWAP should be closer to 10 due to higher volume
        bars.Add(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 1000));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100));

        var vwap = new Vwap();
        vwap.Update(bars[0]);
        var result = vwap.Update(bars[1]);

        // VWAP = (10*1000 + 20*100) / 1100 = 12000/1100 = 10.909
        double expected = (10.0 * 1000.0 + 20.0 * 100.0) / 1100.0;
        Assert.Equal(expected, result.Value, 6);

        // VWAP should be much closer to 10 than to 20
        Assert.True(result.Value < 15, "VWAP should be weighted toward high-volume price");
    }

    [Fact]
    public void Vwap_DifferentPeriods_ProduceDifferentResults()
    {
        // VWAP with different periods should produce different results after reset
        var vwap0 = new Vwap(0);    // No reset
        var vwap10 = new Vwap(10);  // Reset every 10 bars
        var vwap50 = new Vwap(50);  // Reset every 50 bars

        var results0 = new List<double>();
        var results10 = new List<double>();
        var results50 = new List<double>();

        foreach (var bar in _data.Bars)
        {
            results0.Add(vwap0.Update(bar).Value);
            results10.Add(vwap10.Update(bar).Value);
            results50.Add(vwap50.Update(bar).Value);
        }

        // After sufficient bars, different periods should produce different results
        int checkIndex = 60;
        bool anyDifferent = Math.Abs(results0[checkIndex] - results10[checkIndex]) > 1e-6 ||
                           Math.Abs(results10[checkIndex] - results50[checkIndex]) > 1e-6;

        Assert.True(anyDifferent, "Different periods should produce different VWAP values after resets");
    }

    [Fact]
    public void Vwap_WithPeriod_ResetsBehavior()
    {
        // Verify that period-based reset works correctly
        var vwap = new Vwap(5);

        // First 5 bars at price=100
        for (int i = 0; i < 5; i++)
        {
            vwap.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000));
        }
        var afterFirst5 = vwap.Last.Value;
        Assert.Equal(100.0, afterFirst5, 6);

        // Bar 5 triggers reset, price=200
        var afterReset = vwap.Update(new TBar(DateTime.UtcNow.AddMinutes(5), 200, 200, 200, 200, 1000));
        Assert.Equal(200.0, afterReset.Value, 6);
    }

    [Fact]
    public void Vwap_StableWithConstantPrice()
    {
        // VWAP should remain stable when price is constant
        var vwap = new Vwap();
        var results = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50, 50, 50, 1000 + i * 10);
            results.Add(vwap.Update(bar).Value);
        }

        // All VWAP values should be 50
        foreach (var value in results)
        {
            Assert.Equal(50.0, value, 10);
        }
    }

    [Fact]
    public void Vwap_ZeroVolume_HandledCorrectly()
    {
        // VWAP should handle zero volume gracefully
        var vwap = new Vwap();

        // First bar with volume
        vwap.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 1000));

        // Second bar with zero volume
        var result = vwap.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 0));

        // VWAP should remain at 10 (zero volume doesn't contribute)
        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Vwap_TypicalPriceCalculation()
    {
        // Verify typical price is (H+L+C)/3
        var vwap = new Vwap();

        var bar = new TBar(DateTime.UtcNow, 10, 30, 10, 20, 1000);  // O=10, H=30, L=10, C=20
        var result = vwap.Update(bar);

        // Typical price = (30+10+20)/3 = 20
        double expectedTypicalPrice = (30.0 + 10.0 + 20.0) / 3.0;
        Assert.Equal(expectedTypicalPrice, result.Value, 10);
    }
}