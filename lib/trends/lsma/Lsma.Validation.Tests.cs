using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit.Abstractions;

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
            global::QuanTAlib.Lsma.Calculate(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate Skender EPMA
            var sResult = _testData.SkenderQuotes.GetEpma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, x => x.Epma, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("LSMA Span validated successfully against Skender");
    }
}
