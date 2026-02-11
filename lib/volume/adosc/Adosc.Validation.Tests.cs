using QuanTAlib.Tests;
using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;

namespace QuanTAlib;

public sealed class AdoscValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public AdoscValidationTests()
    {
        _testData = new ValidationTestData(); // Default 5000 bars
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
    public void Validate_Against_TALib_Adosc()
    {
        const int fastPeriod = 3;
        int slowPeriod = 10;
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();
        double[] output = new double[close.Length];

        var retCode = TALib.Functions.AdOsc(high, low, close, volume, 0..^0, output, out var outRange, fastPeriod, slowPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData(result, output, outRange, lookback: slowPeriod - 1, tolerance: ValidationHelper.TalibTolerance);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, outRange, lookback: slowPeriod - 1, tolerance: ValidationHelper.TalibTolerance);

        // 3. Span Mode
        double[] spanOutput = new double[close.Length];
        Adosc.Batch(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, outRange, lookback: slowPeriod - 1, tolerance: ValidationHelper.TalibTolerance);
    }

    [Fact]
    public void Validate_Against_Tulip_Adosc()
    {
        int fastPeriod = 3;
        int slowPeriod = 10;
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();

        var adoscIndicator = Tulip.Indicators.adosc;
        double[][] inputs = { high, low, close, volume };
        double[] options = { fastPeriod, slowPeriod };
        int start = adoscIndicator.Start(options);
        double[][] outputs = { new double[close.Length - start] };

        adoscIndicator.Run(inputs, options, outputs);
        double[] output = outputs[0];

        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData(result, output, lookback: start, tolerance: ValidationHelper.TulipTolerance);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, lookback: start, tolerance: ValidationHelper.TulipTolerance);

        // 3. Span Mode
        double[] spanOutput = new double[close.Length];
        Adosc.Batch(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, lookback: start, tolerance: ValidationHelper.TulipTolerance);
    }

    [Fact]
    public void Validate_Against_Skender_ChaikinOsc()
    {
        int fastPeriod = 3;
        int slowPeriod = 10;

        var skenderResults = _testData.SkenderQuotes.GetChaikinOsc(fastPeriod, slowPeriod).ToList();

        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData<ChaikinOscResult>(result, skenderResults, (x) => x.Oscillator, tolerance: ValidationHelper.SkenderTolerance);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData<ChaikinOscResult>(streamResults, skenderResults, (x) => x.Oscillator, tolerance: ValidationHelper.SkenderTolerance);

        // 3. Span Mode
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();
        double[] spanOutput = new double[close.Length];
        Adosc.Batch(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData<ChaikinOscResult>(spanOutput, skenderResults, (x) => x.Oscillator, tolerance: ValidationHelper.SkenderTolerance);
    }

    [Fact]
    public void Validate_Against_Ooples_ChaikinOscillator()
    {
        int fastPeriod = 3;
        int slowPeriod = 10;

        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var results = stockData.CalculateChaikinOscillator(MovingAvgType.ExponentialMovingAverage, fastPeriod, slowPeriod);
        var output = results.OutputValues["ChaikinOsc"].ToArray();

        // 1. Batch Mode
        var adosc = new Adosc(fastPeriod, slowPeriod);
        var result = adosc.Update(_testData.Bars);
        ValidationHelper.VerifyData(result, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);

        // 2. Streaming Mode
        var adoscStream = new Adosc(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamResults.Add(adoscStream.Update(bar).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);

        // 3. Span Mode
        double[] high = _testData.Bars.High.Values.ToArray();
        double[] low = _testData.Bars.Low.Values.ToArray();
        double[] close = _testData.Bars.Close.Values.ToArray();
        double[] volume = _testData.Bars.Volume.Values.ToArray();
        double[] spanOutput = new double[close.Length];
        Adosc.Batch(high, low, close, volume, spanOutput, fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);
    }
}
