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

public sealed class AtrValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AtrValidationTests(ITestOutputHelper output)
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
        int[] periods = { 14 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATR (batch TSeries)
            var atr = new global::QuanTAlib.Atr(period);
            var qResult = atr.Update(_testData.Bars);

            // Calculate Skender ATR
            var sResult = _testData.SkenderQuotes.GetAtr(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Atr, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("ATR Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 14 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATR (streaming)
            var atr = new global::QuanTAlib.Atr(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(atr.Update(item).Value);
            }

            // Calculate Skender ATR
            var sResult = _testData.SkenderQuotes.GetAtr(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Atr, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("ATR Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 14 };

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] output = new double[hData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATR (batch TSeries)
            var atr = new global::QuanTAlib.Atr(period);
            var qResult = atr.Update(_testData.Bars);

            // Calculate TA-Lib ATR
            var retCode = TALib.Functions.Atr(hData, lData, cData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.AtrLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("ATR Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 14 };

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] output = new double[hData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATR (streaming)
            var atr = new global::QuanTAlib.Atr(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(atr.Update(item).Value);
            }

            // Calculate TA-Lib ATR
            var retCode = TALib.Functions.Atr(hData, lData, cData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.AtrLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("ATR Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 14 };

        // Prepare data for Tulip (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATR (batch TSeries)
            var atr = new global::QuanTAlib.Atr(period);
            var qResult = atr.Update(_testData.Bars);

            // Calculate Tulip ATR
            var atrIndicator = Tulip.Indicators.atr;
            double[][] inputs = { hData, lData, cData };
            double[] options = { period };
            
            // Tulip ATR lookback
            int lookback = atrIndicator.Start(options);
            double[][] outputs = { new double[hData.Length - lookback] };

            atrIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("ATR Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 14 };

        // Prepare data for Tulip (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATR (streaming)
            var atr = new global::QuanTAlib.Atr(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(atr.Update(item).Value);
            }

            // Calculate Tulip ATR
            var atrIndicator = Tulip.Indicators.atr;
            double[][] inputs = { hData, lData, cData };
            double[] options = { period };
            
            // Tulip ATR lookback
            int lookback = atrIndicator.Start(options);
            double[][] outputs = { new double[hData.Length - lookback] };

            atrIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("ATR Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        int[] periods = { 14 };

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
            // Calculate QuanTAlib ATR (batch TSeries)
            var atr = new global::QuanTAlib.Atr(period);
            var qResult = atr.Update(_testData.Bars);

            // Calculate Ooples ATR
            var stockData = new StockData(ooplesData);
            var sResult = Calculations.CalculateAverageTrueRange(stockData, MovingAvgType.WildersSmoothingMethod, period).OutputValues.Values.First();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s, 100, ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("ATR Batch(TSeries) validated successfully against Ooples");
    }
}
