using System;
using System.Collections.Generic;
using System.Linq;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit;

namespace QuanTAlib.Tests;

public class RmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public RmaValidationTests()
    {
        _testData = new ValidationTestData(count: 1000, seed: 123);
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
    public void Rma_Matches_Skender_Smma()
    {
        // Arrange
        int period = 14;
        
        // QuanTAlib RMA
        var rma = new Rma(period);
        var quantalibResults = new TSeries();
        foreach (var item in _testData.Data)
        {
            quantalibResults.Add(rma.Update(item));
        }

        // Skender SMMA
        var skenderResults = _testData.SkenderQuotes.GetSmma(period).ToList();

        // Assert
        // Skip warmup period for comparison
        // Skender uses SMA initialization, QuanTAlib uses zero-lag compensator
        // They should converge after some periods
        int skip = period * 30;

        int itemsToVerify = _testData.Data.Count - skip;
        ValidationHelper.VerifyData(quantalibResults, skenderResults, (s) => s.Smma, skip: itemsToVerify, tolerance: ValidationHelper.OoplesTolerance);
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        // Arrange
        int period = 14;

        // QuanTAlib RMA
        var rma = new Rma(period);
        var qResult = rma.Update(_testData.Data);

        // Ooples WWMA (Welles Wilder Moving Average)
        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Close = (double)q.Close,
            High = (double)q.High,
            Low = (double)q.Low,
            Open = (double)q.Open,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateWellesWilderMovingAverage(length: period);
        var oValues = oResult.OutputValues["Wwma"];

        // Assert
        // Skip warmup period for comparison
        int skip = period * 30;
        int itemsToVerify = _testData.Data.Count - skip;
        
        ValidationHelper.VerifyData(qResult, oValues, (s) => s, skip: itemsToVerify, tolerance: ValidationHelper.OoplesTolerance);
    }
}
