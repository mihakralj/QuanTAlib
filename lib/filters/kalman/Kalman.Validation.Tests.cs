using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public class KalmanValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public KalmanValidationTests()
    {
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
            _testData.Dispose();
        }

        _disposed = true;
    }

    [Fact]
    public void Validate_Against_Manual_Calculation()
    {
        // Manual calculation check for a small dataset
        double[] input = { 10.0, 10.5, 10.2, 10.8, 10.0 };
        double q = 0.01;
        double r = 0.1;
        var kalman = new Kalman(q, r);

        var actual = new List<double>();
        foreach (var val in input)
        {
            actual.Add(kalman.Update(new TValue(DateTime.UtcNow, val)).Value);
        }

        double[] expected = new double[input.Length];

        // Step-by-step manual recursion simulated here to verify implementation logic

        double x = 10.0; // Initial measurement sets state
        double p = r;    // Initial P matches implementation (P = R)
        expected[0] = 10.0;

        for (int i = 1; i < input.Length; i++)
        {
            // Prediction
            double p_pred = p + q;

            // Gain
            double denom = p_pred + r;
            double k = p_pred / denom;

            // Update
            x = x + k * (input[i] - x);

            // p = (1 - k) * pPred
            // But implementation uses: p = (pPred * r) / denom
            // (1 - pPred/(pPred+r)) * pPred = ( (pPred+r - pPred) / (pPred+r) ) * pPred = (r * pPred) / denom
            // This is mathematically identical but numerically more stable

            p = (p_pred * r) / denom;

            expected[i] = x;
        }

        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 1e-9);
        }
    }
}