using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class UltoscValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public UltoscValidationTests(ITestOutputHelper output)
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
        int[][] periodSets = { [7, 14, 28] };

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            // Calculate QuanTAlib Ultosc (batch TBarSeries)
            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_testData.Bars);

            // Calculate Skender Ultimate Oscillator
            var sResult = _testData.SkenderQuotes.GetUltimate(p1, p2, p3).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s.Ultimate, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("Ultosc Batch(TBarSeries) validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[][] periodSets = { [7, 14, 28] };

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            // Calculate QuanTAlib Ultosc (streaming)
            var ultosc = new Ultosc(p1, p2, p3);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(ultosc.Update(item).Value);
            }

            // Calculate Skender Ultimate Oscillator
            var sResult = _testData.SkenderQuotes.GetUltimate(p1, p2, p3).ToList();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, sResult, (s) => s.Ultimate, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("Ultosc Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[][] periodSets = { [7, 14, 28] };

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] output = new double[hData.Length];

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            // Calculate QuanTAlib Ultosc (batch TBarSeries)
            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_testData.Bars);

            // Calculate TA-Lib UltOsc
            var retCode = TALib.Functions.UltOsc(hData, lData, cData, 0..^0, output, out var outRange, p1, p2, p3);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.UltOscLookback(p1, p2, p3);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, output, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("Ultosc Batch(TBarSeries) validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[][] periodSets = { [7, 14, 28] };

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] output = new double[hData.Length];

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            // Calculate QuanTAlib Ultosc (streaming)
            var ultosc = new Ultosc(p1, p2, p3);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(ultosc.Update(item).Value);
            }

            // Calculate TA-Lib UltOsc
            var retCode = TALib.Functions.UltOsc(hData, lData, cData, 0..^0, output, out var outRange, p1, p2, p3);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.UltOscLookback(p1, p2, p3);

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, output, outRange, lookback, tolerance: ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("Ultosc Streaming validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[][] periodSets = { [7, 14, 28] };

        // Prepare data for Tulip (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            // Calculate QuanTAlib Ultosc (batch TBarSeries)
            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_testData.Bars);

            // Calculate Tulip UltOsc
            var ultoscIndicator = Tulip.Indicators.ultosc;
            double[][] inputs = { hData, lData, cData };
            double[] options = { p1, p2, p3 };

            // Tulip UltOsc lookback
            int lookback = ultoscIndicator.Start(options);
            double[][] outputs = { new double[hData.Length - lookback] };

            ultoscIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("Ultosc Batch(TBarSeries) validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[][] periodSets = { [7, 14, 28] };

        // Prepare data for Tulip (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            // Calculate QuanTAlib Ultosc (streaming)
            var ultosc = new Ultosc(p1, p2, p3);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(ultosc.Update(item).Value);
            }

            // Calculate Tulip UltOsc
            var ultoscIndicator = Tulip.Indicators.ultosc;
            double[][] inputs = { hData, lData, cData };
            double[] options = { p1, p2, p3 };

            // Tulip UltOsc lookback
            int lookback = ultoscIndicator.Start(options);
            double[][] outputs = { new double[hData.Length - lookback] };

            ultoscIndicator.Run(inputs, options, outputs);
            var tResult = outputs[0];

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("Ultosc Streaming validated successfully against Tulip");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        int[][] periodSets = { [7, 14, 28] };

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

        foreach (var periods in periodSets)
        {
            int p1 = periods[0];
            int p2 = periods[1];
            int p3 = periods[2];

            // Calculate QuanTAlib Ultosc (batch TBarSeries)
            var ultosc = new Ultosc(p1, p2, p3);
            var qResult = ultosc.Update(_testData.Bars);

            // Calculate Ooples Ultimate Oscillator
            var stockData = new StockData(ooplesData);
            var sResult = stockData.CalculateUltimateOscillator(p1, p2, p3).OutputValues.Values.First();

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, sResult, (s) => s, 100, ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("Ultosc Batch(TBarSeries) validated successfully against Ooples");
    }

    [Fact]
    public void Validate_Span_MatchesTBarSeries()
    {
        int p1 = 7;
        int p2 = 14;
        int p3 = 28;

        // Prepare data
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] spanOutput = new double[hData.Length];

        // Calculate using span method
        Ultosc.Calculate(hData, lData, cData, spanOutput, p1, p2, p3);

        // Calculate using TBarSeries batch
        var ultosc = new Ultosc(p1, p2, p3);
        var tbarResult = ultosc.Update(_testData.Bars);

        // Compare results
        for (int i = 0; i < tbarResult.Count; i++)
        {
            Assert.Equal(tbarResult[i].Value, spanOutput[i], 1e-10);
        }
        _output.WriteLine("Ultosc Span calculation matches TBarSeries batch calculation");
    }
}
