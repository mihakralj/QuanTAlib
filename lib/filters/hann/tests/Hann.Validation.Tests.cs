using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class HannValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public HannValidationTests(ITestOutputHelper output)
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
    /// Simple reference implementation of Hann filter for validation, matching Pine script logic.
    /// </summary>
    private static double[] CalculateExpectedHann(double[] source, int length)
    {
        // Pine Script Logic:
        // 1. Generate weights: w[i] = 0.5 * (1 - cos(2*pi*i/(len-1)))
        // 2. Convolution: summation of price[lag_i]*w[i]
        // 3. Normalize by sum of weights used (handles NaNs)

        double[] weights = new double[length];
        double denom = length - 1;
        for (int i = 0; i < length; i++)
        {
            weights[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / denom));
        }

        double[] result = new double[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            // p = min(bar_index + 1, len)
            int p = Math.Min(i + 1, length);

            double acc = 0;
            double wSum = 0;

            for (int k = 0; k < p; k++)
            {
                int srcIdx = i - (p - 1) + k;
                double val = source[srcIdx];
                if (!double.IsNaN(val))
                {
                    double w = weights[k];
                    acc += val * w;
                    wSum += w;
                }
            }

            if (wSum > double.Epsilon)
            {
                result[i] = acc / wSum;
            }
            else
            {
                result[i] = source[i];
            }
        }

        return result;
    }

    [Fact]
    public void Validate_AgainstReference_Batch()
    {
        int[] lengths = { 5, 10, 20 };
        double[] source = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var len in lengths)
        {
            var hann = new Hann(len);
            var qResult = hann.Update(_testData.Data);
            var expected = CalculateExpectedHann(source, len);

            ValidationHelper.VerifyData(qResult, expected, (refVal) => refVal, tolerance: 1e-9);
            _output.WriteLine($"Batch mode (len={len}) passed exact match");
        }
    }

    [Fact]
    public void Validate_AgainstReference_Streaming()
    {
        int[] lengths = { 5, 10, 20 };
        double[] source = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var len in lengths)
        {
            var hann = new Hann(len);
            var qResults = new List<double>();

            foreach (var item in _testData.Data)
            {
                qResults.Add(hann.Update(item).Value);
            }

            var expected = CalculateExpectedHann(source, len);

            ValidationHelper.VerifyData(qResults, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Streaming mode successfully validated");
    }

    [Fact]
    public void Validate_AgainstReference_Span()
    {
        int[] lengths = { 5, 10, 20 };
        double[] source = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var len in lengths)
        {
            double[] output = new double[source.Length];
            Hann.Batch(source.AsSpan(), output.AsSpan(), len);

            var expected = CalculateExpectedHann(source, len);

            ValidationHelper.VerifyData(output, expected, (refVal) => refVal, tolerance: 1e-9);
        }
        _output.WriteLine("Span mode successfully validated");
    }
}
