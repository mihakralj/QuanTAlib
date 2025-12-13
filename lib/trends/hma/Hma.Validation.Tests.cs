using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class HmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public HmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData(count: 1000, seed: 42);
    }

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _testData.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 9, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (batch TSeries)
            var hma = new global::QuanTAlib.Hma(period);
            var qResult = hma.Update(_testData.Data);

            // Calculate Skender HMA
            var sResult = _testData.SkenderQuotes.GetHma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Hma, tolerance: 1e-5);
        }
        _output.WriteLine("HMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 9, 14, 20, 50 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (batch TSeries)
            var hma = new global::QuanTAlib.Hma(period);
            var qResult = hma.Update(_testData.Data);

            // Calculate Tulip HMA
            var hmaIndicator = Tulip.Indicators.hma;
            double[][] inputs = { tData };
            double[] options = { period };

            // HMA lookback is period + sqrt(period) - 1 roughly
            // We'll calculate the output size based on the input size and expected lookback
            // Tulip usually returns (input_len - lookback) elements
            // But we can just let it fill what it can if we provide a large enough buffer?
            // No, Tulip.NET wrapper usually expects exact size or it might crash/misbehave.
            // Let's try to be precise.
            // WMA(n) lookback = n-1
            // HMA = WMA(sqrt(n), 2*WMA(n/2) - WMA(n))
            // Path 1: WMA(n) -> valid at n-1
            // Path 2: WMA(n/2) -> valid at n/2-1
            // Combined: valid at max(n-1, n/2-1) = n-1
            // Then WMA(sqrt(n)) on that -> adds sqrt(n)-1 lag
            // Total lookback = (n-1) + (sqrt(n)-1) = n + sqrt(n) - 2

            int sqrtPeriod = (int)Math.Sqrt(period);
            int lookback = period + sqrtPeriod - 2;

            double[][] outputs = { new double[tData.Length - lookback] };

            hmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: 1e-5);
        }
        _output.WriteLine("HMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 9, 14, 20, 50 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (streaming)
            var hma = new global::QuanTAlib.Hma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(hma.Update(item).Value);
            }

            // Calculate Skender HMA
            var sResult = _testData.SkenderQuotes.GetHma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Hma);
        }
        _output.WriteLine("HMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 9, 14, 20, 50 };

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib HMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Hma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Skender HMA
            var sResult = _testData.SkenderQuotes.GetHma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Hma, tolerance: 1e-5);
        }
        _output.WriteLine("HMA Span validated successfully against Skender");
    }
}
