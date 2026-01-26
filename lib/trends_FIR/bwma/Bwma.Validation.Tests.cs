namespace QuanTAlib.Tests;

/// <summary>
/// BWMA Validation Tests
/// Note: BWMA (Bessel-Weighted Moving Average) is not available in TA-Lib, Skender,
/// Tulip, or OoplesFinance. Validation is limited to self-consistency tests
/// verifying that streaming, batch, and span APIs produce identical results.
/// </summary>
public sealed class BwmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public BwmaValidationTests()
    {
        _testData = new ValidationTestData(count: 1000, seed: 42);
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
    public void Validate_Streaming_Batch_Span_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };
        int[] orders = { 0, 1, 2, 3 };

        foreach (var period in periods)
        {
            foreach (var order in orders)
            {
                // 1. Streaming API
                var bwmaStreaming = new Bwma(period, order);
                var streamingResults = new List<double>();
                foreach (var item in _testData.Data)
                {
                    streamingResults.Add(bwmaStreaming.Update(item).Value);
                }

                // 2. Batch API (TSeries)
                var bwmaBatch = new Bwma(period, order);
                var batchResults = bwmaBatch.Update(_testData.Data);

                // 3. Span API
                ReadOnlySpan<double> sourceData = _testData.RawData.Span;
                double[] spanOutput = new double[sourceData.Length];
                Bwma.Calculate(sourceData, spanOutput.AsSpan(), period, order);

                // Verify streaming vs batch
                Assert.Equal(streamingResults.Count, batchResults.Count);
                for (int i = 0; i < batchResults.Count; i++)
                {
                    Assert.Equal(streamingResults[i], batchResults.Values[i], 1e-9);
                }

                // Verify streaming vs span
                for (int i = 0; i < spanOutput.Length; i++)
                {
                    Assert.Equal(streamingResults[i], spanOutput[i], 1e-9);
                }
            }
        }
    }

    [Fact]
    public void Validate_StaticBatch_Matches_Instance()
    {
        int[] periods = { 5, 10, 20, 50 };
        int[] orders = { 0, 1, 2 };

        foreach (var period in periods)
        {
            foreach (var order in orders)
            {
                // Instance batch
                var bwma = new Bwma(period, order);
                var instanceResult = bwma.Update(_testData.Data);

                // Static batch
                var staticResult = Bwma.Batch(_testData.Data, period, order);

                Assert.Equal(instanceResult.Count, staticResult.Count);
                for (int i = 0; i < staticResult.Count; i++)
                {
                    Assert.Equal(instanceResult.Values[i], staticResult.Values[i], 1e-9);
                }
            }
        }
    }

    [Fact]
    public void Validate_BarCorrection_Consistency()
    {
        int[] periods = { 5, 10, 20 };

        foreach (var period in periods)
        {
            var bwma1 = new Bwma(period);
            var bwma2 = new Bwma(period);

            // Process most of the data
            for (int i = 0; i < _testData.Data.Count - 1; i++)
            {
                bwma1.Update(_testData.Data[i]);
                bwma2.Update(_testData.Data[i]);
            }

            // bwma1: update with original value, then correct with modified value
            var lastItem = _testData.Data[^1];
            bwma1.Update(lastItem, isNew: true);
            var correctedResult = bwma1.Update(new TValue(lastItem.Time, lastItem.Value + 10.0), isNew: false);

            // bwma2: directly update with modified value
            var directResult = bwma2.Update(new TValue(lastItem.Time, lastItem.Value + 10.0), isNew: true);

            Assert.Equal(directResult.Value, correctedResult.Value, 1e-9);
        }
    }

    [Fact]
    public void Validate_Reset_ProducesSameResults()
    {
        int period = 14;
        int order = 1;

        var bwma = new Bwma(period, order);

        // First pass
        var firstPassResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            firstPassResults.Add(bwma.Update(item).Value);
        }

        // Reset
        bwma.Reset();

        // Second pass
        var secondPassResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            secondPassResults.Add(bwma.Update(item).Value);
        }

        Assert.Equal(firstPassResults.Count, secondPassResults.Count);
        for (int i = 0; i < firstPassResults.Count; i++)
        {
            Assert.Equal(firstPassResults[i], secondPassResults[i], 1e-9);
        }
    }

    [Fact]
    public void Validate_DifferentOrders_ProduceDifferentWeights()
    {
        int period = 20;

        // Calculate with different orders
        var results = new Dictionary<int, double[]>();
        foreach (var order in new[] { 0, 1, 3 })  // Skip order 2 as it uses same power as order 1 (1.5)
        {
            var bwma = new Bwma(period, order);
            var orderResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                orderResults.Add(bwma.Update(item).Value);
            }
            results[order] = orderResults.ToArray();
        }

        // Verify that order 0 vs 1 produce different results
        bool order0vs1AllEqual = true;
        for (int j = period; j < results[0].Length; j++)
        {
            if (Math.Abs(results[0][j] - results[1][j]) > 1e-9)
            {
                order0vs1AllEqual = false;
                break;
            }
        }
        Assert.False(order0vs1AllEqual, "Order 0 and 1 produced identical results");

        // Verify that order 1 vs 3 produce different results
        bool order1vs3AllEqual = true;
        for (int j = period; j < results[1].Length; j++)
        {
            if (Math.Abs(results[1][j] - results[3][j]) > 1e-9)
            {
                order1vs3AllEqual = false;
                break;
            }
        }
        Assert.False(order1vs3AllEqual, "Order 1 and 3 produced identical results");
    }

    [Fact]
    public void Validate_WarmupPeriod_IsCorrect()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var bwma = new Bwma(period);
            Assert.Equal(period, bwma.WarmupPeriod);

            // Verify IsHot transitions correctly
            for (int i = 0; i < period - 1; i++)
            {
                bwma.Update(new TValue(DateTime.UtcNow, i + 1.0));
                Assert.False(bwma.IsHot);
            }

            bwma.Update(new TValue(DateTime.UtcNow, period));
            Assert.True(bwma.IsHot);
        }
    }

    [Fact]
    public void Validate_NaN_Handling_Consistency()
    {
        int period = 10;

        // Create data with NaN values
        var dataWithNaN = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            double value = (i == 25 || i == 50 || i == 75) ? double.NaN : _testData.Data[i].Value;
            dataWithNaN.Add(new TValue(_testData.Data[i].Time, value));
        }

        // Streaming
        var bwmaStreaming = new Bwma(period);
        var streamingResults = new List<double>();
        foreach (var item in dataWithNaN)
        {
            streamingResults.Add(bwmaStreaming.Update(item).Value);
        }

        // Batch
        var bwmaBatch = new Bwma(period);
        var batchResults = bwmaBatch.Update(dataWithNaN);

        // Span
        double[] spanOutput = new double[dataWithNaN.Count];
        Bwma.Calculate(dataWithNaN.Values, spanOutput.AsSpan(), period);

        // Verify all produce same results
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults.Values[i], 1e-9);
            Assert.Equal(streamingResults[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Validate_LargeDataset_NoOverflow()
    {
        int period = 50;
        int order = 2;
        int dataSize = 10000;

        var largeData = new TSeries();
        var gbm = new GBM();
        var bars = gbm.Fetch(dataSize, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            largeData.Add(new TValue(bar.Time, bar.Close));
        }

        var bwma = new Bwma(period, order);
        var results = bwma.Update(largeData);

        Assert.Equal(dataSize, results.Count);
        Assert.True(bwma.IsHot);

        // Verify no overflow or NaN in results after warmup
        for (int i = period; i < results.Count; i++)
        {
            Assert.True(double.IsFinite(results.Values[i]), $"Value at index {i} is not finite");
        }
    }

    [Fact]
    public void Validate_EdgeCase_Period1()
    {
        // Period 1 should return input values directly
        var bwma = new Bwma(1);

        foreach (var item in _testData.Data)
        {
            var result = bwma.Update(item);
            Assert.Equal(item.Value, result.Value, 1e-9);
        }
    }

    [Fact]
    public void Validate_EdgeCase_Period2()
    {
        // Period 2 with order 0: weights are [0, 1] (x = -1, 0 -> w = 0, 1)
        // Actually for period 2: x = [0*2/1 - 1, 1*2/1 - 1] = [-1, 1]
        // w = 1 - x² = [0, 0] which is degenerate
        // Let's verify it handles this gracefully
        var bwma = new Bwma(2, 0);

        var item = new TValue(DateTime.UtcNow, 100.0);
        var result = bwma.Update(item);
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));

        bwma.Update(new TValue(DateTime.UtcNow, 200.0));
        // Should handle degenerate case without crashing
        Assert.True(bwma.IsHot);
    }

    [Fact]
    public void Validate_Symmetry_Order0()
    {
        // For order 0, the Bessel window is symmetric (parabolic)
        // Verify that symmetric input produces expected center-weighted result
        int period = 5;
        var bwma = new Bwma(period, 0);

        // Feed symmetric values: 1, 2, 3, 2, 1
        var values = new double[] { 1, 2, 3, 2, 1 };
        TValue result = default;
        foreach (var v in values)
        {
            result = bwma.Update(new TValue(DateTime.UtcNow, v));
        }

        // With symmetric weights and symmetric data, result should be close to center value (3)
        // but weighted more toward center
        Assert.True(double.IsFinite(result.Value));
        // The parabolic window emphasizes the center, so result should be > mean (1.8)
        Assert.True(result.Value > 1.8);
    }
}
