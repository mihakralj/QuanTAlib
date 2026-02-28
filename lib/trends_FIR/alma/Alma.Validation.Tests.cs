using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class AlmaValidationTests : IDisposable
{
    // Note: ALMA is not available in TA-Lib or Tulip,
    // validation is limited to Skender.Stock.Indicators and OoplesFinance.StockIndicators.

    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AlmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData(count: 10000, seed: 42);
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
        int[] periods = { 9, 14, 20, 50 };
        const double offset = 0.85;
        double sigma = 6.0;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ALMA (batch TSeries)
            var alma = new global::QuanTAlib.Alma(period, offset, sigma);
            var qResult = alma.Update(_testData.Data);

            // Calculate Skender ALMA
            var sResult = _testData.SkenderQuotes.GetAlma(period, offset, sigma).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Alma);
        }
        _output.WriteLine("ALMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 9, 14, 20, 50 };
        double offset = 0.85;
        double sigma = 6.0;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ALMA (streaming)
            var alma = new global::QuanTAlib.Alma(period, offset, sigma);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(alma.Update(item).Value);
            }

            // Calculate Skender ALMA
            var sResult = _testData.SkenderQuotes.GetAlma(period, offset, sigma).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Alma);
        }
        _output.WriteLine("ALMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 9, 14, 20, 50 };
        double offset = 0.85;
        double sigma = 6.0;

        // Prepare data for Span API
        ReadOnlySpan<double> sourceData = _testData.RawData.Span;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ALMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Alma.Batch(sourceData, qOutput.AsSpan(), period, offset, sigma);

            // Calculate Skender ALMA
            var sResult = _testData.SkenderQuotes.GetAlma(period, offset, sigma).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Alma);
        }
        _output.WriteLine("ALMA Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        int[] periods = { 9, 14, 20, 50 };
        double offset = 0.85;
        double sigma = 6.0;

        // Prepare data for Ooples
        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        foreach (var period in periods)
        {
            // 1. Calculate Ooples ALMA
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateArnaudLegouxMovingAverage(period, offset, (int)sigma);
            var oAlma = oResult.OutputValues["Alma"];

            // 2. Calculate QuanTAlib ALMA
            var alma = new global::QuanTAlib.Alma(period, offset, sigma);
            var qResult = alma.Update(_testData.Data);

            // 3. Verify
            ValidationHelper.VerifyData(qResult, oAlma, x => x, skip: 100, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("ALMA Batch validated successfully against Ooples");
    }
}
