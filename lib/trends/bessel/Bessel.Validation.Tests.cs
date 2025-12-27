using System;
using System.Linq;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class BesselValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public BesselValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
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
    public void Validate_Internal_Span_Against_TSeries()
    {
        int[] lengths = { 5, 14, 20, 50 };

        foreach (int length in lengths)
        {
            // QuanTAlib Bessel via TSeries API
            var (qResult, _) = Bessel.Calculate(_testData.Data, length);

            // Same data via Span API
            var src = _testData.Data.Values.ToArray();
            var outSpan = new double[src.Length];
            Bessel.Calculate(src.AsSpan(), outSpan.AsSpan(), length);

            // Verify last window for convergence and consistency
            ValidationHelper.VerifyData(qResult, outSpan, lookback: 0, skip: length, tolerance: ValidationHelper.DefaultTolerance);
        }

        _output.WriteLine("Bessel validated internally: Span vs TSeries are consistent.");
    }
}

