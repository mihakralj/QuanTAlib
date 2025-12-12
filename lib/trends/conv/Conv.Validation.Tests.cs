using System;
using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public class ConvValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public ConvValidationTests()
    {
        _testData = new ValidationTestData(count: 1000, seed: 123);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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

    [Fact]
    public void Validate_Against_Sma()
    {
        // SMA(10) is equivalent to Conv with 10 weights of 1/10
        int period = 10;
        double weight = 1.0 / period;
        double[] kernel = new double[period];
        Array.Fill(kernel, weight);

        var sma = new Sma(period);
        var conv = new Conv(kernel);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var item = _testData.Data[i];
            var smaVal = sma.Update(item);
            var convVal = conv.Update(item);

            if (i >= period) // Skip warmup
            {
                Assert.Equal(smaVal.Value, convVal.Value, 1e-4);
            }
        }
    }

    [Fact]
    public void Validate_Against_Wma()
    {
        // WMA(10) weights are 1, 2, ..., 10 divided by sum(1..10)
        int period = 10;
        double divisor = period * (period + 1) / 2.0;
        double[] kernel = new double[period];
        for (int i = 0; i < period; i++)
        {
            kernel[i] = (i + 1) / divisor;
        }

        var wma = new Wma(period);
        var conv = new Conv(kernel);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var item = _testData.Data[i];
            var wmaVal = wma.Update(item);
            var convVal = conv.Update(item);

            if (i >= period) // Skip warmup
            {
                Assert.Equal(wmaVal.Value, convVal.Value, 1e-4);
            }
        }
    }

    [Fact]
    public void Validate_Against_Trima()
    {
        // TRIMA(10) - Even period
        // Weights: 1, 2, 3, 4, 5, 5, 4, 3, 2, 1
        // Sum: 30
        int period = 10;
        double[] kernel = new double[period];
        double sum = 0;

        // Generate triangular weights
        int mid = period / 2;
        for (int i = 0; i < period; i++)
        {
            // For even period 10:
            // i=0 -> 1
            // i=4 -> 5
            // i=5 -> 5
            // i=9 -> 1

            // Distance from ends?
            // 0 -> 1
            // 1 -> 2
            // ...
            // mid-1 -> mid
            // mid -> mid

            double val = (i < mid) ? (i + 1) : (period - i);
            kernel[i] = val;
            sum += val;
        }

        // Normalize
        for (int i = 0; i < period; i++)
        {
            kernel[i] /= sum;
        }

        var trima = new Trima(period);
        var conv = new Conv(kernel);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var item = _testData.Data[i];
            var trimaVal = trima.Update(item);
            var convVal = conv.Update(item);

            if (i >= period) // Skip warmup
            {
                Assert.Equal(trimaVal.Value, convVal.Value, 1e-4);
            }
        }
    }

}
