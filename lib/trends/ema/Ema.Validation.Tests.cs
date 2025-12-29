using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Tulip;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class EmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public EmaValidationTests(ITestOutputHelper output)
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

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (batch TSeries)
            var ema = new global::QuanTAlib.Ema(period);
            var qResult = ema.Update(_testData.Data);

            // Calculate Skender EMA
            var sResult = _testData.SkenderQuotes.GetEma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Ema);
        }
        _output.WriteLine("EMA Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (streaming)
            var ema = new global::QuanTAlib.Ema(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(ema.Update(item).Value);
            }

            // Calculate Skender EMA
            var sResult = _testData.SkenderQuotes.GetEma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Ema);
        }
        _output.WriteLine("EMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Ema.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Skender EMA
            var sResult = _testData.SkenderQuotes.GetEma(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Ema);
        }
        _output.WriteLine("EMA Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (batch TSeries)
            var ema = new global::QuanTAlib.Ema(period);
            var qResult = ema.Update(_testData.Data);

            // Calculate TA-Lib EMA
            var retCode = TALib.Functions.Ema<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.EmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback);
        }
        _output.WriteLine("EMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] output = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (streaming)
            var ema = new global::QuanTAlib.Ema(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(ema.Update(item).Value);
            }

            // Calculate TA-Lib EMA
            var retCode = TALib.Functions.Ema<double>(tData, 0..^0, output, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.EmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback);
        }
        _output.WriteLine("EMA Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] sourceData = _testData.RawData.ToArray();
        double[] talibOutput = new double[sourceData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Ema.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate TA-Lib EMA
            var retCode = TALib.Functions.Ema<double>(sourceData, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.EmaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback);
        }
        _output.WriteLine("EMA Span validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (batch TSeries)
            var ema = new global::QuanTAlib.Ema(period);
            var qResult = ema.Update(_testData.Data);

            // Calculate Tulip EMA
            var emaIndicator = Tulip.Indicators.ema;
            double[][] inputs = { tData };
            double[] options = { period };
            double[][] outputs = { new double[tData.Length] };

            emaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, 0);
        }
        _output.WriteLine("EMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (streaming)
            var ema = new global::QuanTAlib.Ema(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(ema.Update(item).Value);
            }

            // Calculate Tulip EMA
            var emaIndicator = Tulip.Indicators.ema;
            double[][] inputs = { tData };
            double[] options = { period };
            double[][] outputs = { new double[tData.Length] };

            emaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, 0);
        }
        _output.WriteLine("EMA Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib EMA (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Ema.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Tulip EMA
            var emaIndicator = Tulip.Indicators.ema;
            double[][] inputs = { sourceData };
            double[] options = { period };
            double[][] outputs = { new double[sourceData.Length] };

            emaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, tResult, 0);
        }
        _output.WriteLine("EMA Span validated successfully against Tulip");
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
            // Calculate QuanTAlib EMA
            var ema = new global::QuanTAlib.Ema(period);
            var qResult = ema.Update(_testData.Data);

            // Calculate Ooples EMA
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateExponentialMovingAverage(period);
            var oValues = oResult.OutputValues.Values.First();

            // Compare
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("EMA validated successfully against Ooples");
    }
}
