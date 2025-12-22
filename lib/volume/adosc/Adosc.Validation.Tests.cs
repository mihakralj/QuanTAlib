using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuanTAlib.Tests;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;

namespace QuanTAlib;

public class AdoscValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public AdoscValidationTests()
    {
        _testData = new ValidationTestData(); // Default 5000 bars
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
    public void Validate_Against_TALib_Adosc()
    {
        int fastPeriod = 3;
        int slowPeriod = 10;
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();
        double[] output = new double[close.Length];

        var retCode = TALib.Functions.AdOsc(high, low, close, volume, 0..^0, output, out var outRange, fastPeriod, slowPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData(result, output, outRange, lookback: slowPeriod - 1);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, outRange, lookback: slowPeriod - 1);

        // 3. Span Mode
        double[] spanOutput = new double[close.Length];
        Adosc.Calculate(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, outRange, lookback: slowPeriod - 1);
    }

    [Fact(Skip = "Tulip ADOSC implementation diverges significantly from TA-Lib and Skender")]
    public void Validate_Against_Tulip_Adosc()
    {
        int fastPeriod = 3;
        int slowPeriod = 10;
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();

        var adoscIndicator = Tulip.Indicators.adosc;
        double[][] inputs = { high, low, close, volume };
        double[] options = { fastPeriod, slowPeriod };
        double[][] outputs = { new double[close.Length - 1] }; // Tulip starts at 1? Need to check

        adoscIndicator.Run(inputs, options, outputs);
        double[] output = outputs[0];

        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData(result, output, lookback: 1);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, lookback: 1);

        // 3. Span Mode
        double[] spanOutput = new double[close.Length];
        Adosc.Calculate(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, lookback: 1);
    }

    [Fact]
    public void Validate_Against_Skender_ChaikinOsc()
    {
        int fastPeriod = 3;
        int slowPeriod = 10;

        var skenderResults = _testData.SkenderQuotes.GetChaikinOsc(fastPeriod, slowPeriod).ToList();
        
        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData<ChaikinOscResult>(result, skenderResults, (x) => x.Oscillator);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData<ChaikinOscResult>(streamResults, skenderResults, (x) => x.Oscillator);

        // 3. Span Mode
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();
        double[] spanOutput = new double[close.Length];
        Adosc.Calculate(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData<ChaikinOscResult>(spanOutput, skenderResults, (x) => x.Oscillator);
    }

    [Fact]
    public void Validate_Against_Ooples_ChaikinOscillator()
    {
        int fastPeriod = 3;
        int slowPeriod = 10;

        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var results = stockData.CalculateChaikinOscillator(MovingAvgType.ExponentialMovingAverage, fastPeriod, slowPeriod);
        var output = results.OutputValues["ChaikinOsc"].ToArray();

        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData(result, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);

        // 3. Span Mode
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();
        double[] spanOutput = new double[close.Length];
        Adosc.Calculate(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);
    }
}
