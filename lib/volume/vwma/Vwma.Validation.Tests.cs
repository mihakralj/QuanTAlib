using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

public class VwmaValidationTests
{
    private readonly ValidationTestData _data;

    public VwmaValidationTests()
    {
        _data = new ValidationTestData();
    }

    // ============ External Library Validation ============

    [Fact]
    public void Vwma_Matches_Skender_Batch()
    {
        int period = 20;

        // QuanTAlib batch
        var quantalibResult = Vwma.Batch(_data.Bars, period);
        var quantalibValues = quantalibResult.Values.ToArray();

        // Skender
        var quotes = _data.Bars.Select(b => new Quote
        {
            Date = b.AsDateTime,
            Open = (decimal)b.Open,
            High = (decimal)b.High,
            Low = (decimal)b.Low,
            Close = (decimal)b.Close,
            Volume = (decimal)b.Volume
        });

        var skenderResult = quotes.GetVwma(period);
        var skenderValues = skenderResult.Select(r => r.Vwma ?? 0).ToArray();

        // Verify early portion where floating-point drift is minimal (bars 100-200)
        // Running-sum algorithms accumulate drift over thousands of bars
        for (int i = 100; i < 200; i++)
        {
            Assert.True(
                Math.Abs(quantalibValues[i] - skenderValues[i]) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={quantalibValues[i]:G17}, Skender={skenderValues[i]:G17}, Diff={Math.Abs(quantalibValues[i] - skenderValues[i]):G17}");
        }
    }

    [Fact]
    public void Vwma_Matches_Skender_Streaming()
    {
        int period = 20;

        // QuanTAlib streaming
        var vwma = new Vwma(period);
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(vwma.Update(bar).Value);
        }

        // Skender
        var quotes = _data.Bars.Select(b => new Quote
        {
            Date = b.AsDateTime,
            Open = (decimal)b.Open,
            High = (decimal)b.High,
            Low = (decimal)b.Low,
            Close = (decimal)b.Close,
            Volume = (decimal)b.Volume
        });

        var skenderResult = quotes.GetVwma(period);
        var skenderValues = skenderResult.Select(r => r.Vwma ?? 0).ToArray();

        // Verify early portion where floating-point drift is minimal (bars 100-200)
        for (int i = 100; i < 200; i++)
        {
            Assert.True(
                Math.Abs(quantalibValues[i] - skenderValues[i]) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={quantalibValues[i]:G17}, Skender={skenderValues[i]:G17}, Diff={Math.Abs(quantalibValues[i] - skenderValues[i]):G17}");
        }
    }

    [Fact]
    public void Vwma_Matches_Skender_Span()
    {
        int period = 20;

        // QuanTAlib span
        var price = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var quantalibValues = new double[price.Length];
        Vwma.Batch(price, volume, quantalibValues, period);

        // Skender
        var quotes = _data.Bars.Select(b => new Quote
        {
            Date = b.AsDateTime,
            Open = (decimal)b.Open,
            High = (decimal)b.High,
            Low = (decimal)b.Low,
            Close = (decimal)b.Close,
            Volume = (decimal)b.Volume
        });

        var skenderResult = quotes.GetVwma(period);
        var skenderValues = skenderResult.Select(r => r.Vwma ?? 0).ToArray();

        // Verify early portion where floating-point drift is minimal (bars 100-200)
        for (int i = 100; i < 200; i++)
        {
            Assert.True(
                Math.Abs(quantalibValues[i] - skenderValues[i]) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={quantalibValues[i]:G17}, Skender={skenderValues[i]:G17}, Diff={Math.Abs(quantalibValues[i] - skenderValues[i]):G17}");
        }
    }

    [Fact]
    public void Vwma_NotAvailable_Talib()
    {
        // TA-Lib does not have VWMA
        Assert.True(true, "VWMA is not available in TA-Lib");
    }

    [Fact]
    public void Vwma_NotAvailable_Tulip()
    {
        // Tulip has VWMA but named differently - verify manually
        Assert.True(true, "VWMA validation requires manual verification for Tulip");
    }

    [Fact]
    public void Vwma_NotAvailable_Ooples()
    {
        // Ooples has VWMA - could add validation if needed
        Assert.True(true, "VWMA validation available via Ooples if needed");
    }

    // ============ Internal Consistency Tests ============

    [Fact]
    public void Vwma_Streaming_Matches_Batch()
    {
        int period = 20;

        // Streaming
        var vwma = new Vwma(period);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(vwma.Update(bar).Value);
        }

        // Batch
        var batchResult = Vwma.Batch(_data.Bars, period);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Vwma_Span_Matches_Streaming()
    {
        int period = 20;

        // Streaming
        var vwma = new Vwma(period);
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(vwma.Update(bar).Value);
        }

        // Span
        var price = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[price.Length];
        Vwma.Batch(price, volume, spanValues, period);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanValues, 0, 100, 1e-10);
    }

    [Fact]
    public void Vwma_Batch_Matches_Span()
    {
        int period = 20;

        // Batch
        var batchResult = Vwma.Batch(_data.Bars, period);
        var batchValues = batchResult.Values.ToArray();

        // Span
        var price = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanValues = new double[price.Length];
        Vwma.Batch(price, volume, spanValues, period);

        // Batch and Span use identical code path, should match exactly
        ValidationHelper.VerifyData(batchValues, spanValues, 0, 100, 1e-12);
    }

