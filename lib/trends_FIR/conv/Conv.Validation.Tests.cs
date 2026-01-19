using QuanTAlib.Tests;
using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib;

public sealed class ConvValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public ConvValidationTests()
    {
        _testData = new ValidationTestData(count: 1000, seed: 123);
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

    private static double[] GenerateWmaKernel(int period)
    {
        double divisor = period * (period + 1) / 2.0;
        double[] kernel = new double[period];
        for (int i = 0; i < period; i++)
        {
            kernel[i] = (i + 1) / divisor;
        }
        return kernel;
    }

    [Fact]
    public void Validate_Against_Sma()
    {
        // SMA(10) is equivalent to Conv with 10 weights of 1/10
        const int period = 10;
        double weight = 1.0 / period;
        double[] kernel = new double[period];
        Array.Fill(kernel, weight);

        var sma = new Sma(period);
        var conv = new Conv(kernel);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var item = _testData.Data[i];
            var smaVal = sma.Update(item);
            var convVal = conv.Update(item);

            if (i >= period) // Skip warmup
            {
                Assert.Equal(smaVal.Value, convVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Validate_Against_Wma()
    {
        int period = 10;
        double[] kernel = GenerateWmaKernel(period);

        var wma = new Wma(period);
        var conv = new Conv(kernel);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var item = _testData.Data[i];
            var wmaVal = wma.Update(item);
            var convVal = conv.Update(item);

            if (i >= period) // Skip warmup
            {
                Assert.Equal(wmaVal.Value, convVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Validate_Against_Trima()
    {
        // TRIMA(10) - Even period
        // Weights: 1, 2, 3, 4, 5, 5, 4, 3, 2, 1
        // Sum: 30
        int period = 10;
        double[] kernel = new double[period];
        double sum = 0;

        // Generate triangular weights
        int mid = period / 2;
        for (int i = 0; i < period; i++)
        {
            double val = (i < mid) ? (i + 1) : (period - i);
            kernel[i] = val;
            sum += val;
        }

        // Normalize
        for (int i = 0; i < period; i++)
        {
            kernel[i] /= sum;
        }

        var trima = new Trima(period);
        var conv = new Conv(kernel);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var item = _testData.Data[i];
            var trimaVal = trima.Update(item);
            var convVal = conv.Update(item);

            if (i >= period) // Skip warmup
            {
                Assert.Equal(trimaVal.Value, convVal.Value, ValidationHelper.DefaultTolerance);
            }
        }
    }

    [Fact]
    public void Validate_Against_Skender_Wma()
    {
        int period = 14;
        var skenderWma = _testData.SkenderQuotes.GetWma(period).ToList();
        double[] kernel = GenerateWmaKernel(period);
        var conv = new Conv(kernel);
        var result = conv.Update(_testData.Data);

        ValidationHelper.VerifyData(result, skenderWma, (s) => s.Wma, skip: period);
    }

    [Fact]
    public void Validate_Against_TALib_Wma()
    {
        int period = 14;
        double[] input = _testData.Data.Values.ToArray();
        double[] output = new double[input.Length];

        var retCode = TALib.Functions.Wma<double>(input, 0..^0, output, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        double[] kernel = GenerateWmaKernel(period);
        var conv = new Conv(kernel);
        var result = conv.Update(_testData.Data);

        ValidationHelper.VerifyData(result, output, outRange, lookback: period - 1);
    }

    [Fact]
    public void Validate_Against_Tulip_Wma()
    {
        int period = 14;
        double[] input = _testData.Data.Values.ToArray();

        var wmaIndicator = Tulip.Indicators.wma;
        double[][] inputs = { input };
        double[] options = { period };
        double[][] outputs = { new double[input.Length - period + 1] };

        wmaIndicator.Run(inputs, options, outputs);
        double[] output = outputs[0];

        double[] kernel = GenerateWmaKernel(period);
        var conv = new Conv(kernel);
        var result = conv.Update(_testData.Data);

        ValidationHelper.VerifyData(result, output, lookback: period - 1);
    }

    [Fact]
    public void Validate_Against_Ooples_Wma()
    {
        int period = 14;
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
        var ooplesWma = stockData.CalculateWeightedMovingAverage(length: period).OutputValues["Wma"];

        double[] kernel = GenerateWmaKernel(period);
        var conv = new Conv(kernel);
        var result = conv.Update(_testData.Data);

        ValidationHelper.VerifyData(result, ooplesWma, (s) => s, skip: period, tolerance: ValidationHelper.OoplesTolerance);
    }
}
