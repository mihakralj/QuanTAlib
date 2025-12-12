using System;
using System.Collections.Generic;
using System.Linq;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using Xunit;
using Xunit.Abstractions;
using QuanTAlib.Tests;

namespace QuanTAlib.Tests;

public class VidyaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public VidyaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void ValidateAgainstReference()
    {
        // Note: Tulip's VIDYA implementation uses Standard Deviation ratio (1992 version),
        // while QuanTAlib uses Chande Momentum Oscillator (1994 version).
        // Therefore, we cannot validate against Tulip.
        // We validate against a simple, readable reference implementation of the CMO-based VIDYA.
        
        var period = 14;
        
        // QuanTAlib
        var vidya = new Vidya(period);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(vidya.Update(item).Value);
        }
        
        // Reference Implementation
        var refResults = CalculateVidyaReference(_testData.Data, period);
        
        // Compare
        ValidationHelper.VerifyData(qResults, refResults, x => x);
        
        _output.WriteLine("VIDYA validated successfully against reference implementation");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Map to Ooples StockData
        var ooplesData = new StockData(
            _testData.SkenderQuotes.Select(x => (double)x.Open),
            _testData.SkenderQuotes.Select(x => (double)x.High),
            _testData.SkenderQuotes.Select(x => (double)x.Low),
            _testData.SkenderQuotes.Select(x => (double)x.Close),
            _testData.SkenderQuotes.Select(x => (double)x.Volume),
            _testData.SkenderQuotes.Select(x => x.Date)
        );

        foreach (var period in periods)
        {
            // Calculate QuanTAlib VIDYA (batch TSeries)
            var vidya = new global::QuanTAlib.Vidya(period);
            var qResult = vidya.Update(_testData.Data);

            // Calculate Ooples VIDYA
            var oResult = ooplesData.CalculateVariableIndexDynamicAverage(MovingAvgType.ExponentialMovingAverage, period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, oResult.OutputValues["Vidya"], x => x, tolerance: 1e-4);
        }
        _output.WriteLine("VIDYA Batch(TSeries) validated successfully against OoplesFinance");
    }
    
    private static List<double> CalculateVidyaReference(TSeries data, int period)
    {
        var results = new List<double>();
        var prices = data.Select(x => x.Value).ToList();
        double alpha = 2.0 / (period + 1);
        
        double prevVidya = 0;
        
        for (int i = 0; i < prices.Count; i++)
        {
            if (i == 0)
            {
                results.Add(prices[i]);
                prevVidya = prices[i];
                continue;
            }
            
            double sumUp = 0;
            double sumDown = 0;
            
            var changes = new List<double>();
            for (int j = 1; j <= i; j++)
            {
                changes.Add(prices[j] - prices[j-1]);
            }
            
            var recentChanges = changes.TakeLast(period).ToList();
            
            sumUp = recentChanges.Where(x => x > 0).Sum();
            sumDown = recentChanges.Where(x => x < 0).Select(x => -x).Sum();
            
            double sum = sumUp + sumDown;
            double vi = 0;
            if (sum > 0)
            {
                vi = Math.Abs(sumUp - sumDown) / sum;
            }
            
            double dynamicAlpha = alpha * vi;
            double currentVidya = dynamicAlpha * prices[i] + (1 - dynamicAlpha) * prevVidya;
            
            results.Add(currentVidya);
            prevVidya = currentVidya;
        }
        
        return results;
    }
}
