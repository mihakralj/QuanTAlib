using QuanTAlib.Tests;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;

namespace QuanTAlib;

public sealed class ApoValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public ApoValidationTests()
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
    public void Validate_Against_TALib_Apo()
    {
        const int fastPeriod = 12;
        int slowPeriod = 26;
        double[] input = _testData.Data.Values.ToArray();
        double[] output = new double[input.Length];

        // TA-Lib APO: double[] inReal, int optInFastPeriod, int optInSlowPeriod, int optInTALib.Core.MAType
        // TALib.Core.MAType 1 = EMA
        var retCode = TALib.Functions.Apo<double>(input, 0..^0, output, out var outRange, fastPeriod, slowPeriod, TALib.Core.MAType.Ema);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        // 1. Batch Mode
        var apo = new Apo(fastPeriod, slowPeriod);
        var result = apo.Update(_testData.Data);
        ValidationHelper.VerifyData(result, output, outRange, lookback: slowPeriod - 1);

        // 2. Streaming Mode
        var apoStream = new Apo(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamResults.Add(apoStream.Update(item).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, outRange, lookback: slowPeriod - 1);

        // 3. Span Mode
        double[] spanOutput = new double[input.Length];
        Apo.Batch(input.AsSpan(), spanOutput.AsSpan(), fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, outRange, lookback: slowPeriod - 1);
    }

    [Fact]
    public void Validate_Against_Tulip_Apo()
    {
        // Tulip APO uses standard EMA initialization (first value), while QuanTAlib uses
        // compensated EMA initialization (zero-based). They converge after sufficient periods.
        // With 5000 bars, the tail (last 100) should match closely.
        int fastPeriod = 12;
        int slowPeriod = 26;
        double[] input = _testData.Data.Values.ToArray();

        var apoIndicator = Tulip.Indicators.apo;
        double[][] inputs = { input };
        double[] options = { fastPeriod, slowPeriod };
        double[][] outputs = { new double[input.Length - 1] }; // Tulip APO starts at 1

        apoIndicator.Run(inputs, options, outputs);
        double[] output = outputs[0];

        // 1. Batch Mode
        var apo = new Apo(fastPeriod, slowPeriod);
        var result = apo.Update(_testData.Data);
        ValidationHelper.VerifyData(result, output, lookback: 1);

        // 2. Streaming Mode
        var apoStream = new Apo(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamResults.Add(apoStream.Update(item).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, lookback: 1);

        // 3. Span Mode
        double[] spanOutput = new double[input.Length];
        Apo.Batch(input.AsSpan(), spanOutput.AsSpan(), fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, lookback: 1);
    }

    [Fact]
    public void Validate_Against_Ooples_Apo()
    {
        int fastPeriod = 12;
        int slowPeriod = 26;

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
        var results = stockData.CalculateAbsolutePriceOscillator(MovingAvgType.ExponentialMovingAverage, fastPeriod, slowPeriod);
        var output = results.OutputValues["Apo"].ToArray();

        // 1. Batch Mode
        var apo = new Apo(fastPeriod, slowPeriod);
        var result = apo.Update(_testData.Data);
        ValidationHelper.VerifyData(result, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);

        // 2. Streaming Mode
        var apoStream = new Apo(fastPeriod, slowPeriod);
        var streamResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            streamResults.Add(apoStream.Update(item).Value);
        }
        ValidationHelper.VerifyData(streamResults, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);

        // 3. Span Mode
        double[] input = _testData.Data.Values.ToArray();
        double[] spanOutput = new double[input.Length];
        Apo.Batch(input.AsSpan(), spanOutput.AsSpan(), fastPeriod, slowPeriod);
        ValidationHelper.VerifyData(spanOutput, output, lookback: 0, tolerance: ValidationHelper.OoplesTolerance);
    }
}
