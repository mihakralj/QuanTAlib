using System;
using System.Collections.Generic;
using System.Linq;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class MacdValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public MacdValidationTests(ITestOutputHelper output)
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
    public void Validate_Skender_Batch()
    {
        // Standard MACD parameters
        int fastPeriod = 12;
        int slowPeriod = 26;
        int signalPeriod = 9;

        // Calculate QuanTAlib MACD (batch TSeries)
        var macd = new global::QuanTAlib.Macd(fastPeriod, slowPeriod, signalPeriod);
        var qResult = macd.Update(_testData.Data);

        // Calculate Skender MACD
        var sResult = _testData.SkenderQuotes.GetMacd(fastPeriod, slowPeriod, signalPeriod).ToList();

        // Compare last 100 records
        // MACD Line
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Macd);
        
        // Signal Line
        // We need to extract Signal line from QuanTAlib result. 
        // Since Update returns TSeries of MACD line, we need to access Signal property from the indicator instance
        // But for batch update, we need to re-run or capture signal.
        // The Macd.Update(TSeries) returns the MACD line series.
        // To validate Signal and Histogram, we should use the streaming approach or modify Macd to return all lines.
        // For now, let's validate MACD line here, and do full validation in Streaming test.
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int fastPeriod = 12;
        int slowPeriod = 26;
        int signalPeriod = 9;

        // Calculate QuanTAlib MACD (streaming)
        var macd = new global::QuanTAlib.Macd(fastPeriod, slowPeriod, signalPeriod);
        var qMacd = new List<double>();
        var qSignal = new List<double>();
        var qHist = new List<double>();

        foreach (var item in _testData.Data)
        {
            macd.Update(item);
            qMacd.Add(macd.Last.Value);
            qSignal.Add(macd.Signal.Value);
            qHist.Add(macd.Histogram.Value);
        }

        // Calculate Skender MACD
        var sResult = _testData.SkenderQuotes.GetMacd(fastPeriod, slowPeriod, signalPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qMacd, sResult, (s) => s.Macd);
        ValidationHelper.VerifyData(qSignal, sResult, (s) => s.Signal);
        ValidationHelper.VerifyData(qHist, sResult, (s) => s.Histogram);

        _output.WriteLine("MACD Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int fastPeriod = 12;
        int slowPeriod = 26;
        int signalPeriod = 9;

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] outMacd = new double[tData.Length];
        double[] outSignal = new double[tData.Length];
        double[] outHist = new double[tData.Length];

        // Calculate QuanTAlib MACD (streaming)
        var macd = new global::QuanTAlib.Macd(fastPeriod, slowPeriod, signalPeriod);
        var qMacd = new List<double>();
        var qSignal = new List<double>();
        var qHist = new List<double>();

        foreach (var item in _testData.Data)
        {
            macd.Update(item);
            qMacd.Add(macd.Last.Value);
            qSignal.Add(macd.Signal.Value);
            qHist.Add(macd.Histogram.Value);
        }

        // Calculate TA-Lib MACD
        var retCode = TALib.Functions.Macd<double>(tData, 0..^0, outMacd, outSignal, outHist, out var outRange, fastPeriod, slowPeriod, signalPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MacdLookback(fastPeriod, slowPeriod, signalPeriod);

        // Compare last 100 records
        ValidationHelper.VerifyData(qMacd, outMacd, outRange, lookback);
        ValidationHelper.VerifyData(qSignal, outSignal, outRange, lookback);
        ValidationHelper.VerifyData(qHist, outHist, outRange, lookback);

        _output.WriteLine("MACD Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        int fastPeriod = 12;
        int slowPeriod = 26;
        int signalPeriod = 9;

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

        // Calculate QuanTAlib MACD (streaming)
        var macd = new global::QuanTAlib.Macd(fastPeriod, slowPeriod, signalPeriod);
        var qMacd = new List<double>();
        var qSignal = new List<double>();
        var qHist = new List<double>();

        foreach (var item in _testData.Data)
        {
            macd.Update(item);
            qMacd.Add(macd.Last.Value);
            qSignal.Add(macd.Signal.Value);
            qHist.Add(macd.Histogram.Value);
        }

        // Calculate Ooples MACD
        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateMovingAverageConvergenceDivergence(fastLength: fastPeriod, slowLength: slowPeriod, signalLength: signalPeriod);
        
        var oMacd = oResult.OutputValues["Macd"];
        var oSignal = oResult.OutputValues["Signal"];
        var oHist = oResult.OutputValues["Histogram"];

        // Compare
        ValidationHelper.VerifyData(qMacd, oMacd, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        ValidationHelper.VerifyData(qSignal, oSignal, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        ValidationHelper.VerifyData(qHist, oHist, (s) => s, tolerance: ValidationHelper.OoplesTolerance);

        _output.WriteLine("MACD validated successfully against Ooples");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        // Tulip has a hardcoded override for 12/26 that uses 0.15 and 0.075 instead of standard alpha
        // We use different periods to validate the algorithm correctness without this quirk
        int fastPeriod = 10;
        int slowPeriod = 20;
        int signalPeriod = 9;

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        // Calculate QuanTAlib MACD (streaming)
        var macd = new global::QuanTAlib.Macd(fastPeriod, slowPeriod, signalPeriod);
        var qMacd = new List<double>();
        var qSignal = new List<double>();
        var qHist = new List<double>();

        foreach (var item in _testData.Data)
        {
            macd.Update(item);
            qMacd.Add(macd.Last.Value);
            qSignal.Add(macd.Signal.Value);
            qHist.Add(macd.Histogram.Value);
        }

        // Calculate Tulip MACD
        var macdIndicator = Tulip.Indicators.macd;
        double[][] inputs = { tData };
        double[] options = { fastPeriod, slowPeriod, signalPeriod };
        
        // Tulip MACD lookback
        int lookback = macdIndicator.Start(options);
        double[][] outputs = { 
            new double[tData.Length - lookback], // MACD
            new double[tData.Length - lookback], // Signal
            new double[tData.Length - lookback]  // Histogram
        };

        macdIndicator.Run(inputs, options, outputs);
        var tMacd = outputs[0];
        var tSignal = outputs[1];
        var tHist = outputs[2];

        // Compare last 100 records
        ValidationHelper.VerifyData(qMacd, tMacd, lookback);
        ValidationHelper.VerifyData(qSignal, tSignal, lookback);
        ValidationHelper.VerifyData(qHist, tHist, lookback);

        _output.WriteLine("MACD Streaming validated successfully against Tulip");
    }
}
