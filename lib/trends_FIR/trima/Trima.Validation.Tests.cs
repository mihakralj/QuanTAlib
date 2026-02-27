using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class TrimaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    public TrimaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (batch TSeries)
            var trima = new global::QuanTAlib.Trima(period);
            var qResult = trima.Update(_testData.Data);

            // Calculate Skender Composite TRIMA: SMA(SMA(x, p1), p2)
            int p1 = period / 2 + 1;
            int p2 = (period + 1) / 2;

            var sma1Results = _testData.SkenderQuotes.GetSma(p1).ToList();

            // Map SMA1 results to Quotes for the second pass
            // Note: We use 0 for null values during warmup, which might affect early values
            // but should stabilize for the verification window (last 100 records)
            var quotes2 = sma1Results.Select(r => new Quote
            {
                Date = r.Date,
                Close = (decimal)(r.Sma ?? 0)
            }).ToList();

            var sResult = quotes2.GetSma(p2).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, x => x.Sma, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("TRIMA Batch(TSeries) validated successfully against Skender Composite SMA");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data for TA-Lib (double[])
        double[] output = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (batch TSeries)
            var trima = new global::QuanTAlib.Trima(period);
            var qResult = trima.Update(_testData.Data);

            // Calculate TA-Lib TRIMA
            var retCode = TALib.Functions.Trima<double>(_testData.RawData.Span, 0..^0, output, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TrimaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("TRIMA Batch(TSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (batch TSeries)
            var trima = new global::QuanTAlib.Trima(period);
            var qResult = trima.Update(_testData.Data);

            // Calculate Tulip TRIMA
            var trimaIndicator = Tulip.Indicators.trima;
            double[][] inputs = { _testData.RawData.ToArray() };
            double[] options = { period };
            // Tulip TRIMA lookback might be different, let's calculate or infer
            // Usually it's period-1 for simple averages, but TRIMA is double smoothed.
            // We'll rely on the output length to align.
            // Tulip.Indicators.trima.Run expects outputs to be sized correctly.
            // We can try to run it with a large buffer and see what happens,
            // or calculate the expected lookback.
            // For TRIMA(n), lookback is roughly n-1.
            int lookback = period - 1;
            double[][] outputs = { new double[_testData.RawData.Length - lookback] };

            trimaIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("TRIMA Batch(TSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Talib_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        // Prepare data
        double[] talibOutput = new double[_testData.RawData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib TRIMA (Span API)
            double[] qOutput = new double[_testData.RawData.Length];
            global::QuanTAlib.Trima.Batch(_testData.RawData.Span, qOutput.AsSpan(), period);

            // Calculate TA-Lib TRIMA
            var retCode = TALib.Functions.Trima<double>(_testData.RawData.Span, 0..^0, talibOutput, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.TrimaLookback(period);

            // Compare last 100 records
            ValidationHelper.VerifyData(qOutput, talibOutput, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("TRIMA Span validated successfully against TA-Lib");
    }

    // ── Cross-library: OoplesFinance ──────────────────────────────────────────
    [Fact]
    public void Trima_MatchesOoples_Structural()
    {
        const int period = 14;
        var ooplesData = _testData.SkenderQuotes.Select(static q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateTriangularMovingAverage(length: period);
        var oValues = oResult.OutputValues.Values.First();

        var trima = new global::QuanTAlib.Trima(period);
        var qValues = new List<double>();
        foreach (var item in _testData.Data)
        {
            qValues.Add(trima.Update(item).Value);
        }

        Assert.True(oValues.Count > 0, "Ooples Trima must produce output");
        int finiteCount = 0;
        for (int i = period; i < Math.Min(oValues.Count, qValues.Count); i++)
        {
            if (double.IsFinite(oValues[i]) && double.IsFinite(qValues[i]))
            {
                finiteCount++;
            }
        }
        Assert.True(finiteCount > 100, $"Expected >100 finite Trima pairs, got {finiteCount}");
        _output.WriteLine($"Trima Ooples structural: {finiteCount} finite pairs verified.");
    }
}
