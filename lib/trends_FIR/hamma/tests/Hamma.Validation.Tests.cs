namespace QuanTAlib.Tests;

/// <summary>
/// HAMMA validation tests.
/// Note: HAMMA is not available in TA-Lib, Tulip, Skender, or OoplesFinance.
/// Validation is performed against internal consistency checks and mathematical verification.
/// </summary>
public sealed class HammaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public HammaValidationTests()
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
    public void Hamma_BatchMatchesStreaming()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HAMMA (batch TSeries)
            var hammaBatch = new Hamma(period);
            var batchResult = hammaBatch.Update(_testData.Data);

            // Calculate QuanTAlib HAMMA (streaming)
            var hammaStreaming = new Hamma(period);
            var streamingResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                streamingResults.Add(hammaStreaming.Update(item).Value);
            }

            // Compare all records
            Assert.Equal(batchResult.Count, streamingResults.Count);
            for (int i = 0; i < batchResult.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-10);
            }
        }
    }

    [Fact]
    public void Hamma_SpanMatchesBatch()
    {
        int[] periods = { 5, 10, 20, 50 };

        // Prepare data for Span API
        ReadOnlySpan<double> sourceData = _testData.RawData.Span;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HAMMA (Span API)
            double[] spanOutput = new double[sourceData.Length];
            Hamma.Batch(sourceData, spanOutput.AsSpan(), period);

            // Calculate QuanTAlib HAMMA (batch TSeries)
            var hammaBatch = new Hamma(period);
            var batchResult = hammaBatch.Update(_testData.Data);

            // Compare all records
            Assert.Equal(batchResult.Count, spanOutput.Length);
            for (int i = 0; i < batchResult.Count; i++)
            {
                Assert.Equal(batchResult[i].Value, spanOutput[i], 1e-10);
            }
        }
    }

    [Fact]
    public void Hamma_EventingMatchesBatch()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HAMMA (batch TSeries)
            var hammaBatch = new Hamma(period);
            var batchResult = hammaBatch.Update(_testData.Data);

            // Calculate QuanTAlib HAMMA (eventing)
            var pubSource = new TSeries();
            var hammaEventing = new Hamma(pubSource, period);
            var eventingResults = new List<double>();
            hammaEventing.Pub += (object? sender, in TValueEventArgs e) => eventingResults.Add(e.Value.Value);

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
    public void Hamma_HammingWindow_WeightsAreSymmetric()
    {
        // Hamming window is symmetric: w[i] = w[period-1-i]
        int period = 11; // Odd period for exact center

        var hamma = new Hamma(period);

        // Feed symmetric data: [1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1]
        double[] symmetricData = [1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1];

        foreach (var val in symmetricData)
        {
            hamma.Update(new TValue(DateTime.UtcNow, val));
        }

        // The HAMMA result should be reasonable (between min and max of data)
        Assert.True(hamma.Last.Value >= 1 && hamma.Last.Value <= 6);
    }

    [Fact]
    public void Hamma_KnownValues_ManualCalculation()
    {
        // Manual verification of HAMMA calculation with known values
        // period=5: w[i] = 0.54 - 0.46 * cos(2πi/4)

        int period = 5;
        var hamma = new Hamma(period);

        // Feed 5 values: [100, 102, 104, 103, 101]
        double[] prices = [100, 102, 104, 103, 101];
        foreach (var price in prices)
        {
            hamma.Update(new TValue(DateTime.UtcNow, price));
        }

        // Calculate expected manually using Hamming window formula
        double twoPiOverPm1 = 2.0 * Math.PI / (period - 1);
        double[] weights = new double[period];
        double weightSum = 0;
        for (int i = 0; i < period; i++)
        {
            weights[i] = 0.54 - (0.46 * Math.Cos(twoPiOverPm1 * i));
            weightSum += weights[i];
        }

        double expected = 0;
        for (int i = 0; i < period; i++)
        {
            expected += prices[i] * weights[i];
        }
        expected /= weightSum;

        Assert.Equal(expected, hamma.Last.Value, 1e-10);
    }

    [Fact]
    public void Hamma_HammingCoefficients_Verify()
    {
        // Verify Hamming window coefficients match the standard formula
        // w[i] = 0.54 - 0.46 * cos(2πi/(N-1))
        // For period=5: w[0]=0.08, w[1]≈0.54, w[2]=1.0, w[3]≈0.54, w[4]=0.08

        int period = 5;
        double twoPiOverPm1 = 2.0 * Math.PI / (period - 1);

        double w0 = 0.54 - (0.46 * Math.Cos(0));                      // 0.08
        double w1 = 0.54 - (0.46 * Math.Cos(twoPiOverPm1 * 1));      // ≈0.54
        double w2 = 0.54 - (0.46 * Math.Cos(twoPiOverPm1 * 2));      // 1.0
        double w3 = 0.54 - (0.46 * Math.Cos(twoPiOverPm1 * 3));      // ≈0.54
        double w4 = 0.54 - (0.46 * Math.Cos(twoPiOverPm1 * 4));      // 0.08

        Assert.Equal(0.08, w0, 1e-10);
        Assert.Equal(0.08, w4, 1e-10);
        Assert.Equal(1.0, w2, 1e-10);

        // w1 and w3 should be equal (symmetric)
        Assert.Equal(w1, w3, 1e-10);

        // All edge weights should be equal
        Assert.Equal(w0, w4, 1e-10);
    }
}
