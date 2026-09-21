namespace QuanTAlib.Tests;

public class RwmaValidationTests
{
    private readonly ValidationTestData _data;

    public RwmaValidationTests()
    {
        _data = new ValidationTestData();
    }

    // ============ External Library Validation ============
    // RWMA is not available in Skender, TA-Lib, Tulip, or Ooples.
    // Validation focuses on internal consistency and algorithm correctness.

    [Fact]
    public void Rwma_NotAvailable_Skender()
    {
        Assert.True(true, "RWMA is not available in Skender.Stock.Indicators");
    }

    [Fact]
    public void Rwma_NotAvailable_TaLib()
    {
        Assert.True(true, "RWMA is not available in TA-Lib");
    }

    [Fact]
    public void Rwma_NotAvailable_Tulip()
    {
        Assert.True(true, "RWMA is not available in Tulip");
    }

    [Fact]
    public void Rwma_NotAvailable_Ooples()
    {
        Assert.True(true, "RWMA is not available in OoplesFinance");
    }

    // ============ Internal Consistency Tests ============

    [Fact]
    public void Rwma_Streaming_Matches_Batch()
    {
        int period = 14;

        // Streaming
        var rwma = new Rwma(period);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(rwma.Update(bar).Value);
        }

        // Batch
        var batchResult = Rwma.Batch(_data.Bars, period);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Rwma_Span_Matches_Streaming()
    {
        int period = 14;

        // Streaming
        var rwma = new Rwma(period);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(rwma.Update(bar).Value);
        }

        // Span
        var close = _data.Bars.Close.Values.ToArray();
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var spanValues = new double[close.Length];
        Rwma.Batch(close, high, low, spanValues, period);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Rwma_Batch_Matches_Span()
    {
        int period = 14;

        // Batch
        var batchResult = Rwma.Batch(_data.Bars, period);
        var batchValues = batchResult.Values.ToArray();

        // Span
        var close = _data.Bars.Close.Values.ToArray();
        var high = _data.Bars.High.Values.ToArray();
        var low = _data.Bars.Low.Values.ToArray();
        var spanValues = new double[close.Length];
        Rwma.Batch(close, high, low, spanValues, period);

        // Batch and Span use identical code path, should match exactly
        ValidationHelper.VerifyData(batchValues, spanValues, 0, 100, 1e-12);
    }

    // ============ Algorithm Correctness Tests ============

    [Fact]
    public void Rwma_Algorithm_Correctness_ManualCalculation()
    {
        // Manual calculation to verify algorithm correctness
        var bars = new TBarSeries();

        // Bar 0: close=10, high=15, low=5   → range=10
        // Bar 1: close=20, high=24, low=18  → range=6
        // Bar 2: close=30, high=35, low=25  → range=10
        bars.Add(new TBar(DateTime.UtcNow, 10, 15, 5, 10, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 24, 18, 20, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(2), 30, 35, 25, 30, 100));

        var rwma = new Rwma(10);  // Period larger than data
        var results = new List<double>();
        foreach (var bar in bars)
        {
            results.Add(rwma.Update(bar).Value);
        }

        // Bar 0: RWMA = 10*10 / 10 = 10
        Assert.Equal(10.0, results[0], 6);

        // Bar 1: RWMA = (10*10 + 20*6) / (10+6) = (100+120)/16 = 13.75
        double expectedBar1 = ((10.0 * 10.0) + (20.0 * 6.0)) / 16.0;
        Assert.Equal(expectedBar1, results[1], 6);

        // Bar 2: RWMA = (10*10 + 20*6 + 30*10) / (10+6+10) = (100+120+300)/26 = 20.0
        double expectedBar2 = ((10.0 * 10.0) + (20.0 * 6.0) + (30.0 * 10.0)) / 26.0;
        Assert.Equal(expectedBar2, results[2], 6);
    }

