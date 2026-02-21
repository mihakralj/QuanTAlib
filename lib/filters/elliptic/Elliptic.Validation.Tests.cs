using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class EllipticValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;

    public EllipticValidationTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        _testData.Dispose();
    }

    [Fact]
    public void Validate_NoiseReduction()
    {
        // Elliptic filters are efficient at removing high-frequency noise.
        // We generate a noisy signal (Constant + White Noise) and verify variance reduction.

        var filter = new Elliptic(10);
        int N = 1000;
        // Use GBM log-returns as noise around 100 to create high-frequency jitter
        var closes = new GBM(seed: 42).Fetch(N + 1, DateTime.UtcNow.Ticks, TimeSpan.FromSeconds(1)).CloseValues;
        var noisySignal = new List<double>(N);
        for (int i = 0; i < N; i++)
        {
            noisySignal.Add(100.0 + (closes[i + 1] / closes[i] - 1.0) * 1000.0); // amplified jitter around 100
        }

        var output = new List<double>();
        for (int i = 0; i < N; i++)
        {
            output.Add(filter.Update(new TValue(DateTime.UtcNow.AddSeconds(i), noisySignal[i])).Value);
        }

        double inputStd = StdDev(noisySignal);
        double outputStd = StdDev(output);

        // A 2nd-order Elliptic lowpass should significantly reduce white noise variance
        // Expected reduction is substantial for Period=10
        Assert.True(outputStd < inputStd * 0.75, $"Output StdDev ({outputStd:F2}) should be significantly less than Input StdDev ({inputStd:F2})");
    }

    private static double StdDev(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        double avg = values.Average();
        double sumSq = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }
}
