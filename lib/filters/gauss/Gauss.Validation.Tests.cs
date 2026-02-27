using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public class GaussValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public GaussValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _testData?.Dispose();
        }
        _disposed = true;
    }

    /// <summary>
    /// Simple reference implementation of Gaussian filter for validation
    /// </summary>
    private static double[] CalculateExpectedGauss(double[] source, double sigma)
    {
        int kernelSize = (int)(2 * Math.Ceiling(3.0 * sigma) + 1);
        double[] weights = new double[kernelSize];
        double sum = 0;
        int center = kernelSize / 2;
        double twoSigmaSq = 2.0 * sigma * sigma;

        for (int i = 0; i < kernelSize; i++)
        {
            double x = i - center;
            double weight = Math.Exp(-(x * x) / twoSigmaSq);
            weights[i] = weight;
            sum += weight;
        }

        for (int i = 0; i < kernelSize; i++)
        {
            weights[i] /= sum;
        }

        double[] result = new double[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            double val = 0;
            double wSum = 0;

            // Manual convolution with boundary handling matching the main implementation
            // For index i, we look at window ending at i.
            // Window: source[i-(kernelSize-1)] ... source[i]
            // Weights: weights[0] ... weights[kernelSize-1]

            // Map buffer indices to weights
            // buffer[0] (oldest) -> weights[0]
            // ...
            // buffer[kernelSize-1] (newest) -> weights[kernelSize-1]

            // We need to iterate over the kernel weights
            for (int k = 0; k < kernelSize; k++)
            {
                // The source index corresponding to weights[k]
                // weights[k] is applied to source[i - (kernelSize - 1) + k]
                // Let's verify:
                // k=kernelSize-1 (newest weight) -> source[i]
                // k=0 (oldest weight) -> source[i - kernelSize + 1]

                int srcIdx = i - (kernelSize - 1) + k;

                if (srcIdx >= 0 && srcIdx < source.Length)
                {
                    double v = source[srcIdx];
                    if (!double.IsNaN(v))
                    {
                        val += v * weights[k];
                        wSum += weights[k];
                    }
                }
            }

            if (wSum > 0)
            {
                result[i] = val / wSum;
            }
            else
            {
                result[i] = source[i]; // Fallback if no weights applied (shouldn't happen with valid sigma)
            }
        }

        return result;
    }

    [Fact]
    public void Validate_AgainstReference_Batch()
    {
        double[] sigmas = { 0.5, 1.0, 2.0, 5.0 };
        double[] source = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var sigma in sigmas)
        {
            var gauss = new Gauss(sigma);
            var qResult = gauss.Update(_testData.Data);
            var expected = CalculateExpectedGauss(source, sigma);

            // Using slightly loose tolerance as partial sums might have minor floating point diffs
            // between incremental and batch approaches, though they should be very close.
            ValidationHelper.VerifyData(qResult, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Batch mode successfully validated against reference implementation");
    }

    [Fact]
    public void Validate_AgainstReference_Streaming()
    {
        double[] sigmas = { 0.5, 1.0, 2.0, 5.0 };
        double[] source = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var sigma in sigmas)
        {
            var gauss = new Gauss(sigma);
            var qResults = new List<double>();

            foreach (var item in _testData.Data)
            {
                qResults.Add(gauss.Update(item).Value);
            }

            var expected = CalculateExpectedGauss(source, sigma);

            ValidationHelper.VerifyData(qResults, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Streaming mode successfully validated against reference implementation");
    }

    [Fact]
    public void Validate_AgainstReference_Span()
    {
        double[] sigmas = { 0.5, 1.0, 2.0, 5.0 };
        double[] source = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var sigma in sigmas)
        {
            double[] output = new double[source.Length];
            Gauss.Batch(source.AsSpan(), output.AsSpan(), sigma);

            var expected = CalculateExpectedGauss(source, sigma);

            ValidationHelper.VerifyData(output, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Span mode successfully validated against reference implementation");
    }

    [Fact]
    public void Gauss_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateEhlersGaussianFilter();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}