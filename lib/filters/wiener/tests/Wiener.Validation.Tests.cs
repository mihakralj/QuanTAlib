using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class WienerValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public WienerValidationTests(ITestOutputHelper output)
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

    private static double[] CalculateExpectedWiener(double[] source, int period, int smoothPeriod)
    {
        double[] result = new double[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            // Wiener needs at least 2 points to calculate noise variance (diffs)
            int count = i + 1;
            if (count < 2)
            {
                result[i] = source[i];
                continue;
            }

            // 1. Noise Variance
            // Loop lookback: min(count, period)
            // Diffs calculation logic matching Wiener.cs
            int noiseLen = Math.Min(count, period);
            double sumDiffs = 0;
            int numDiffs = 0;

            for (int k = 0; k < noiseLen - 1; k++)
            {
                // In Wiener.cs: _buffer[^(k+1)] - _buffer[^(k+2)]
                // Here: source[i-k] - source[i-k-1]
                double val1 = source[i - k];
                double val2 = source[i - k - 1];
                double diff = val1 - val2;
                sumDiffs += diff * diff;
                numDiffs++;
            }

            double noiseVar = 0;
            if (numDiffs > 0)
            {
                noiseVar = sumDiffs / (2.0 * numDiffs);
            }

            // 2. Signal Variance
            // Lookback: min(count, smoothPeriod)
            int signalLen = Math.Min(count, smoothPeriod);

            // a. Mean
            double sumSrc = 0;
            for (int k = 0; k < signalLen; k++)
            {
                sumSrc += source[i - k];
            }
            double mean = sumSrc / signalLen;

            // b. Signal + Noise (Variance around mean)
            double sumSqDev = 0;
            for (int k = 0; k < signalLen; k++)
            {
                double val = source[i - k];
                double dev = val - mean;
                sumSqDev += dev * dev;
            }
            double signalPlusNoise = sumSqDev / signalLen;

            // 3. Filter
            double signalVar = Math.Max(signalPlusNoise - noiseVar, 0.0);
            double kp = 0;
            if (signalVar + noiseVar > 1e-10) // epsilon
            {
                kp = signalVar / (signalVar + noiseVar);
            }

            result[i] = mean + (kp * (source[i] - mean));
        }

        return result;
    }

    [Fact]
    public void Validate_AgainstReference_Batch()
    {
        var source = _testData.Data.Select(x => x.Value).ToArray();

        var scenarios = new[]
        {
            (period: 5, smooth: 10),
            (period: 10, smooth: 5),
            (period: 20, smooth: 20),
            (period: 50, smooth: 14) // Typical TA settings
        };

        foreach (var (period, smooth) in scenarios)
        {
            var filter = new Wiener(period, smooth);
            var qResult = filter.Update(_testData.Data);
            var expected = CalculateExpectedWiener(source, period, smooth);

            ValidationHelper.VerifyData(qResult, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Batch mode successfully validated against reference implementation");
    }

    [Fact]
    public void Validate_AgainstReference_Streaming()
    {
        var source = _testData.Data.Select(x => x.Value).ToArray();

        var scenarios = new[]
        {
            (period: 5, smooth: 10),
            (period: 10, smooth: 5),
            (period: 20, smooth: 20)
        };

        foreach (var (period, smooth) in scenarios)
        {
            var filter = new Wiener(period, smooth);
            var qResults = new List<double>();

            foreach (var item in _testData.Data)
            {
                qResults.Add(filter.Update(item).Value);
            }

            var expected = CalculateExpectedWiener(source, period, smooth);

            ValidationHelper.VerifyData(qResults, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Streaming mode successfully validated against reference implementation");
    }

    [Fact]
    public void Validate_AgainstReference_Span()
    {
        var source = _testData.Data.Select(x => x.Value).ToArray();

        var scenarios = new[]
        {
            (period: 5, smooth: 10),
            (period: 10, smooth: 5),
            (period: 20, smooth: 20)
        };

        foreach (var (period, smooth) in scenarios)
        {
            double[] output = new double[source.Length];
            Wiener.Batch(source.AsSpan(), output.AsSpan(), period, smooth);

            var expected = CalculateExpectedWiener(source, period, smooth);

            ValidationHelper.VerifyData(output, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Span mode successfully validated against reference implementation");
    }
}
