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

public class RsiValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public RsiValidationTests(ITestOutputHelper output)
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
        if (disposing)
        {
            _testData.Dispose();
        }
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 9, 14, 25 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (batch TSeries)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResult = rsi.Update(_testData.Data);

            // Calculate Skender RSI
            var sResult = _testData.SkenderQuotes.GetRsi(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Rsi);
        }
        _output.WriteLine("RSI Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 9, 14, 25 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (streaming)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(rsi.Update(item).Value);
            }

            // Calculate Skender RSI
            var sResult = _testData.SkenderQuotes.GetRsi(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Rsi);
        }
        _output.WriteLine("RSI Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Rsi.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Skender RSI
            var sResult = _testData.SkenderQuotes.GetRsi(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Rsi);
        }
        _output.WriteLine("RSI Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (batch TSeries)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResult = rsi.Update(_testData.Data);

            // Calculate TA-Lib RSI
            var retCode = TALib.Functions.Rsi<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.RsiLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("RSI Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (streaming)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(rsi.Update(item).Value);
            }

            // Calculate TA-Lib RSI
            var retCode = TALib.Functions.Rsi<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.RsiLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("RSI Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (batch TSeries)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResult = rsi.Update(_testData.Data);

            // Calculate Tulip RSI
            var rsiIndicator = Tulip.Indicators.rsi;
            double[][] inputs = { tData };
            double[] options = { period };
            
            // Tulip RSI lookback
            int lookback = rsiIndicator.Start(options);
            double[][] outputs = { new double[tData.Length - lookback] };

            rsiIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("RSI Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (streaming)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(rsi.Update(item).Value);
            }

            // Calculate Tulip RSI
            var rsiIndicator = Tulip.Indicators.rsi;
            double[][] inputs = { tData };
            double[] options = { period };
            
            // Tulip RSI lookback
            int lookback = rsiIndicator.Start(options);
            double[][] outputs = { new double[tData.Length - lookback] };

            rsiIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback);
        }
        _output.WriteLine("RSI Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        int[] periods = { 9, 14, 25 };

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
            // Calculate QuanTAlib RSI
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResult = rsi.Update(_testData.Data);

            // Calculate Ooples RSI
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateRelativeStrengthIndex(length: period);
            var oValues = oResult.OutputValues.Values.First();

            // Compare
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("RSI validated successfully against Ooples");
    }
}
