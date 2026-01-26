using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for VWAPBANDS (Volume Weighted Average Price with Standard Deviation Bands).
/// VWAP is a standard institutional calculation. Validation focuses on:
/// 1. Internal consistency between streaming, batch, and span modes
/// 2. Mathematical correctness of VWAP formula
/// 3. Standard deviation bands calculation accuracy
/// 4. Volume weighting behavior
/// </summary>
public sealed class VwapbandsValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public VwapbandsValidationTests(ITestOutputHelper output)
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
        double[] multipliers = { 0.5, 1.0, 2.0 };

        foreach (var multiplier in multipliers)
        {
            // Generate test data
            var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
            var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

            // Streaming mode
            var streamingVwapbands = new Vwapbands(multiplier);
            var streamingVwap = new List<double>();
            var streamingUpper1 = new List<double>();
            var streamingLower1 = new List<double>();

            for (int i = 0; i < bars.Count; i++)
            {
                streamingVwapbands.Update(bars[i]);
                streamingVwap.Add(streamingVwapbands.Vwap.Value);
                streamingUpper1.Add(streamingVwapbands.Upper1.Value);
                streamingLower1.Add(streamingVwapbands.Lower1.Value);
            }

            // Batch mode
            var batchVwapbands = new Vwapbands(multiplier);
            var batchResult = batchVwapbands.Update(bars);

            // Compare last 100 values
            int compareCount = Math.Min(100, bars.Count - 2);
            for (int i = bars.Count - compareCount; i < bars.Count; i++)
            {
                Assert.Equal(streamingVwap[i], batchResult[i].Value, precision: 10);
            }
        }
        _output.WriteLine("VWAPBANDS Streaming vs Batch consistency validated successfully");
    }

    [Fact]
    public void Validate_Streaming_Span_Consistency()
    {
        double[] multipliers = { 0.5, 1.0, 2.0 };

        foreach (var multiplier in multipliers)
        {
            // Generate test data
            var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
            var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

            // Streaming mode
            var streamingVwapbands = new Vwapbands(multiplier);
            var streamingVwap = new List<double>();
            var streamingUpper1 = new List<double>();
            var streamingLower1 = new List<double>();
            var streamingUpper2 = new List<double>();
            var streamingLower2 = new List<double>();

            for (int i = 0; i < bars.Count; i++)
            {
                streamingVwapbands.Update(bars[i]);
                streamingVwap.Add(streamingVwapbands.Vwap.Value);
                streamingUpper1.Add(streamingVwapbands.Upper1.Value);
                streamingLower1.Add(streamingVwapbands.Lower1.Value);
                streamingUpper2.Add(streamingVwapbands.Upper2.Value);
                streamingLower2.Add(streamingVwapbands.Lower2.Value);
            }

            // Span mode - using HLC3 for price (use bar.HLC3 property for consistency)
            double[] price = new double[bars.Count];
            double[] volume = new double[bars.Count];
            for (int i = 0; i < bars.Count; i++)
            {
                price[i] = bars[i].HLC3;
                volume[i] = bars[i].Volume;
            }

            double[] spanVwap = new double[bars.Count];
            double[] spanUpper1 = new double[bars.Count];
            double[] spanLower1 = new double[bars.Count];
            double[] spanUpper2 = new double[bars.Count];
            double[] spanLower2 = new double[bars.Count];

            Vwapbands.Calculate(price.AsSpan(), volume.AsSpan(),
                spanUpper1.AsSpan(), spanLower1.AsSpan(),
                spanUpper2.AsSpan(), spanLower2.AsSpan(),
                spanVwap.AsSpan(), multiplier);

            // Compare last 100 values
            int compareCount = Math.Min(100, bars.Count - 2);
            for (int i = bars.Count - compareCount; i < bars.Count; i++)
            {
                Assert.Equal(streamingVwap[i], spanVwap[i], precision: 10);
                Assert.Equal(streamingUpper1[i], spanUpper1[i], precision: 10);
                Assert.Equal(streamingLower1[i], spanLower1[i], precision: 10);
                Assert.Equal(streamingUpper2[i], spanUpper2[i], precision: 10);
                Assert.Equal(streamingLower2[i], spanLower2[i], precision: 10);
            }
        }
        _output.WriteLine("VWAPBANDS Streaming vs Span consistency validated successfully");
    }

    [Fact]
    public void Validate_VwapFormula_ManualCalculation()
    {
        // Manually verify VWAP calculation: sum(price × volume) / sum(volume)
        var vwapbands = new Vwapbands(1.0);

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
            vwapbands.Update(bar);

            Assert.Equal(expectedVwap, vwapbands.Vwap.Value, precision: 10);
            _output.WriteLine($"Bar {i + 1}: Price={price}, Vol={vol}, Expected VWAP={expectedVwap:F4}, Actual={vwapbands.Vwap.Value:F4}");
        }

        _output.WriteLine("VWAPBANDS formula validation completed successfully");
    }

    [Fact]
    public void Validate_StdDevFormula_ManualCalculation()
    {
        // Manually verify variance calculation: (sum(price² × vol) / sum(vol)) - VWAP²
        var vwapbands = new Vwapbands(1.0);

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
            vwapbands.Update(bar);
        }

        // After 2 bars: VWAP = (100 + 200) / 2 = 150
        // MeanP2 = (100² + 200²) / 2 = (10000 + 40000) / 2 = 25000
        // Variance = 25000 - 150² = 25000 - 22500 = 2500
        // StdDev = sqrt(2500) = 50

        Assert.Equal(150.0, vwapbands.Vwap.Value, precision: 10);
        Assert.Equal(50.0, vwapbands.StdDev.Value, precision: 10);

        _output.WriteLine("VWAPBANDS StdDev formula validation completed successfully");
    }

    [Fact]
    public void Validate_BandCharacteristics()
    {
        // Verify core VWAPBANDS characteristics:
        // 1. Upper2 >= Upper1 >= VWAP >= Lower1 >= Lower2
        // 2. Bands are symmetric around VWAP
        // 3. Band width is proportional to StdDev

        double multiplier = 1.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapbands = new Vwapbands(multiplier);

        for (int i = 0; i < bars.Count; i++)
        {
            vwapbands.Update(bars[i]);

            // Skip first bar where StdDev is 0
            if (i > 0)
            {
                // Upper2 >= Upper1 >= VWAP >= Lower1 >= Lower2
                Assert.True(vwapbands.Upper2.Value >= vwapbands.Upper1.Value,
                    $"Upper2 ({vwapbands.Upper2.Value}) should be >= Upper1 ({vwapbands.Upper1.Value})");
                Assert.True(vwapbands.Upper1.Value >= vwapbands.Vwap.Value,
                    $"Upper1 ({vwapbands.Upper1.Value}) should be >= VWAP ({vwapbands.Vwap.Value})");
                Assert.True(vwapbands.Vwap.Value >= vwapbands.Lower1.Value,
                    $"VWAP ({vwapbands.Vwap.Value}) should be >= Lower1 ({vwapbands.Lower1.Value})");
                Assert.True(vwapbands.Lower1.Value >= vwapbands.Lower2.Value,
                    $"Lower1 ({vwapbands.Lower1.Value}) should be >= Lower2 ({vwapbands.Lower2.Value})");

                // Symmetry: Upper1 - VWAP == VWAP - Lower1
                double upperOffset = vwapbands.Upper1.Value - vwapbands.Vwap.Value;
                double lowerOffset = vwapbands.Vwap.Value - vwapbands.Lower1.Value;
                Assert.Equal(upperOffset, lowerOffset, precision: 10);

                // Width = 2 × multiplier × StdDev
                double expectedWidth = 2.0 * multiplier * vwapbands.StdDev.Value;
                Assert.Equal(expectedWidth, vwapbands.Width.Value, precision: 10);
            }
        }

        _output.WriteLine("VWAPBANDS band characteristics validated successfully");
    }

    [Fact]
    public void Validate_VolumeWeighting()
    {
        // Verify that VWAP is properly volume-weighted
        var vwapbands = new Vwapbands(1.0);

        // High volume at low price, low volume at high price
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 10000);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 100);

        vwapbands.Update(bar1);
        vwapbands.Update(bar2);

        // VWAP should be closer to 100 (high volume price)
        // VWAP = (100 × 10000 + 200 × 100) / (10000 + 100) = 1020000 / 10100 ≈ 100.99
        double expectedVwap = (100.0 * 10000 + 200.0 * 100) / (10000 + 100);
        Assert.Equal(expectedVwap, vwapbands.Vwap.Value, precision: 10);
        Assert.True(vwapbands.Vwap.Value < 110, "VWAP should be heavily weighted toward 100");

        _output.WriteLine($"Volume weighting verified: VWAP = {vwapbands.Vwap.Value:F4} (expected ≈ 100.99)");
    }

    [Fact]
    public void Validate_NaN_Handling()
    {
        double multiplier = 1.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapbands = new Vwapbands(multiplier);
        int nanCount = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            if (i == 50 || i == 51)
            {
                // Inject NaN price
                vwapbands.Update(new TValue(bars[i].Time, double.NaN), bars[i].Volume, isNew: true);
                nanCount++;
            }
            else
            {
                vwapbands.Update(bars[i]);
            }

            Assert.True(double.IsFinite(vwapbands.Vwap.Value),
                $"VWAP should be finite after NaN at index {i}");
            Assert.True(double.IsFinite(vwapbands.Upper1.Value),
                $"Upper1 should be finite after NaN at index {i}");
            Assert.True(double.IsFinite(vwapbands.Lower1.Value),
                $"Lower1 should be finite after NaN at index {i}");
        }

        _output.WriteLine($"VWAPBANDS NaN handling validated ({nanCount} NaN values handled)");
    }

    [Fact]
    public void Validate_BarCorrection()
    {
        double multiplier = 1.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var vwapbands = new Vwapbands(multiplier);

        // Process all bars
        for (int i = 0; i < bars.Count - 1; i++)
        {
            vwapbands.Update(bars[i]);
        }

        // Record state before last bar
        vwapbands.Update(bars[^1]);
        double originalVwap = vwapbands.Vwap.Value;
        double originalUpper1 = vwapbands.Upper1.Value;

        // Correct last bar with different value
        var correctedBar = new TBar(bars[^1].Time, 200, 210, 190, 200, 5000);
        vwapbands.Update(correctedBar, isNew: false);
        double correctedVwap = vwapbands.Vwap.Value;

        // Should be different
        Assert.NotEqual(originalVwap, correctedVwap);

        // Restore original bar
        vwapbands.Update(bars[^1], isNew: false);
        double restoredVwap = vwapbands.Vwap.Value;
        double restoredUpper1 = vwapbands.Upper1.Value;

        // Should match original
        Assert.Equal(originalVwap, restoredVwap, precision: 10);
        Assert.Equal(originalUpper1, restoredUpper1, precision: 10);

        _output.WriteLine("VWAPBANDS bar correction validated successfully");
    }

    [Fact]
    public void Validate_SessionReset()
    {
        // Verify that session reset properly clears VWAP accumulation
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Process first session
        for (int i = 0; i < 25; i++)
        {
            vwapbands.Update(bars[i]);
        }
        double session1Vwap = vwapbands.Vwap.Value;

        // Reset for new session
        var resetBar = new TBar(DateTime.UtcNow, 200, 200, 200, 200, 1000);
        vwapbands.Update(resetBar, isNew: true, reset: true);

        // After reset, VWAP should be just the reset bar's price
        Assert.Equal(200.0, vwapbands.Vwap.Value, precision: 10);
        Assert.NotEqual(session1Vwap, vwapbands.Vwap.Value);

        _output.WriteLine("VWAPBANDS session reset validated successfully");
    }

    [Fact]
    public void Validate_DifferentMultipliers()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] multipliers = { 0.5, 1.0, 1.5, 2.0 };
        var avgWidths = new List<double>();

        foreach (var multiplier in multipliers)
        {
            var vwapbands = new Vwapbands(multiplier);
            double sumWidth = 0;
            int count = 0;

            for (int i = 0; i < bars.Count; i++)
            {
                vwapbands.Update(bars[i]);
                if (vwapbands.IsHot)
                {
                    sumWidth += vwapbands.Width.Value;
                    count++;
                }
            }

            double avgWidth = count > 0 ? sumWidth / count : 0;
            avgWidths.Add(avgWidth);
            _output.WriteLine($"Multiplier {multiplier}: Average width = {avgWidth:F4}");
        }

        // Higher multipliers should give wider bands
        for (int i = 1; i < avgWidths.Count; i++)
        {
            Assert.True(avgWidths[i] > avgWidths[i - 1],
                $"Higher multiplier should produce wider bands");
        }
    }

    [Fact]
    public void Validate_ZeroVolumeBars()
    {
        // Zero volume bars should not affect VWAP
        var vwapbands = new Vwapbands(1.0);

        // First bar with volume
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        vwapbands.Update(bar1);
        double vwapAfterBar1 = vwapbands.Vwap.Value;

        // Multiple zero-volume bars with different prices
        for (int i = 0; i < 5; i++)
        {
            var zeroVolBar = new TBar(DateTime.UtcNow.AddMinutes(i + 1), 200 + i * 10, 200 + i * 10, 200 + i * 10, 200 + i * 10, 0);
            vwapbands.Update(zeroVolBar);
        }

        // VWAP should remain unchanged
        Assert.Equal(vwapAfterBar1, vwapbands.Vwap.Value, precision: 10);

        _output.WriteLine("VWAPBANDS zero volume handling validated successfully");
    }

    [Fact]
    public void Validate_ConstantPrice_ZeroStdDev()
    {
        // With constant price, StdDev should be 0 and all bands should equal VWAP
        var vwapbands = new Vwapbands(1.0);

        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            vwapbands.Update(bar);
        }

        Assert.Equal(100.0, vwapbands.Vwap.Value, precision: 6);
        Assert.Equal(0.0, vwapbands.StdDev.Value, precision: 6);
        Assert.Equal(100.0, vwapbands.Upper1.Value, precision: 6);
        Assert.Equal(100.0, vwapbands.Lower1.Value, precision: 6);
        Assert.Equal(100.0, vwapbands.Upper2.Value, precision: 6);
        Assert.Equal(100.0, vwapbands.Lower2.Value, precision: 6);

        _output.WriteLine("VWAPBANDS constant price validation completed");
    }

    [Fact]
    public void Validate_LargeDataset_Performance()
    {
        // Process large dataset to verify stability
        var vwapbands = new Vwapbands(1.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < bars.Count; i++)
        {
            vwapbands.Update(bars[i]);

            // Verify all values remain finite
            Assert.True(double.IsFinite(vwapbands.Vwap.Value), $"VWAP not finite at index {i}");
            Assert.True(double.IsFinite(vwapbands.StdDev.Value), $"StdDev not finite at index {i}");
            Assert.True(double.IsFinite(vwapbands.Upper1.Value), $"Upper1 not finite at index {i}");
            Assert.True(double.IsFinite(vwapbands.Lower1.Value), $"Lower1 not finite at index {i}");
        }

        sw.Stop();
        _output.WriteLine($"Processed {bars.Count} bars in {sw.ElapsedMilliseconds}ms ({bars.Count * 1000.0 / sw.ElapsedMilliseconds:F0} bars/sec)");
    }

    [Fact]
    public void Validate_StaticCalculate_TBarSeries()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (upper1, lower1, upper2, lower2, vwap, stdev) = Vwapbands.Calculate(bars, 1.0);

        Assert.Equal(bars.Count, upper1.Count);
        Assert.Equal(bars.Count, lower1.Count);
        Assert.Equal(bars.Count, upper2.Count);
        Assert.Equal(bars.Count, lower2.Count);
        Assert.Equal(bars.Count, vwap.Count);
        Assert.Equal(bars.Count, stdev.Count);

        // Verify streaming matches static
        var streamingVwapbands = new Vwapbands(1.0);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingVwapbands.Update(bars[i]);
        }

        Assert.Equal(streamingVwapbands.Vwap.Value, vwap.Last.Value, precision: 10);
        Assert.Equal(streamingVwapbands.Upper1.Value, upper1.Last.Value, precision: 10);
        Assert.Equal(streamingVwapbands.Lower1.Value, lower1.Last.Value, precision: 10);

        _output.WriteLine("VWAPBANDS static Calculate validated successfully");
    }
}
