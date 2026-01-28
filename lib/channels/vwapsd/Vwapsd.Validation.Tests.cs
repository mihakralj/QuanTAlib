using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for VWAPSD (Volume Weighted Average Price with Standard Deviation Bands).
/// VWAP is a standard institutional calculation. Validation focuses on:
/// 1. Internal consistency between streaming, batch, and span modes
/// 2. Mathematical correctness of VWAP formula
/// 3. Standard deviation bands calculation accuracy with configurable numDevs
/// 4. Volume weighting behavior
/// </summary>
public sealed class VwapsdValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public VwapsdValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    [Fact]
    public void Validate_Streaming_Batch_Consistency()
    {
        double[] numDevsValues = { 0.5, 1.0, 2.0, 3.0 };

        foreach (var numDevs in numDevsValues)
        {
            // Generate test data
            var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
            var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

            // Streaming mode
            var streamingVwapsd = new Vwapsd(numDevs);
            var streamingVwap = new List<double>();
            var streamingUpper = new List<double>();
            var streamingLower = new List<double>();

            for (int i = 0; i < bars.Count; i++)
            {
                streamingVwapsd.Update(bars[i]);
                streamingVwap.Add(streamingVwapsd.Vwap.Value);
                streamingUpper.Add(streamingVwapsd.Upper.Value);
                streamingLower.Add(streamingVwapsd.Lower.Value);
            }

            // Batch mode
            var batchVwapsd = new Vwapsd(numDevs);
            var batchResult = batchVwapsd.Update(bars);

            // Compare last 100 values
            int compareCount = Math.Min(100, bars.Count - 2);
            for (int i = bars.Count - compareCount; i < bars.Count; i++)
            {
                Assert.Equal(streamingVwap[i], batchResult[i].Value, precision: 10);
            }
        }
        _output.WriteLine("VWAPSD Streaming vs Batch consistency validated successfully");
    }

    [Fact]
    public void Validate_Streaming_Span_Consistency()
    {
        double[] numDevsValues = { 0.5, 1.0, 2.0, 3.0 };

        foreach (var numDevs in numDevsValues)
        {
            // Generate test data
            var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
            var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

            // Streaming mode
            var streamingVwapsd = new Vwapsd(numDevs);
            var streamingVwap = new List<double>();
            var streamingUpper = new List<double>();
            var streamingLower = new List<double>();

            for (int i = 0; i < bars.Count; i++)
            {
                streamingVwapsd.Update(bars[i]);
                streamingVwap.Add(streamingVwapsd.Vwap.Value);
                streamingUpper.Add(streamingVwapsd.Upper.Value);
                streamingLower.Add(streamingVwapsd.Lower.Value);
            }

            // Span mode - using bar.HLC3 for price to match streaming mode
            double[] price = new double[bars.Count];
            double[] volume = new double[bars.Count];
            for (int i = 0; i < bars.Count; i++)
            {
                price[i] = bars[i].HLC3;
                volume[i] = bars[i].Volume;
            }

            double[] spanVwap = new double[bars.Count];
            double[] spanUpper = new double[bars.Count];
            double[] spanLower = new double[bars.Count];
            double[] spanStdDev = new double[bars.Count];

            Vwapsd.Calculate(price.AsSpan(), volume.AsSpan(),
                spanUpper.AsSpan(), spanLower.AsSpan(),
                spanVwap.AsSpan(), spanStdDev.AsSpan(), numDevs);

            // Compare last 100 values
            int compareCount = Math.Min(100, bars.Count - 2);
            for (int i = bars.Count - compareCount; i < bars.Count; i++)
            {
                Assert.Equal(streamingVwap[i], spanVwap[i], precision: 10);
                Assert.Equal(streamingUpper[i], spanUpper[i], precision: 10);
                Assert.Equal(streamingLower[i], spanLower[i], precision: 10);
            }
        }
        _output.WriteLine("VWAPSD Streaming vs Span consistency validated successfully");
    }

    [Fact]
    public void Validate_VwapFormula_ManualCalculation()
    {
        // Manually verify VWAP calculation: sum(price × volume) / sum(volume)
        var vwapsd = new Vwapsd(1.0);

        // Create known test data
        var testData = new (double price, double volume)[]
        {
            (100.0, 1000),
            (102.0, 1500),
            (98.0, 800),
            (105.0, 2000),
            (103.0, 1200)
        };

        double sumPV = 0;
        double sumVol = 0;

        for (int i = 0; i < testData.Length; i++)
        {
            var (price, vol) = testData[i];
            sumPV += price * vol;
            sumVol += vol;
            double expectedVwap = sumPV / sumVol;

            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price, price, price, vol);
            vwapsd.Update(bar);

            Assert.Equal(expectedVwap, vwapsd.Vwap.Value, precision: 10);
            _output.WriteLine($"Bar {i + 1}: Price={price}, Vol={vol}, Expected VWAP={expectedVwap:F4}, Actual={vwapsd.Vwap.Value:F4}");
        }

        _output.WriteLine("VWAPSD formula validation completed successfully");
    }

    [Fact]
    public void Validate_StdDevFormula_ManualCalculation()
    {
        // Manually verify variance calculation: (sum(price² × vol) / sum(vol)) - VWAP²
        var vwapsd = new Vwapsd(1.0);

        // Create test data with known variance
        var testData = new (double price, double volume)[]
        {
            (100.0, 1.0),
            (200.0, 1.0)  // Equal weights, max variance
        };

        for (int i = 0; i < testData.Length; i++)
        {
            var (price, vol) = testData[i];
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), price, price, price, price, vol);
            vwapsd.Update(bar);
        }

        // After 2 bars: VWAP = (100 + 200) / 2 = 150
        // MeanP2 = (100² + 200²) / 2 = (10000 + 40000) / 2 = 25000
        // Variance = 25000 - 150² = 25000 - 22500 = 2500
        // StdDev = sqrt(2500) = 50

        Assert.Equal(150.0, vwapsd.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapsd.StdDev.Value, precision: 10);

        _output.WriteLine("VWAPSD StdDev formula validation completed successfully");
    }

    [Fact]
    public void Validate_NumDevsEffect_BandWidth()
    {
        // Verify that numDevs properly scales the band width
        var vwapsd1 = new Vwapsd(1.0);
        var vwapsd2 = new Vwapsd(2.0);
        var vwapsd3 = new Vwapsd(3.0);

        // Create test data with known variance
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1);

        vwapsd1.Update(bar1);
        vwapsd1.Update(bar2);
        vwapsd2.Update(bar1);
        vwapsd2.Update(bar2);
        vwapsd3.Update(bar1);
        vwapsd3.Update(bar2);

        // VWAP = 150, StdDev = 50 for all
        Assert.Equal(150.0, vwapsd1.Vwap.Value, precision: 10);
        Assert.Equal(150.0, vwapsd2.Vwap.Value, precision: 10);
        Assert.Equal(150.0, vwapsd3.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapsd1.StdDev.Value, precision: 10);
        Assert.Equal(50.0, vwapsd2.StdDev.Value, precision: 10);
        Assert.Equal(50.0, vwapsd3.StdDev.Value, precision: 10);

        // With numDevs=1: Upper = 200, Lower = 100, Width = 100
        // With numDevs=2: Upper = 250, Lower = 50, Width = 200
        // With numDevs=3: Upper = 300, Lower = 0, Width = 300
        Assert.Equal(200.0, vwapsd1.Upper.Value, precision: 10);
        Assert.Equal(100.0, vwapsd1.Lower.Value, precision: 10);
        Assert.Equal(100.0, vwapsd1.Width.Value, precision: 10);

        Assert.Equal(250.0, vwapsd2.Upper.Value, precision: 10);
        Assert.Equal(50.0, vwapsd2.Lower.Value, precision: 10);
        Assert.Equal(200.0, vwapsd2.Width.Value, precision: 10);

        Assert.Equal(300.0, vwapsd3.Upper.Value, precision: 10);
        Assert.Equal(0.0, vwapsd3.Lower.Value, precision: 10);
        Assert.Equal(300.0, vwapsd3.Width.Value, precision: 10);

        _output.WriteLine("VWAPSD numDevs effect validation completed successfully");
    }

    [Fact]
    public void Validate_BandCharacteristics()
    {
        // Verify core VWAPSD characteristics:
        // 1. Upper >= VWAP >= Lower
        // 2. Bands are symmetric around VWAP
        // 3. Band width is proportional to numDevs × StdDev

        double numDevs = 1.5;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapsd = new Vwapsd(numDevs);

        for (int i = 0; i < bars.Count; i++)
        {
            vwapsd.Update(bars[i]);

            // Skip first bar where StdDev is 0
            if (i > 0)
            {
                // Upper >= VWAP >= Lower
                Assert.True(vwapsd.Upper.Value >= vwapsd.Vwap.Value,
                    $"Upper ({vwapsd.Upper.Value}) should be >= VWAP ({vwapsd.Vwap.Value})");
                Assert.True(vwapsd.Vwap.Value >= vwapsd.Lower.Value,
                    $"VWAP ({vwapsd.Vwap.Value}) should be >= Lower ({vwapsd.Lower.Value})");

                // Symmetry: Upper - VWAP == VWAP - Lower
                double upperOffset = vwapsd.Upper.Value - vwapsd.Vwap.Value;
                double lowerOffset = vwapsd.Vwap.Value - vwapsd.Lower.Value;
                Assert.Equal(upperOffset, lowerOffset, precision: 9);

                // Width = 2 × numDevs × StdDev
                double expectedWidth = 2.0 * numDevs * vwapsd.StdDev.Value;
                Assert.Equal(expectedWidth, vwapsd.Width.Value, precision: 9);
            }
        }

        _output.WriteLine("VWAPSD band characteristics validated successfully");
    }

    [Fact]
    public void Validate_VolumeWeighting()
    {
        // Verify that VWAP is properly volume-weighted
        var vwapsd = new Vwapsd(1.0);

        // High volume at low price, low volume at high price
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 10000);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 100);

        vwapsd.Update(bar1);
        vwapsd.Update(bar2);

        // VWAP should be closer to 100 (high volume price)
        // VWAP = (100 × 10000 + 200 × 100) / (10000 + 100) = 1020000 / 10100 ≈ 100.99
        double expectedVwap = (100.0 * 10000 + 200.0 * 100) / (10000 + 100);
        Assert.Equal(expectedVwap, vwapsd.Vwap.Value, precision: 10);
        Assert.True(vwapsd.Vwap.Value < 110, "VWAP should be heavily weighted toward 100");

        _output.WriteLine($"Volume weighting verified: VWAP = {vwapsd.Vwap.Value:F4} (expected ≈ 100.99)");
    }

    [Fact]
    public void Validate_NaN_Handling()
    {
        double numDevs = 1.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapsd = new Vwapsd(numDevs);
        int nanCount = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            if (i == 50 || i == 51)
            {
                // Inject NaN price
                vwapsd.Update(new TValue(bars[i].Time, double.NaN), bars[i].Volume, isNew: true);
                nanCount++;
            }
            else
            {
                vwapsd.Update(bars[i]);
            }

            Assert.True(double.IsFinite(vwapsd.Vwap.Value),
                $"VWAP should be finite after NaN at index {i}");
            Assert.True(double.IsFinite(vwapsd.Upper.Value),
                $"Upper should be finite after NaN at index {i}");
            Assert.True(double.IsFinite(vwapsd.Lower.Value),
                $"Lower should be finite after NaN at index {i}");
        }

        _output.WriteLine($"VWAPSD NaN handling validated ({nanCount} NaN values handled)");
    }

    [Fact]
    public void Validate_BarCorrection()
    {
        double numDevs = 1.5;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapsd = new Vwapsd(numDevs);

        // Process all bars
        for (int i = 0; i < bars.Count - 1; i++)
        {
            vwapsd.Update(bars[i]);
        }

        // Record state before last bar
        vwapsd.Update(bars[^1]);
        double originalVwap = vwapsd.Vwap.Value;
        double originalUpper = vwapsd.Upper.Value;

        // Correct last bar with different value
        var correctedBar = new TBar(bars[^1].Time, 200, 210, 190, 200, 5000);
        vwapsd.Update(correctedBar, isNew: false);
        double correctedVwap = vwapsd.Vwap.Value;

        // Should be different
        Assert.NotEqual(originalVwap, correctedVwap);

        // Restore original bar
        vwapsd.Update(bars[^1], isNew: false);
        double restoredVwap = vwapsd.Vwap.Value;
        double restoredUpper = vwapsd.Upper.Value;

        // Should match original
        Assert.Equal(originalVwap, restoredVwap, precision: 10);
        Assert.Equal(originalUpper, restoredUpper, precision: 10);

        _output.WriteLine("VWAPSD bar correction validated successfully");
    }

    [Fact]
    public void Validate_SessionReset()
    {
        // Verify that session reset properly clears VWAP accumulation
        var vwapsd = new Vwapsd(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process first session
        for (int i = 0; i < 25; i++)
        {
            vwapsd.Update(bars[i]);
        }
        double session1Vwap = vwapsd.Vwap.Value;

        // Reset for new session
        var resetBar = new TBar(DateTime.UtcNow, 200, 200, 200, 200, 1000);
        vwapsd.Update(resetBar, isNew: true, reset: true);

        // After reset, VWAP should be just the reset bar's price
        Assert.Equal(200.0, vwapsd.Vwap.Value, precision: 10);
        Assert.NotEqual(session1Vwap, vwapsd.Vwap.Value);

        _output.WriteLine("VWAPSD session reset validated successfully");
    }

    [Fact]
    public void Validate_DifferentNumDevs()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] numDevsValues = { 0.5, 1.0, 1.5, 2.0, 3.0 };
        var avgWidths = new List<double>();

        foreach (var numDevs in numDevsValues)
        {
            var vwapsd = new Vwapsd(numDevs);
            double sumWidth = 0;
            int count = 0;

            for (int i = 0; i < bars.Count; i++)
            {
                vwapsd.Update(bars[i]);
                if (vwapsd.IsHot)
                {
                    sumWidth += vwapsd.Width.Value;
                    count++;
                }
            }

            double avgWidth = count > 0 ? sumWidth / count : 0;
            avgWidths.Add(avgWidth);
            _output.WriteLine($"NumDevs {numDevs}: Average width = {avgWidth:F4}");
        }

        // Higher numDevs should give wider bands
        for (int i = 1; i < avgWidths.Count; i++)
        {
            Assert.True(avgWidths[i] > avgWidths[i - 1],
                $"Higher numDevs should produce wider bands");
        }
    }

    [Fact]
    public void Validate_ZeroVolumeBars()
    {
        // Zero volume bars should not affect VWAP
        var vwapsd = new Vwapsd(1.0);

        // First bar with volume
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vwapsd.Update(bar1);
        double vwapAfterBar1 = vwapsd.Vwap.Value;

        // Multiple zero-volume bars with different prices
        for (int i = 0; i < 5; i++)
        {
            var zeroVolBar = new TBar(DateTime.UtcNow.AddMinutes(i + 1), 200 + i * 10, 200 + i * 10, 200 + i * 10, 200 + i * 10, 0);
            vwapsd.Update(zeroVolBar);
        }

        // VWAP should remain unchanged
        Assert.Equal(vwapAfterBar1, vwapsd.Vwap.Value, precision: 10);

        _output.WriteLine("VWAPSD zero volume handling validated successfully");
    }

    [Fact]
    public void Validate_ConstantPrice_ZeroStdDev()
    {
        // With constant price, StdDev should be 0 and all bands should equal VWAP
        var vwapsd = new Vwapsd(2.0);

        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            vwapsd.Update(bar);
        }

        Assert.Equal(100.0, vwapsd.Vwap.Value, precision: 6);
        Assert.Equal(0.0, vwapsd.StdDev.Value, precision: 6);
        Assert.Equal(100.0, vwapsd.Upper.Value, precision: 6);
        Assert.Equal(100.0, vwapsd.Lower.Value, precision: 6);

        _output.WriteLine("VWAPSD constant price validation completed");
    }

    [Fact]
    public void Validate_LargeDataset_Performance()
    {
        // Process large dataset to verify stability
        var vwapsd = new Vwapsd(1.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < bars.Count; i++)
        {
            vwapsd.Update(bars[i]);

            // Verify all values remain finite
            Assert.True(double.IsFinite(vwapsd.Vwap.Value), $"VWAP not finite at index {i}");
            Assert.True(double.IsFinite(vwapsd.StdDev.Value), $"StdDev not finite at index {i}");
            Assert.True(double.IsFinite(vwapsd.Upper.Value), $"Upper not finite at index {i}");
            Assert.True(double.IsFinite(vwapsd.Lower.Value), $"Lower not finite at index {i}");
        }

        sw.Stop();
        _output.WriteLine($"Processed {bars.Count} bars in {sw.ElapsedMilliseconds}ms ({bars.Count * 1000.0 / sw.ElapsedMilliseconds:F0} bars/sec)");
    }

    [Fact]
    public void Validate_StaticCalculate_TBarSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (upper, lower, vwap, stdev) = Vwapsd.Calculate(bars, 1.5);

        Assert.Equal(bars.Count, upper.Count);
        Assert.Equal(bars.Count, lower.Count);
        Assert.Equal(bars.Count, vwap.Count);
        Assert.Equal(bars.Count, stdev.Count);

        // Verify streaming matches static
        var streamingVwapsd = new Vwapsd(1.5);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingVwapsd.Update(bars[i]);
        }

        Assert.Equal(streamingVwapsd.Vwap.Value, vwap.Last.Value, precision: 10);
        Assert.Equal(streamingVwapsd.Upper.Value, upper.Last.Value, precision: 10);
        Assert.Equal(streamingVwapsd.Lower.Value, lower.Last.Value, precision: 10);

        _output.WriteLine("VWAPSD static Calculate validated successfully");
    }

    [Fact]
    public void Validate_FractionalNumDevs()
    {
        // Test fractional numDevs values within valid range
        double[] fractionalValues = { 0.1, 0.25, 0.5, 0.75, 1.25, 1.5, 2.5, 4.5 };

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var numDevs in fractionalValues)
        {
            var vwapsd = new Vwapsd(numDevs);

            for (int i = 0; i < bars.Count; i++)
            {
                vwapsd.Update(bars[i]);
            }

            Assert.True(double.IsFinite(vwapsd.Vwap.Value));
            Assert.True(double.IsFinite(vwapsd.Upper.Value));
            Assert.True(double.IsFinite(vwapsd.Lower.Value));
            Assert.True(vwapsd.Width.Value >= 0);

            _output.WriteLine($"NumDevs {numDevs:F2}: VWAP={vwapsd.Vwap.Value:F4}, Width={vwapsd.Width.Value:F4}");
        }

        _output.WriteLine("VWAPSD fractional numDevs validation completed");
    }

    [Fact]
    public void Validate_BoundaryNumDevs()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Test minimum boundary (0.1)
        var vwapsdMin = new Vwapsd(0.1);
        for (int i = 0; i < bars.Count; i++)
        {
            vwapsdMin.Update(bars[i]);
        }
        Assert.True(vwapsdMin.Width.Value > 0 || vwapsdMin.StdDev.Value == 0);
        _output.WriteLine($"Min numDevs (0.1): Width={vwapsdMin.Width.Value:F6}");

        // Test maximum boundary (5.0)
        var vwapsdMax = new Vwapsd(5.0);
        for (int i = 0; i < bars.Count; i++)
        {
            vwapsdMax.Update(bars[i]);
        }
        Assert.True(vwapsdMax.Width.Value >= vwapsdMin.Width.Value);
        _output.WriteLine($"Max numDevs (5.0): Width={vwapsdMax.Width.Value:F6}");

        // Verify width ratio matches numDevs ratio
        if (vwapsdMin.StdDev.Value > 0)
        {
            double expectedRatio = 5.0 / 0.1; // 50x
            double actualRatio = vwapsdMax.Width.Value / vwapsdMin.Width.Value;
            Assert.Equal(expectedRatio, actualRatio, precision: 8);
        }

        _output.WriteLine("VWAPSD boundary numDevs validation completed");
    }
}
