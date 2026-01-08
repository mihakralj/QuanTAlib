using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class MgdiValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public MgdiValidationTests()
    {
        _data = new ValidationTestData(5000);
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        // Calculate Skender MGDI
        // Skender uses Dynamic(14, 0.6) by default if not specified, but let's be explicit
        var skenderResults = _data.SkenderQuotes.GetDynamic(14, 0.6).ToList();

        // Calculate QuanTAlib MGDI
        var mgdi = new Mgdi(14, 0.6);
        var series = _data.Data;
        var quantalibResults = mgdi.Update(series);

        // Compare results
        // Skip warmup period
        for (int i = quantalibResults.Count - 100; i < quantalibResults.Count; i++)
        {
            double skenderValue = skenderResults[i].Dynamic ?? double.NaN;
            double quantalibValue = quantalibResults.Values[i];

            if (!double.IsNaN(skenderValue))
            {
                Assert.Equal(skenderValue, quantalibValue, ValidationHelper.SkenderTolerance);
            }
        }
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        // Calculate Skender MGDI
        var skenderResults = _data.SkenderQuotes.GetDynamic(14, 0.6).ToList();

        // Calculate QuanTAlib MGDI Streaming
        var mgdi = new Mgdi(14, 0.6);
        var streamingResults = new List<double>();

        foreach (var item in _data.Data)
        {
            streamingResults.Add(mgdi.Update(item).Value);
        }

        // Compare results
        for (int i = streamingResults.Count - 100; i < streamingResults.Count; i++)
        {
            double skenderValue = skenderResults[i].Dynamic ?? double.NaN;
            double quantalibValue = streamingResults[i];

            if (!double.IsNaN(skenderValue))
            {
                Assert.Equal(skenderValue, quantalibValue, ValidationHelper.SkenderTolerance);
            }
        }
    }

    [Fact]
    public void Validate_Ooples()
    {
        // Prepare data for Ooples
        var ooplesData = _data.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        // Calculate Ooples MGDI
        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateMcGinleyDynamicIndicator(length: 14);
        var oValues = oResult.OutputValues["Mdi"];

        // Calculate QuanTAlib MGDI
        var mgdi = new Mgdi(14, 0.6);
        var series = _data.Data;
        var quantalibResults = mgdi.Update(series);

        // Compare results
        for (int i = quantalibResults.Count - 100; i < quantalibResults.Count; i++)
        {
            double ooplesValue = oValues[i];
            double quantalibValue = quantalibResults.Values[i];

            // Ooples might use a slightly different formula or precision
            // We'll check for close correlation using relative error
            double diff = Math.Abs(ooplesValue - quantalibValue);
            double relError = diff / ooplesValue;
            Assert.True(relError < ValidationHelper.OoplesTolerance, $"Relative error {relError} too high at index {i}");
        }
    }
}
