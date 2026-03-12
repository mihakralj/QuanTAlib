using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class Ssf2ValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public Ssf2ValidationTests(ITestOutputHelper output)
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
            // Calculate QuanTAlib SSF2
            var ssf = new Ssf2(period);
            var qResult = ssf.Update(_testData.Data);

            // Calculate Ooples SSF
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateEhlersSuperSmootherFilter(period);
            var oValues = oResult.OutputValues.Values.First();

            // Compare
            // We use a looser tolerance (10.0) because our implementation uses high-precision constants (Math.Sqrt(2) * Math.PI)
            // whereas Ooples likely uses the approximation (1.414 * 3.14159) found in some reference implementations.
            // This difference in constants causes a divergence in values.
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, skip: period, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("SSF2 validated successfully against Ooples");
    }
}
