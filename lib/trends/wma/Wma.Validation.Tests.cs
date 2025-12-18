using System;
using System.Collections.Generic;
using System.Linq;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class WmaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public WmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (batch TSeries)
            var wma = new global::QuanTAlib.Wma(period);
            var qResult = wma.Update(_testData.Data);

            // Calculate Skender WMA
            var sResult = _testData.SkenderQuotes.GetWma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, x => x.Wma, tolerance: 1e-5);
        }
        _output.WriteLine("WMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (streaming)
            var wma = new global::QuanTAlib.Wma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(wma.Update(item).Value);
            }

            // Calculate Skender WMA
            var sResult = _testData.SkenderQuotes.GetWma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, x => x.Wma, tolerance: 1e-5);
        }
        _output.WriteLine("WMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Wma.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate Skender WMA
            var sResult = _testData.SkenderQuotes.GetWma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, x => x.Wma, tolerance: 1e-5);
        }
        _output.WriteLine("WMA Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] output = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (batch TSeries)
            var wma = new global::QuanTAlib.Wma(period);
            var qResult = wma.Update(_testData.Data);

            // Calculate TA-Lib WMA
            var retCode = TALib.Functions.Wma<double>(_testData.RawData.Span, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.WmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback, tolerance: 1e-4);
        }
        _output.WriteLine("WMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] output = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (streaming)
            var wma = new global::QuanTAlib.Wma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(wma.Update(item).Value);
            }

            // Calculate TA-Lib WMA
            var retCode = TALib.Functions.Wma<double>(_testData.RawData.Span, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.WmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback, tolerance: 1e-4);
        }
        _output.WriteLine("WMA Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] talibOutput = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Wma.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate TA-Lib WMA
            var retCode = TALib.Functions.Wma<double>(_testData.RawData.Span, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.WmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback, tolerance: 1e-4);
        }
        _output.WriteLine("WMA Span validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (batch TSeries)
            var wma = new global::QuanTAlib.Wma(period);
            var qResult = wma.Update(_testData.Data);

            // Calculate Tulip WMA
            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs = { _testData.RawData.ToArray() };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[_testData.RawData.Length - lookback] };

            wmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: 1e-4);
        }
        _output.WriteLine("WMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (streaming)
            var wma = new global::QuanTAlib.Wma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(wma.Update(item).Value);
            }

            // Calculate Tulip WMA
            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs = { _testData.RawData.ToArray() };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[_testData.RawData.Length - lookback] };

            wmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: 1e-4);
        }
        _output.WriteLine("WMA Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Wma.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate Tulip WMA
            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs = { _testData.RawData.ToArray() };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[_testData.RawData.Length - lookback] };

            wmaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, tResult, lookback, tolerance: 1e-4);
        }
        _output.WriteLine("WMA Span validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Ooples (List<TickerData>)
        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Close = (double)q.Close,
            High = (double)q.High,
            Low = (double)q.Low,
            Open = (double)q.Open,
            Volume = (double)q.Volume
        }).ToList();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib WMA
            var wma = new global::QuanTAlib.Wma(period);
            var qResult = wma.Update(_testData.Data);

            // Calculate Ooples WMA
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateWeightedMovingAverage(length: period);
            var oValues = oResult.OutputValues["Wma"];

            // Compare
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: 5e-4);
        }
        _output.WriteLine("WMA validated successfully against Ooples");
    }
}
