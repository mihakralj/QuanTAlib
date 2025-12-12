using System;
using System.Collections.Generic;
using System.Linq;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class KamaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public KamaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
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
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: 5e-4);
        }
        _output.WriteLine("KAMA validated successfully against Ooples");
    }
}
