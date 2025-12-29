using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using QuanTAlib;
using TALib;

namespace QuanTAlib.Tests;

public sealed class HtitValidationTests : IDisposable
{
    private readonly ValidationTestData _data;
    private bool _disposed;

    public HtitValidationTests()
    {
        _data = new ValidationTestData(5000);
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
            _data?.Dispose();
        }
    }

    [Fact]
    public void Validate_TaLib()
    {
        // Calculate TA-Lib HTIT
        var input = _data.RawData.Span;
        var output = new double[input.Length];
        var retCode = TALib.Functions.HtTrendline(input, 0..^0, output, out var outRange);

        Assert.Equal(Core.RetCode.Success, retCode);

        // Calculate QuanTAlib HTIT
        var htit = new Htit();
        var quantalibResults = htit.Update(_data.Data);

        // Compare results
        // TA-Lib HT_TRENDLINE has a lookback of 63
        for (int i = quantalibResults.Count - 100; i < quantalibResults.Count; i++)
        {
            if (i >= outRange.Start.Value)
            {
                double talibValue = output[i - outRange.Start.Value];
                double quantalibValue = quantalibResults.Values[i];
                Assert.Equal(talibValue, quantalibValue, ValidationHelper.TalibTolerance);
            }
        }
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        // Calculate Skender HTIT
        var skenderResults = _data.SkenderQuotes.GetHtTrendline().ToList();

        // Calculate QuanTAlib HTIT
        var htit = new Htit();
        var series = _data.Data;
        var quantalibResults = htit.Update(series);

        // Compare results
        // Skip warmup period (Skender needs 100 periods for convergence, but we can check after 50)
        for (int i = quantalibResults.Count - 100; i < quantalibResults.Count; i++)
        {
            double skenderValue = skenderResults[i].Trendline ?? double.NaN;
            double quantalibValue = quantalibResults.Values[i];

            if (!double.IsNaN(skenderValue))
            {
                // Skender implementation differs slightly (~0.32%) from TA-Lib/QuanTAlib.
                // QuanTAlib matches TA-Lib (reference) with 1e-6 precision.
                // The divergence in Skender is likely due to implementation details or smoothing differences.
                double diff = Math.Abs(skenderValue - quantalibValue);
                double relError = diff / skenderValue;
                Assert.True(relError < ValidationHelper.RelativeTolerance, $"Relative error {relError} too high at index {i}");
            }
        }
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        // Calculate Skender HTIT
        var skenderResults = _data.SkenderQuotes.GetHtTrendline().ToList();

        // Calculate QuanTAlib HTIT Streaming
        var htit = new Htit();
        var streamingResults = new List<double>();

        foreach (var item in _data.Data)
        {
            streamingResults.Add(htit.Update(item).Value);
        }

        // Compare results
        for (int i = streamingResults.Count - 100; i < streamingResults.Count; i++)
        {
            double skenderValue = skenderResults[i].Trendline ?? double.NaN;
            double quantalibValue = streamingResults[i];

            if (!double.IsNaN(skenderValue))
            {
                // Skender implementation differs slightly (~0.32%) from TA-Lib/QuanTAlib
                double diff = Math.Abs(skenderValue - quantalibValue);
                double relError = diff / skenderValue;
                Assert.True(relError < ValidationHelper.RelativeTolerance, $"Relative error {relError} too high at index {i}");
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

        // Calculate Ooples HTIT
        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateEhlersInstantaneousTrendlineV1();
        var oValues = oResult.OutputValues["Eit"];

        // Calculate QuanTAlib HTIT
        var htit = new Htit();
        var quantalibResults = htit.Update(_data.Data);

        // Compare results
        // Ooples might have different warmup or calculation details
        // We'll check for correlation or close values after warmup
        for (int i = quantalibResults.Count - 100; i < quantalibResults.Count; i++)
        {
            double ooplesValue = oValues[i];
            double quantalibValue = quantalibResults.Values[i];

            // Ooples V1 differs slightly (~0.25%) from TA-Lib/QuanTAlib.
            // QuanTAlib matches TA-Lib (reference) with 1e-6 precision.
            double diff = Math.Abs(ooplesValue - quantalibValue);
            double relError = diff / ooplesValue;
            Assert.True(relError < ValidationHelper.RelativeTolerance, $"Relative error {relError} too high at index {i}");
        }
    }
}
