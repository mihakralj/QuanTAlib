using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class PwmaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public PwmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

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
            // Calculate QuanTAlib PWMA
            var pwma = new global::QuanTAlib.Pwma(period);
            var qResult = pwma.Update(_testData.Data);

            // Calculate Ooples PWMA
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateParabolicWeightedMovingAverage(length: period);
            var oValues = oResult.OutputValues["Pwma"];

            // Compare
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: 2e-4);
        }
        _output.WriteLine("PWMA validated successfully against Ooples");
    }
}
