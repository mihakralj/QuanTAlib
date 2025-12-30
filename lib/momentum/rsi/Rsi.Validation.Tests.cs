using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class RsiValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
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
        int[] periods = { 9, 14, 25 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (batch TSeries)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResult = rsi.Update(_testData.Data);

            // Calculate Skender RSI
            var sResult = _testData.SkenderQuotes.GetRsi(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Rsi);
        }
        _output.WriteLine("RSI Batch(TSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 14, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[tData.Length];
            Rsi.Calculate(tData.AsSpan(), qOutput.AsSpan(), period);

            double[] tOutput = new double[tData.Length];
            var retCode = TALib.Functions.Rsi<double>(tData, 0..^0, tOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.RsiLookback(period);

            QuanTAlib.Tests.ValidationHelper.VerifyData(qOutput, tOutput, outRange, lookback);
        }
        _output.WriteLine("RSI Span validated against TA-Lib");
    }

    [Fact]
    public void Validate_Skender_Span()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for Span API
        double[] sourceData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (Span API)
            double[] qOutput = new double[sourceData.Length];
            global::QuanTAlib.Rsi.Calculate(sourceData.AsSpan(), qOutput.AsSpan(), period);

            // Calculate Skender RSI
            var sResult = _testData.SkenderQuotes.GetRsi(period).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Rsi);
        }
        _output.WriteLine("RSI Span validated successfully against Skender");
    }

    [Fact]
    public void Validate_Tulip_Span()
    {
        int[] periods = { 14, 20, 50, 100 };
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            double[] qOutput = new double[tData.Length];
            Rsi.Calculate(tData.AsSpan(), qOutput.AsSpan(), period);

            var rsiIndicator = Tulip.Indicators.rsi;
            double[][] inputs = { tData };
            double[] options = { period };
            int lookback = period;
            double[][] outputs = { new double[tData.Length - lookback] };

            rsiIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            QuanTAlib.Tests.ValidationHelper.VerifyData(qOutput, tResult, lookback);
        }
        _output.WriteLine("RSI Span validated against Tulip");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for TA-Lib (double[])
        double[] tData = _testData.RawData.ToArray();
        double[] tOutput = new double[tData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (streaming)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(rsi.Update(item).Value);
            }

            // Calculate TA-Lib RSI
            var retCode = TALib.Functions.Rsi<double>(tData, 0..^0, tOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.RsiLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tOutput, outRange, lookback);
        }
        _output.WriteLine("RSI Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (batch TSeries)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResult = rsi.Update(_testData.Data);

            // Calculate Tulip RSI
            var rsiIndicator = Tulip.Indicators.rsi;
            double[][] inputs = { tData };
            double[] options = { period };

            // Tulip RSI lookback
            int lookback = rsiIndicator.Start(options);
            double[][] outputs = { new double[tData.Length - lookback] };

            rsiIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback);
        }
        _output.WriteLine("RSI Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 9, 14, 25 };

        // Prepare data for Tulip (double[])
        double[] tData = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib RSI (streaming)
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Data)
            {
                qResults.Add(rsi.Update(item).Value);
            }

            // Calculate Tulip RSI
            var rsiIndicator = Tulip.Indicators.rsi;
            double[][] inputs = { tData };
            double[] options = { period };

            // Tulip RSI lookback
            int lookback = rsiIndicator.Start(options);
            double[][] outputs = { new double[tData.Length - lookback] };

            rsiIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback);
        }
        _output.WriteLine("RSI Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        int[] periods = { 9, 14, 25 };

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
            // Calculate QuanTAlib RSI
            var rsi = new global::QuanTAlib.Rsi(period);
            var qResult = rsi.Update(_testData.Data);

            // Calculate Ooples RSI
            var stockData = new StockData(ooplesData);
            var oResult = stockData.CalculateRelativeStrengthIndex(length: period);
            var oValues = oResult.OutputValues.Values.First();

            // Compare
            ValidationHelper.VerifyData(qResult, oValues, (s) => s, tolerance: ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("RSI validated successfully against Ooples");
    }
}
