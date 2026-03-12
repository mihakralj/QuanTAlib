using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class BbandsValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public BbandsValidationTests(ITestOutputHelper output)
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
    public void Validate_Skender_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (batch TSeries)
            var bbands = new Bbands(period, multiplier);
            var qResult = bbands.Update(_testData.Data);

            // Calculate Skender Bollinger Bands
            var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

            // Compare last 100 records (middle band)
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Sma);
        }
        _output.WriteLine("Bbands Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (streaming)
            var bbands = new Bbands(period, multiplier);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(bbands.Update(item).Value);
            }

            // Calculate Skender Bollinger Bands
            var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

            // Compare last 100 records (middle band)
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Sma);
        }
        _output.WriteLine("Bbands Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (Span API)
            double[] qMiddle = new double[sourceData.Length];
            double[] qUpper = new double[sourceData.Length];
            double[] qLower = new double[sourceData.Length];
            Bbands.Batch(sourceData.AsSpan(), qMiddle.AsSpan(), qUpper.AsSpan(), qLower.AsSpan(), period, multiplier);

            // Calculate Skender Bollinger Bands
            var sResult = _testData.SkenderQuotes.GetBollingerBands(period, multiplier).ToList();

            // Compare last 100 records (middle band)
            ValidationHelper.VerifyData(qMiddle, sResult, (s) => s.Sma);
        }
        _output.WriteLine("Bbands Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] upperOutput = new double[tData.Length];
        double[] middleOutput = new double[tData.Length];
        double[] lowerOutput = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (batch TSeries)
            var bbands = new Bbands(period, multiplier);
            var qResult = bbands.Update(_testData.Data);

            // Calculate TA-Lib Bollinger Bands
            var retCode = Functions.Bbands<double>(
                tData,
                0..^0,
                upperOutput,
                middleOutput,
                lowerOutput,
                out var outRange,
                period,
                multiplier,
                multiplier,
                TALib.Core.MAType.Sma);

            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = Functions.BbandsLookback(period);

            // Compare last 100 records (middle band)
            ValidationHelper.VerifyData(qResult, middleOutput, outRange, lookback);
        }
        _output.WriteLine("Bbands Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] upperOutput = new double[tData.Length];
        double[] middleOutput = new double[tData.Length];
        double[] lowerOutput = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (streaming)
            var bbands = new Bbands(period, multiplier);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(bbands.Update(item).Value);
            }

            // Calculate TA-Lib Bollinger Bands
            var retCode = Functions.Bbands<double>(
                tData,
                0..^0,
                upperOutput,
                middleOutput,
                lowerOutput,
                out var outRange,
                period,
                multiplier,
                multiplier,
                TALib.Core.MAType.Sma);

            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = Functions.BbandsLookback(period);

            // Compare last 100 records (middle band)
            ValidationHelper.VerifyData(qResults, middleOutput, outRange, lookback);
        }
        _output.WriteLine("Bbands Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        // Prepare data
        double[] sourceData = _testData.RawData.ToArray();
        double[] talibUpper = new double[sourceData.Length];
        double[] talibMiddle = new double[sourceData.Length];
        double[] talibLower = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (Span API)
            double[] qMiddle = new double[sourceData.Length];
            double[] qUpper = new double[sourceData.Length];
            double[] qLower = new double[sourceData.Length];
            Bbands.Batch(sourceData.AsSpan(), qMiddle.AsSpan(), qUpper.AsSpan(), qLower.AsSpan(), period, multiplier);

            // Calculate TA-Lib Bollinger Bands
            var retCode = Functions.Bbands<double>(
                sourceData,
                0..^0,
                talibUpper,
                talibMiddle,
                talibLower,
                out var outRange,
                period,
                multiplier,
                multiplier,
                TALib.Core.MAType.Sma);

            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = Functions.BbandsLookback(period);

            // Compare last 100 records (middle band)
            ValidationHelper.VerifyData(qMiddle, talibMiddle, outRange, lookback);
        }
        _output.WriteLine("Bbands Span validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (batch TSeries)
            var bbands = new Bbands(period, multiplier);
            var qResult = bbands.Update(_testData.Data);

            // Calculate Tulip Bollinger Bands
            var bbandsIndicator = Tulip.Indicators.bbands;
            double[][] inputs = { tData };
            double[] options = { period, multiplier };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback], new double[tData.Length - lookback], new double[tData.Length - lookback] };

            bbandsIndicator.Run(inputs, options, outputs);
            var tMiddle = outputs[1]; // Tulip outputs: [lower, middle, upper]

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tMiddle, lookback);
        }
        _output.WriteLine("Bbands Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (streaming)
            var bbands = new Bbands(period, multiplier);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(bbands.Update(item).Value);
            }

            // Calculate Tulip Bollinger Bands
            var bbandsIndicator = Tulip.Indicators.bbands;
            double[][] inputs = { tData };
            double[] options = { period, multiplier };
            int lookback = period - 1;
            double[][] outputs = { new double[tData.Length - lookback], new double[tData.Length - lookback], new double[tData.Length - lookback] };

            bbandsIndicator.Run(inputs, options, outputs);
            var tMiddle = outputs[1]; // Tulip outputs: [lower, middle, upper]

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tMiddle, lookback);
        }
        _output.WriteLine("Bbands Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

        // Prepare data
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib Bbands (Span API)
            double[] qMiddle = new double[sourceData.Length];
            double[] qUpper = new double[sourceData.Length];
            double[] qLower = new double[sourceData.Length];
            Bbands.Batch(sourceData.AsSpan(), qMiddle.AsSpan(), qUpper.AsSpan(), qLower.AsSpan(), period, multiplier);

            // Calculate Tulip Bollinger Bands
            var bbandsIndicator = Tulip.Indicators.bbands;
            double[][] inputs = { sourceData };
            double[] options = { period, multiplier };
            int lookback = period - 1;
            double[][] outputs = { new double[sourceData.Length - lookback], new double[sourceData.Length - lookback], new double[sourceData.Length - lookback] };

            bbandsIndicator.Run(inputs, options, outputs);
            var tMiddle = outputs[1]; // Tulip outputs: [lower, middle, upper]

            // Compare last 100 records
            ValidationHelper.VerifyData(qMiddle, tMiddle, lookback);
        }
        _output.WriteLine("Bbands Span validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };
        double multiplier = 2.0;

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
            // Calculate QuanTAlib Bbands (batch TSeries)
            var bbands = new Bbands(period, multiplier);
            var qResult = bbands.Update(_testData.Data);

            // Calculate Ooples Bollinger Bands
            var stockData = new StockData(ooplesData);
            var ooResult = stockData.CalculateBollingerBands(MovingAvgType.SimpleMovingAverage, period, (int)multiplier);
            var sResult = ooResult.OutputValues["MiddleBand"];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s, 100, ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("Bbands Batch(TSeries) validated successfully against Ooples");
    }
}