    [Fact]
    public void Rwma_Algorithm_Correctness_SlidingWindow()
    {
        // Verify sliding window drops old values correctly
        var rwma = new Rwma(2);  // Period = 2

        // Bar 0: close=10, range=10 (h=15, l=5)
        rwma.Update(new TBar(DateTime.UtcNow, 10, 15, 5, 10, 100));
        Assert.Equal(10.0, rwma.Last.Value, 6);

        // Bar 1: close=20, range=6 (h=23, l=17)
        // RWMA = (10*10 + 20*6) / (10+6) = 220/16 = 13.75
        rwma.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 23, 17, 20, 100));
        Assert.Equal(13.75, rwma.Last.Value, 6);

        // Bar 2: close=30, range=10 (h=35, l=25)
        // Now bar0 drops out: RWMA = (20*6 + 30*10) / (6+10) = (120+300)/16 = 26.25
        rwma.Update(new TBar(DateTime.UtcNow.AddMinutes(2), 30, 35, 25, 30, 100));
        Assert.Equal(26.25, rwma.Last.Value, 6);
    }

    [Fact]
    public void Rwma_Algorithm_Correctness_RangeWeighting()
    {
        // Verify range weighting: high-range bars have more influence
        var rwma = new Rwma(10);

        // Two bars: one with high range at low price, one with low range at high price
        rwma.Update(new TBar(DateTime.UtcNow, 10, 60, 10, 10, 100));  // range=50
        var result = rwma.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 100, 101, 99, 100, 100));  // range=2

        // RWMA = (10*50 + 100*2) / (50+2) = (500+200)/52 = 13.46...
        double expected = ((10.0 * 50.0) + (100.0 * 2.0)) / 52.0;
        Assert.Equal(expected, result.Value, 6);

        // RWMA should be much closer to 10 than to 100
        Assert.True(result.Value < 20, "RWMA should be weighted toward high-range price");
    }

    [Fact]
    public void Rwma_DifferentPeriods_ProduceDifferentResults()
    {
        var rwma10 = new Rwma(10);
        var rwma20 = new Rwma(20);
        var rwma50 = new Rwma(50);

        var results10 = new List<double>();
        var results20 = new List<double>();
        var results50 = new List<double>();

        foreach (var bar in _data.Bars)
        {
            results10.Add(rwma10.Update(bar).Value);
            results20.Add(rwma20.Update(bar).Value);
            results50.Add(rwma50.Update(bar).Value);
        }

        // After sufficient bars, different periods should produce different results
        int checkIndex = 60;
        bool anyDifferent = Math.Abs(results10[checkIndex] - results20[checkIndex]) > 1e-6 ||
                           Math.Abs(results20[checkIndex] - results50[checkIndex]) > 1e-6;

        Assert.True(anyDifferent, "Different periods should produce different RWMA values");
    }

    [Fact]
    public void Rwma_StableWithConstantPrice()
    {
        // RWMA should remain stable when close price is constant (regardless of range)
        var rwma = new Rwma(10);
        var results = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            // Close always 50, but varying ranges
            double range = 5 + (i % 10);
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50 + range, 50 - range, 50, 1000);
            results.Add(rwma.Update(bar).Value);
        }

        // All RWMA values should be 50 (constant close, varying range)
        for (int i = 0; i < results.Count; i++)
        {
            Assert.Equal(50.0, results[i], 10);
        }
    }

    [Fact]
    public void Rwma_ZeroRange_DegeneratesToCurrentClose()
    {
        // When all ranges are zero, RWMA should return current close
        var rwma = new Rwma(10);

        for (int i = 0; i < 20; i++)
        {
            double close = 100 + i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), close, close, close, close, 100);
            var result = rwma.Update(bar);

            Assert.Equal(close, result.Value, 10);
        }
    }

    [Fact]
    public void Rwma_EqualRanges_ReducesToSma()
    {
        // When all ranges are equal, RWMA = SMA of closes
        var rwma = new Rwma(3);

        // Three bars with equal range (10) but different closes
        rwma.Update(new TBar(DateTime.UtcNow, 10, 15, 5, 10, 100));              // range=10
        rwma.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 25, 15, 20, 100)); // range=10
        rwma.Update(new TBar(DateTime.UtcNow.AddMinutes(2), 30, 35, 25, 30, 100)); // range=10

        // RWMA = (10*10 + 20*10 + 30*10) / (10+10+10) = 600/30 = 20 = SMA(10,20,30)
        Assert.Equal(20.0, rwma.Last.Value, 10);
    }

    [Fact]
    public void Rwma_ResponsiveToPriceChanges()
    {
        // Shorter period RWMA should track price more closely
        var rwmaShort = new Rwma(5);
        var rwmaLong = new Rwma(50);

        for (int i = 0; i < 100; i++)
        {
            double close = i;
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), close, close + 5, close - 5, close, 1000);
            rwmaShort.Update(bar);
            rwmaLong.Update(bar);
        }

        // Short period RWMA should be closer to current price (99)
        double shortDiff = Math.Abs(rwmaShort.Last.Value - 99);
        double longDiff = Math.Abs(rwmaLong.Last.Value - 99);

        Assert.True(shortDiff < longDiff, "Short period RWMA should track price more closely");
    }

    [Fact]
    public void Rwma_ConvexCombination_OutputWithinPriceRange()
    {
        // RWMA is a convex combination, so output must be within [min, max] of closes in window
        var rwma = new Rwma(10);
        var closes = new List<double>();
        var results = new List<double>();

        foreach (var bar in _data.Bars)
        {
            closes.Add(bar.Close);
            results.Add(rwma.Update(bar).Value);
        }

        // Check after warmup
        for (int i = 10; i < 200; i++)
        {
            double minClose = double.MaxValue;
            double maxClose = double.MinValue;
            for (int j = i - 9; j <= i; j++)
            {
                if (closes[j] < minClose)
                {
                    minClose = closes[j];
                }
                if (closes[j] > maxClose)
                {
                    maxClose = closes[j];
                }
            }

            Assert.True(results[i] >= minClose - 1e-9 && results[i] <= maxClose + 1e-9,
                $"RWMA at {i} ({results[i]}) should be within [{minClose}, {maxClose}]");
        }
    }
}
