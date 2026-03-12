using Skender.Stock.Indicators;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public class LsmaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public LsmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib LSMA (batch TSeries)
            var lsma = new global::QuanTAlib.Lsma(period);
            var qResult = lsma.Update(_testData.Data);

            // Calculate Skender EPMA (Endpoint Moving Average = LSMA)
            var sResult = _testData.SkenderQuotes.GetEpma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, x => x.Epma, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("LSMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib LSMA (streaming)
            var lsma = new global::QuanTAlib.Lsma(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(lsma.Update(item).Value);
            }

            // Calculate Skender EPMA
            var sResult = _testData.SkenderQuotes.GetEpma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, x => x.Epma, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("LSMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib LSMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Lsma.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate Skender EPMA
            var sResult = _testData.SkenderQuotes.GetEpma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, x => x.Epma, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("LSMA Span validated successfully against Skender");
    }

    [Fact]
    public void Lsma_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateAdaptiveLeastSquares();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
