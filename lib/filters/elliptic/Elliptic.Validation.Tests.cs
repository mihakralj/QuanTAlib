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
        var noisySignal = new List<double>();
        var random = new Random(42);
        
        // Generate 1000 points of White Noise around 100
        for (int i = 0; i < 1000; i++)
        {
            noisySignal.Add(100.0 + (random.NextDouble() - 0.5) * 20.0); // Range 90 to 110
        }

        var output = new List<double>();
        foreach (var val in noisySignal)
        {
            output.Add(filter.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        double inputStd = StdDev(noisySignal);
        double outputStd = StdDev(output);

        // A 2nd-order Elliptic lowpass should significantly reduce white noise variance
        // Expected reduction is substantial for Period=10
        Assert.True(outputStd < inputStd * 0.75, $"Output StdDev ({outputStd:F2}) should be significantly less than Input StdDev ({inputStd:F2})");
    }

    private static double StdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        double avg = values.Average();
        double sumSq = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }
}