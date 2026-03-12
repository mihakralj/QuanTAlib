using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public class PvtValidationTests
{
    private readonly ValidationTestData _data;

    public PvtValidationTests()
    {
        _data = new ValidationTestData();
    }

    [Fact]
    public void Pvt_Matches_Ooples()
    {
        // Ooples PVT
        var ooplesData = _data.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculatePriceVolumeTrend();
        var oValues = oResult.OutputValues["Pvt"];

        // QuanTAlib
        var pvt = new Pvt();
        var quantalibValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            quantalibValues.Add(pvt.Update(bar).Value);
        }

        // Verify both produce finite values (implementation may differ in cumulative handling)
        Assert.True(quantalibValues.All(v => double.IsFinite(v)), "QuanTAlib PVT should produce finite values");
        Assert.True(oValues.All(v => double.IsFinite(v)), "Ooples PVT should produce finite values");

        // Note: Ooples and QuanTAlib may diverge over long series due to different
        // cumulative calculation approaches or NaN handling.
        ValidationHelper.VerifyData(quantalibValues.ToArray(), oValues.ToArray(), 0, 100, ValidationHelper.OoplesTolerance);
    }

    [Fact]
    public void Pvt_Streaming_Matches_Batch()
    {
        // Streaming
        var pvt = new Pvt();
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(pvt.Update(bar).Value);
        }

        // Batch
        var batchResult = Pvt.Batch(_data.Bars);
        var batchValues = batchResult.Values.ToArray();

        ValidationHelper.VerifyData(streamingValues.ToArray(), batchValues, 0, 100, 1e-9);
    }

    [Fact]
    public void Pvt_Span_Matches_Streaming()
    {
        // Streaming
        var pvt = new Pvt();
        var streamingValues = new List<double>();
        foreach (var bar in _data.Bars)
        {
            streamingValues.Add(pvt.Update(bar).Value);
        }

        // Span
        var close = _data.Bars.Close.Values.ToArray();
        var volume = _data.Bars.Volume.Values.ToArray();
        var spanOutput = new double[close.Length];

        Pvt.Batch(close, volume, spanOutput);

        ValidationHelper.VerifyData(streamingValues.ToArray(), spanOutput, 0, 100, 1e-9);
    }

    [Fact]
    public void Pvt_KnownValues_MatchExpected()
    {
        // Test with known values
        // Bar 0: close=100, volume=1000 -> PVT = 0 (first bar)
        // Bar 1: close=110, volume=2000 -> PVT = 2000 * (10/100) = 200
        // Bar 2: close=105, volume=1500 -> PVT = 200 + 1500 * (-5/110) = 200 - 68.18... = 131.818...
        // Bar 3: close=115, volume=2500 -> PVT = 131.818 + 2500 * (10/105) = 131.818 + 238.095... = 369.914...

        var pvt = new Pvt();
        var time = DateTime.UtcNow;

        var result0 = pvt.Update(new TBar(time, 100, 105, 95, 100, 1000));
        Assert.Equal(0.0, result0.Value, 1e-10);

        var result1 = pvt.Update(new TBar(time.AddMinutes(1), 100, 115, 100, 110, 2000));
        Assert.Equal(200.0, result1.Value, 1e-10);

        var result2 = pvt.Update(new TBar(time.AddMinutes(2), 110, 112, 103, 105, 1500));
        double expected2 = 200 + (1500 * (-5.0 / 110.0));  // = 131.8181818...
        Assert.Equal(expected2, result2.Value, 1e-10);

        var result3 = pvt.Update(new TBar(time.AddMinutes(3), 105, 118, 105, 115, 2500));
        double expected3 = expected2 + (2500 * (10.0 / 105.0));  // = 369.9134...
        Assert.Equal(expected3, result3.Value, 1e-10);
    }
}
