using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class T3ValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public T3ValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3
            var t3 = new global::QuanTAlib.T3(period, vFactor);
            var qResult = t3.Update(_testData.Data);

            // Calculate Skender T3
            var sResult = _testData.SkenderQuotes.GetT3(period, vFactor).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, x => x.T3);
        }
        _output.WriteLine("T3 Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        // Prepare data for TA-Lib
        double[] output = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3
            var t3 = new global::QuanTAlib.T3(period, vFactor);
            var qResult = t3.Update(_testData.Data);

            // Calculate TA-Lib T3
            var retCode = TALib.Functions.T3<double>(_testData.RawData.Span, 0..^0, output, out var outRange, period, vFactor);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.T3Lookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("T3 Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        // Prepare data for TA-Lib
        double[] output = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3 (streaming)
            var t3 = new global::QuanTAlib.T3(period, vFactor);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(t3.Update(item).Value);
            }

            // Calculate TA-Lib T3
            var retCode = TALib.Functions.T3<double>(_testData.RawData.Span, 0..^0, output, out var outRange, period, vFactor);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.T3Lookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("T3 Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

        // Prepare data
        double[] talibOutput = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib T3 (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.T3.Batch(_testData.RawData.Span, qOutput.AsSpan(), period, vFactor);

            // Calculate TA-Lib T3
            var retCode = TALib.Functions.T3<double>(_testData.RawData.Span, 0..^0, talibOutput, out var outRange, period, vFactor);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.T3Lookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("T3 Span validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        int[] periods = { 5, 10, 20 };
        double vFactor = 0.7;

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
            // Calculate QuanTAlib T3
            var t3 = new global::QuanTAlib.T3(period, vFactor);
            var qResult = t3.Update(_testData.Data);

            // Calculate Ooples T3
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateTillsonT3MovingAverage(length: period, vFactor: vFactor);
            var oValues = oResult.OutputValues["T3"];

            // Compare
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("T3 validated successfully against Ooples");
    }
}