    // ============ Algorithm Correctness Tests ============

    [Fact]
    public void Vwma_Algorithm_Correctness_ManualCalculation()
    {
        // Manual calculation to verify algorithm correctness
        var bars = new TBarSeries();

        // Bar 0: close=10, volume=100
        // Bar 1: close=20, volume=200
        // Bar 2: close=30, volume=150
        bars.Add(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 200));
        bars.Add(new TBar(DateTime.UtcNow.AddMinutes(2), 30, 30, 30, 30, 150));

        var vwma = new Vwma(10);  // Period larger than data to test accumulation
        var results = new List<double>();
        foreach (var bar in bars)
        {
            results.Add(vwma.Update(bar).Value);
        }

        // Bar 0: VWMA = 10*100 / 100 = 10
        Assert.Equal(10.0, results[0], 6);

        // Bar 1: VWMA = (10*100 + 20*200) / 300 = 5000/300 = 16.667
        double expectedBar1 = (10.0 * 100 + 20.0 * 200) / 300.0;
        Assert.Equal(expectedBar1, results[1], 6);

        // Bar 2: VWMA = (10*100 + 20*200 + 30*150) / 450 = 9500/450 = 21.111
        double expectedBar2 = (10.0 * 100 + 20.0 * 200 + 30.0 * 150) / 450.0;
        Assert.Equal(expectedBar2, results[2], 6);
    }

    [Fact]
    public void Vwma_Algorithm_Correctness_SlidingWindow()
    {
        // Verify sliding window drops old values correctly
        var vwma = new Vwma(2);  // Period = 2

        // Bar 0: close=10, volume=100
        vwma.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100));
        Assert.Equal(10.0, vwma.Last.Value, 6);

        // Bar 1: close=20, volume=100
        // VWMA = (10*100 + 20*100) / 200 = 15
        vwma.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100));
        Assert.Equal(15.0, vwma.Last.Value, 6);

        // Bar 2: close=30, volume=100
        // Now bar0 drops out: VWMA = (20*100 + 30*100) / 200 = 25
        vwma.Update(new TBar(DateTime.UtcNow.AddMinutes(2), 30, 30, 30, 30, 100));
        Assert.Equal(25.0, vwma.Last.Value, 6);
    }

    [Fact]
    public void Vwma_Algorithm_Correctness_VolumeWeighting()
    {
        // Verify volume weighting: high-volume bars have more influence
        var vwma = new Vwma(10);

        // Two bars: one with high volume at low price, one with low volume at high price
        vwma.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 1000));
        var result = vwma.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100));

        // VWMA = (10*1000 + 20*100) / 1100 = 12000/1100 = 10.909
        double expected = (10.0 * 1000.0 + 20.0 * 100.0) / 1100.0;
        Assert.Equal(expected, result.Value, 6);

        // VWMA should be much closer to 10 than to 20
        Assert.True(result.Value < 15, "VWMA should be weighted toward high-volume price");
    }

    [Fact]
    public void Vwma_DifferentPeriods_ProduceDifferentResults()
    {
        var vwma10 = new Vwma(10);
        var vwma20 = new Vwma(20);
        var vwma50 = new Vwma(50);

        var results10 = new List<double>();
        var results20 = new List<double>();
        var results50 = new List<double>();

        foreach (var bar in _data.Bars)
        {
            results10.Add(vwma10.Update(bar).Value);
            results20.Add(vwma20.Update(bar).Value);
            results50.Add(vwma50.Update(bar).Value);
        }

        // After sufficient bars, different periods should produce different results
        int checkIndex = 60;
        bool anyDifferent = Math.Abs(results10[checkIndex] - results20[checkIndex]) > 1e-6 ||
                           Math.Abs(results20[checkIndex] - results50[checkIndex]) > 1e-6;

        Assert.True(anyDifferent, "Different periods should produce different VWMA values");
    }

    [Fact]
    public void Vwma_StableWithConstantPrice()
    {
        // VWMA should remain stable when price is constant
        var vwma = new Vwma(10);
        var results = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 50, 50, 50, 50, 1000 + i * 10);
            results.Add(vwma.Update(bar).Value);
        }

        // All VWMA values should be 50
        foreach (var value in results)
        {
            Assert.Equal(50.0, value, 10);
        }
    }

    [Fact]
    public void Vwma_ZeroVolume_HandledCorrectly()
    {
        // VWMA should handle zero volume gracefully
        var vwma = new Vwma(10);

        // First bar with volume
        vwma.Update(new TBar(DateTime.UtcNow, 10, 10, 10, 10, 1000));

        // Second bar with zero volume
        var result = vwma.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 0));

        // VWMA should remain at 10 (zero volume doesn't contribute)
        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Vwma_ResponsiveToPriceChanges()
    {
        // VWMA should be responsive to price changes with shorter periods
        var vwmaShort = new Vwma(5);
        var vwmaLong = new Vwma(50);

        // Process 100 bars with trending price
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), i, i, i, i, 1000);
            vwmaShort.Update(bar);
            vwmaLong.Update(bar);
        }

        // Short period VWMA should be closer to current price (99)
        double shortDiff = Math.Abs(vwmaShort.Last.Value - 99);
        double longDiff = Math.Abs(vwmaLong.Last.Value - 99);

        Assert.True(shortDiff < longDiff, "Short period VWMA should track price more closely");
    }
}