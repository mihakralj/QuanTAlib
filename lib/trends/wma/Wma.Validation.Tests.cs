using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Skender.Stock.Indicators;
using TALib;

namespace QuanTAlib.Tests;

public sealed class WmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public WmaValidationTests(ITestOutputHelper output)
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
        if (_disposed) return;
        _disposed = true;
        if (disposing) _testData?.Dispose();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var wma = new Wma(period);
            var qResult = wma.Update(_testData.Data);

            var sResult = _testData.SkenderQuotes.GetWma(period).ToList();

            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Wma);
        }
        _output.WriteLine("WMA Batch(TSeries) validated against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var wma = new Wma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(wma.Update(item).Value);
            }

            var sResult = _testData.SkenderQuotes.GetWma(period).ToList();

            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Wma);
        }
        _output.WriteLine("WMA Streaming validated against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[sourceData.Length];
            Wma.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            var sResult = _testData.SkenderQuotes.GetWma(period).ToList();

            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Wma);
        }
        _output.WriteLine("WMA Span validated against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            var wma = new Wma(period);
            var qResult = wma.Update(_testData.Data);

            var retCode = TALib.Functions.Wma<double>(
                tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.WmaLookback(period);

            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("WMA Batch validated against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            var wma = new Wma(period);
            var qResult = wma.Update(_testData.Data);

            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            wmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("WMA Batch validated against Tulip");
    }
}
