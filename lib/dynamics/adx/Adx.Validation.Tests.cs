using Skender.Stock.Indicators;
using TALib;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AdxValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AdxValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void MatchesSkender()
    {
        var adx = new Adx(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adx.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAdx(14).ToList();

        ValidationHelper.VerifyData(results, skenderResults, x => x.Adx);
    }

    [Fact]
    public void MatchesTalib()
    {
        var adx = new Adx(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adx.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.Adx(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.AdxLookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Fact]
    public void MatchesTulip()
    {
        var adx = new Adx(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adx.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[][] inputs = { hData, lData, cData };
        double[] options = { 14 };

        var adxInd = Tulip.Indicators.adx;
        double[][] outputs = { new double[hData.Length - adxInd.Start(options)] };
        adxInd.Run(inputs, options, outputs);
        double[] tulipResults = outputs[0];

        // Tulip initializes differently, so we skip the warmup period to verify convergence
        // We must use the correct offset (lookback) to align the data series
        int offset = adxInd.Start(options);
        ValidationHelper.VerifyData(results, tulipResults, lookback: offset);
    }

    [Fact]
    public void MatchesOoples()
    {
        var adx = new Adx(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adx.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

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
        var adxResults = stockData.CalculateAverageDirectionalIndex(MovingAvgType.WildersSmoothingMethod, 14);
        var ooplesResults = adxResults.OutputValues["Adx"].ToArray();

        // Ooples uses 0-initialization for WWMA, which takes a long time to converge.
        // We verify only the last 100 bars of the 5000-bar dataset.
        // Note: Ooples returns full-length array, so lookback is 0.
        ValidationHelper.VerifyData(results, ooplesResults, lookback: 0, skip: 100, tolerance: ValidationHelper.OoplesTolerance);
    }
}
