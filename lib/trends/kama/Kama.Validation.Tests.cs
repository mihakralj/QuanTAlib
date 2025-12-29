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

public sealed class KamaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public KamaValidationTests(ITestOutputHelper output)
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
        int[] periods = { 10, 14, 20 };
        int fastPeriod = 2;
        int slowPeriod = 30;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (batch TSeries)
            var kama = new global::QuanTAlib.Kama(period, fastPeriod, slowPeriod);
            var qResult = kama.Update(_testData.Data);

            // Calculate Skender KAMA
            var sResult = _testData.SkenderQuotes.GetKama(period, fastPeriod, slowPeriod).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, x => x.Kama);
        }
        _output.WriteLine("KAMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 10, 14, 20 };
        int fastPeriod = 2;
        int slowPeriod = 30;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (streaming)
            var kama = new global::QuanTAlib.Kama(period, fastPeriod, slowPeriod);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(kama.Update(item).Value);
            }

            // Calculate Skender KAMA
            var sResult = _testData.SkenderQuotes.GetKama(period, fastPeriod, slowPeriod).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, x => x.Kama);
        }
        _output.WriteLine("KAMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 10, 14, 20 };
        int fastPeriod = 2;
        int slowPeriod = 30;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Kama.Calculate(_testData.RawData.Span, qOutput.AsSpan(), period, fastPeriod, slowPeriod);

            // Calculate Skender KAMA
            var sResult = _testData.SkenderQuotes.GetKama(period, fastPeriod, slowPeriod).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, x => x.Kama);
        }
        _output.WriteLine("KAMA Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 10, 14, 20 };
        // TA-Lib KAMA uses default fast=2, slow=30 and doesn't expose them in the standard API

        // Prepare data for TA-Lib (double[])
        double[] cData = _testData.Data.Select(x => x.Value).ToArray();
        double[] output = new double[cData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (batch TSeries)
            // Use default fast=2, slow=30 to match TA-Lib
            var kama = new global::QuanTAlib.Kama(period);
            var qResult = kama.Update(_testData.Data);

            // Calculate TA-Lib KAMA
            var retCode = TALib.Functions.Kama(cData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.KamaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("KAMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 10, 14, 20 };

        // Prepare data for TA-Lib (double[])
        double[] cData = _testData.Data.Select(x => x.Value).ToArray();
        double[] output = new double[cData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (streaming)
            var kama = new global::QuanTAlib.Kama(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(kama.Update(item).Value);
            }

            // Calculate TA-Lib KAMA
            var retCode = TALib.Functions.Kama(cData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.KamaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("KAMA Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 10, 14, 20 };

        // Prepare data for Tulip (double[])
        double[] cData = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (batch TSeries)
            var kama = new global::QuanTAlib.Kama(period);
            var qResult = kama.Update(_testData.Data);

            // Calculate Tulip KAMA
            var kamaIndicator = Tulip.Indicators.kama;
            double[][] inputs = { cData };
            double[] options = { period };
            
            // Tulip KAMA lookback
            int lookback = kamaIndicator.Start(options);
            double[][] outputs = { new double[cData.Length - lookback] };

            kamaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("KAMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 10, 14, 20 };

        // Prepare data for Tulip (double[])
        double[] cData = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib KAMA (streaming)
            var kama = new global::QuanTAlib.Kama(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(kama.Update(item).Value);
            }

            // Calculate Tulip KAMA
            var kamaIndicator = Tulip.Indicators.kama;
            double[][] inputs = { cData };
            double[] options = { period };
            
            // Tulip KAMA lookback
            int lookback = kamaIndicator.Start(options);
            double[][] outputs = { new double[cData.Length - lookback] };

            kamaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("KAMA Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        int[] periods = { 10, 14, 20 };
        int fastPeriod = 2;
        int slowPeriod = 30;

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
            // Calculate QuanTAlib KAMA
            var kama = new global::QuanTAlib.Kama(period, fastPeriod, slowPeriod);
            var qResult = kama.Update(_testData.Data);

            // Calculate Ooples KAMA
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateKaufmanAdaptiveMovingAverage(length: period, fastLength: fastPeriod, slowLength: slowPeriod);
            var oValues = oResult.OutputValues["Kama"];

            // Compare
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("KAMA validated successfully against Ooples");
    }
}
