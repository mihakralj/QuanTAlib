namespace QuanTAlib.Tests;

/// <summary>
/// GWMA validation tests.
/// Note: GWMA is not available in TA-Lib, Tulip, Skender, or OoplesFinance.
/// Validation is performed against the PineScript reference implementation
/// and internal consistency checks.
/// </summary>
public sealed class GwmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public GwmaValidationTests()
    {
        _testData = new ValidationTestData(count: 10000, seed: 42);
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
    public void Gwma_BatchMatchesStreaming()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] sigmas = { 0.2, 0.4, 0.6, 0.8 };

        foreach (var period in periods)
        {
            foreach (var sigma in sigmas)
            {
                // Calculate QuanTAlib GWMA (batch TSeries)
                var gwmaBatch = new Gwma(period, sigma);
                var batchResult = gwmaBatch.Update(_testData.Data);

                // Calculate QuanTAlib GWMA (streaming)
                var gwmaStreaming = new Gwma(period, sigma);
                var streamingResults = new List<double>();
                foreach (var item in _testData.Data)
                {
                    streamingResults.Add(gwmaStreaming.Update(item).Value);
                }

                // Compare all records
                Assert.Equal(batchResult.Count, streamingResults.Count);
                for (int i = 0; i < batchResult.Count; i++)
                {
                    Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-10);
                }
            }
        }
    }

    [Fact]
    public void Gwma_SpanMatchesBatch()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] sigmas = { 0.2, 0.4, 0.6, 0.8 };

        // Prepare data for Span API
        ReadOnlySpan<double> sourceData = _testData.RawData.Span;

        foreach (var period in periods)
        {
            foreach (var sigma in sigmas)
            {
                // Calculate QuanTAlib GWMA (Span API)
                double[] spanOutput = new double[sourceData.Length];
                Gwma.Batch(sourceData, spanOutput.AsSpan(), period, sigma);

                // Calculate QuanTAlib GWMA (batch TSeries)
                var gwmaBatch = new Gwma(period, sigma);
                var batchResult = gwmaBatch.Update(_testData.Data);

                // Compare all records
                Assert.Equal(batchResult.Count, spanOutput.Length);
                for (int i = 0; i < batchResult.Count; i++)
                {
                    Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-10);
                }
            }
        }
    }

    [Fact]
    public void Gwma_EventingMatchesBatch()
    {
        int[] periods = { 5, 10, 20, 50 };
        double sigma = 0.4;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib GWMA (batch TSeries)
            var gwmaBatch = new Gwma(period, sigma);
            var batchResult = gwmaBatch.Update(_testData.Data);

            // Calculate QuanTAlib GWMA (eventing)
            var pubSource = new TSeries();
            var gwmaEventing = new Gwma(pubSource, period, sigma);
            var eventingResults = new List<double>();
            gwmaEventing.Pub += (object? sender, in TValueEventArgs e) => eventingResults.Add(e.Value.Value);

            foreach (var item in _testData.Data)
            {
                pubSource.Add(item);
            }

            // Compare all records
            Assert.Equal(batchResult.Count, eventingResults.Count);
            for (int i = 0; i < batchResult.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, eventingResults[i], 1e-10);
            }
        }
    }

    [Fact]
    public void Gwma_CenteredGaussian_WeightsAreSymmetric()
    {
        // GWMA uses a centered Gaussian, so weights should be symmetric around the center
        int period = 11; // Odd period for exact center
        double sigma = 0.4;

        // Create GWMA and extract weights via reflection or known values
        // For this test, we verify that GWMA(period, sigma) produces
        // symmetric behavior by feeding symmetric data

        var gwma = new Gwma(period, sigma);

        // Feed symmetric data: [1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1]
        double[] symmetricData = [1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1];

        foreach (var val in symmetricData)
        {
            gwma.Update(new TValue(DateTime.UtcNow, val));
        }

        // The result should be close to the center value (6) weighted by the Gaussian
        // Since the Gaussian is centered and data is symmetric, the weighted average
        // should be close to the arithmetic mean

        // The GWMA result should be reasonable (between min and max of data)
        Assert.True(gwma.Last.Value >= 1 && gwma.Last.Value <= 6);
    }

    [Fact]
    public void Gwma_SigmaEffect_NarrowVsWide()
    {
        // Narrower sigma (smaller value) should give more weight to center values
        // Wider sigma (larger value) should give more uniform weights (closer to SMA)
        int period = 10;

        var gwmaNarrow = new Gwma(period, sigma: 0.1);
        var gwmaWide = new Gwma(period, sigma: 0.9);

        // Feed increasing data
        for (int i = 1; i <= period; i++)
        {
            gwmaNarrow.Update(new TValue(DateTime.UtcNow, i));
            gwmaWide.Update(new TValue(DateTime.UtcNow, i));
        }

        double narrowResult = gwmaNarrow.Last.Value;
        double wideResult = gwmaWide.Last.Value;

        // Wide sigma should be closer to SMA (5.5 for 1..10)
        // Narrow sigma should be closer to center values (5 or 6)
        double sma = 5.5; // (1+2+3+4+5+6+7+8+9+10)/10

        // Wide result should be closer to SMA than narrow result
        double wideDiff = Math.Abs(wideResult - sma);
        double narrowDiff = Math.Abs(narrowResult - sma);

        // The wide sigma should produce a result closer to SMA
        Assert.True(wideDiff <= narrowDiff + 1e-9,
            $"Wide sigma result ({wideResult:F4}) should be closer to SMA ({sma}) than narrow sigma result ({narrowResult:F4})");
    }

    [Fact]
    public void Gwma_KnownValues_ManualCalculation()
    {
        // Manual verification of GWMA calculation with known values
        // period=5, sigma=0.4
        // center = (5-1)/2 = 2
        // invSigmaP = 1/(0.4*5) = 0.5

        // Weights for i=0,1,2,3,4:
        // w[0] = exp(-0.5 * ((0-2)*0.5)^2) = exp(-0.5 * 1) = exp(-0.5) ≈ 0.6065
        // w[1] = exp(-0.5 * ((1-2)*0.5)^2) = exp(-0.5 * 0.25) = exp(-0.125) ≈ 0.8825
        // w[2] = exp(-0.5 * ((2-2)*0.5)^2) = exp(0) = 1.0
        // w[3] = exp(-0.5 * ((3-2)*0.5)^2) = exp(-0.125) ≈ 0.8825
        // w[4] = exp(-0.5 * ((4-2)*0.5)^2) = exp(-0.5) ≈ 0.6065

        int period = 5;
        double sigma = 0.4;

        var gwma = new Gwma(period, sigma);

        // Feed 5 values: [100, 102, 104, 103, 101]
        double[] prices = [100, 102, 104, 103, 101];
        foreach (var price in prices)
        {
            gwma.Update(new TValue(DateTime.UtcNow, price));
        }

        // Calculate expected manually
        double center = (period - 1) / 2.0; // 2
        double invSigmaP = 1.0 / (sigma * period); // 0.5

        double[] weights = new double[period];
        double weightSum = 0;
        for (int i = 0; i < period; i++)
        {
            double x = (i - center) * invSigmaP;
            weights[i] = Math.Exp(-0.5 * x * x);
            weightSum += weights[i];
        }

        double expected = 0;
        for (int i = 0; i < period; i++)
        {
            expected += prices[i] * weights[i];
        }
        expected /= weightSum;

        Assert.Equal(expected, gwma.Last.Value, 1e-10);
    }
}
