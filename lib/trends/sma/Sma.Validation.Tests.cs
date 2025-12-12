using System;
using System.Collections.Generic;
using System.Linq;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class SmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public SmaValidationTests(ITestOutputHelper output)
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
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (batch TSeries)
            var sma = new global::QuanTAlib.Sma(period);
            var qResult = sma.Update(_testData.Data);

            // Calculate Skender SMA
            var sResult = _testData.SkenderQuotes.GetSma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Sma);
        }
        _output.WriteLine("SMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (streaming)
            var sma = new global::QuanTAlib.Sma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(sma.Update(item).Value);
            }

            // Calculate Skender SMA
            var sResult = _testData.SkenderQuotes.GetSma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Sma);
        }
        _output.WriteLine("SMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Sma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Skender SMA
            var sResult = _testData.SkenderQuotes.GetSma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Sma);
        }
        _output.WriteLine("SMA Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (batch TSeries)
            var sma = new global::QuanTAlib.Sma(period);
            var qResult = sma.Update(_testData.Data);

            // Calculate TA-Lib SMA
            var retCode = TALib.Functions.Sma<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.SmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("SMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (streaming)
            var sma = new global::QuanTAlib.Sma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(sma.Update(item).Value);
            }

            // Calculate TA-Lib SMA
            var retCode = TALib.Functions.Sma<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.SmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("SMA Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] sourceData = _testData.RawData.ToArray();
        double[] talibOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Sma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib SMA
            var retCode = TALib.Functions.Sma<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.SmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("SMA Span validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (batch TSeries)
            var sma = new global::QuanTAlib.Sma(period);
            var qResult = sma.Update(_testData.Data);

            // Calculate Tulip SMA
            var smaIndicator = Tulip.Indicators.sma;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            smaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("SMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (streaming)
            var sma = new global::QuanTAlib.Sma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(sma.Update(item).Value);
            }

            // Calculate Tulip SMA
            var smaIndicator = Tulip.Indicators.sma;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback] };

            smaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback);
        }
        _output.WriteLine("SMA Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib SMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Sma.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Tulip SMA
            var smaIndicator = Tulip.Indicators.sma;
            double[][] inputs = { sourceData };
            double[] options = { period };
            int lookback = period - 1;
            double[][] outputs = { new double[sourceData.Length - lookback] };

            smaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, tResult, lookback);
        }
        _output.WriteLine("SMA Span validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Ooples (List<TickerData>)
        // Ooples requires TickerData which has Close, High, Low, Open, Volume, Date
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
            // Calculate QuanTAlib SMA (batch TSeries)
            var sma = new global::QuanTAlib.Sma(period);
            var qResult = sma.Update(_testData.Data);

            // Calculate Ooples SMA
            var stockData = new StockData(ooplesData);
            var sResult = Calculations.CalculateSimpleMovingAverage(stockData, period).OutputValues.Values.First();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s, tolerance: 1e-4);
        }
        _output.WriteLine("SMA Batch(TSeries) validated successfully against Ooples");
    }
}
