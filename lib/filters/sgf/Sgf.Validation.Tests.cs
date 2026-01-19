using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class SgfValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public SgfValidationTests(ITestOutputHelper output)
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
        if (_disposed) return;
        if (disposing)
        {
            _testData?.Dispose();
        }
        _disposed = true;
    }

    /// <summary>
    /// Simple reference implementation of Savitzky-Golay filter for validation
    /// Uses same polynomial fitting logic but calculated independently
    /// </summary>
    private static double[] CalculateExpectedSgf(double[] source, int period, int polyOrder)
    {
        int adjPeriod = (period % 2 == 0) ? period - 1 : period;
        adjPeriod = Math.Max(1, adjPeriod);

        double[] weights = new double[adjPeriod];
        int halfWindow = adjPeriod / 2;
        double sumDenom = 0.0;

        // Calculate weights
        for (int i = 0; i < adjPeriod; i++)
        {
            int k = i - halfWindow;
            double weight = 0;
            if (polyOrder == 2)
            {
                weight = 3.0 * (3.0 * adjPeriod * adjPeriod - 7.0 - 20.0 * k * k);
            }
            else if (polyOrder == 4)
            {
                double k2 = k * k;
                weight = 15.0 + k2 * (-20.0 + k2 * 6.0);
            }
            else
            {
                weight = 1.0 - Math.Abs((double)k) / (double)halfWindow;
            }

            weights[i] = weight;
            sumDenom += weight;
        }

        // Normalize
        if (Math.Abs(sumDenom) > double.Epsilon)
        {
            for (int i = 0; i < adjPeriod; i++)
            {
                weights[i] /= sumDenom;
            }
        }

        double[] result = new double[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            double val = 0;
            double wSum = 0;

            // Convolution
            for (int k = 0; k < adjPeriod; k++)
            {
                // Align weights relative to history
                // weights[k] applies to source[i - (adjPeriod - 1) + k]

                int srcIdx = i - (adjPeriod - 1) + k;

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

            if (wSum > double.Epsilon)
                result[i] = val / wSum;
            else if (wSum <= double.Epsilon && wSum > -double.Epsilon)
                result[i] = double.NaN;
            else // Negative or small sum fallback usually just NaN or raw
                result[i] = ((i + 1) < adjPeriod) ? source[i] : double.NaN; // Match Sgf.cs partial window fallback logic roughly

            if (wSum <= double.Epsilon)
            {
                int availablePoints = Math.Min(i + 1, adjPeriod);
                if (availablePoints < adjPeriod)
                    result[i] = source[i];
                else
                    result[i] = double.NaN;
            }
        }

        return result;
    }

    [Fact]
    public void Validate_AgainstReference_Batch()
    {
        var source = _testData.Data.Select(x => x.Value).ToArray();

        // Test combination of periods and orders
        var scenarios = new[]
        {
            (period: 5, order: 2),
            (period: 9, order: 2),
            (period: 11, order: 4),
            (period: 21, order: 4)
        };

        foreach (var (period, order) in scenarios)
        {
            var sgf = new Sgf(period, order);
            var qResult = sgf.Update(_testData.Data);
            var expected = CalculateExpectedSgf(source, period, order);

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
            (period: 5, order: 2),
            (period: 9, order: 2),
            (period: 11, order: 4),
            (period: 21, order: 4)
        };

        foreach (var (period, order) in scenarios)
        {
            var sgf = new Sgf(period, order);
            var qResults = new List<double>();

            foreach (var item in _testData.Data)
            {
                qResults.Add(sgf.Update(item).Value);
            }

            var expected = CalculateExpectedSgf(source, period, order);

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
            (period: 5, order: 2),
            (period: 9, order: 2),
            (period: 11, order: 4),
            (period: 21, order: 4)
        };

        foreach (var (period, order) in scenarios)
        {
            double[] output = new double[source.Length];
            Sgf.Calculate(source.AsSpan(), output.AsSpan(), period, order);

            var expected = CalculateExpectedSgf(source, period, order);

            ValidationHelper.VerifyData(output, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Span mode successfully validated against reference implementation");
    }
}