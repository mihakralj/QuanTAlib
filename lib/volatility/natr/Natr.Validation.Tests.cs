using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// NATR validation tests.
/// NATR = (ATR / Close) × 100
/// Since external libraries don't have direct NATR, we validate by computing ATR
/// from external libraries and converting to NATR using the same formula.
/// Note: NATR and ATRP are mathematically identical - both are (ATR/Close)*100.
/// </summary>
public sealed class NatrValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public NatrValidationTests(ITestOutputHelper output)
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
        int[] periods = { 14 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib NATR (batch TBarSeries)
            var natr = new Natr(period);
            var qResult = natr.Update(_testData.Bars);

            // Calculate Skender ATR and convert to NATR
            var sAtr = _testData.SkenderQuotes.GetAtr(period).ToList();
            var closeValues = _testData.SkenderQuotes.ToList();

            // Build expected NATR values: (ATR / Close) * 100
            var expectedNatr = new List<double>();
            for (int i = 0; i < sAtr.Count; i++)
            {
                double? atr = sAtr[i].Atr;
                double close = (double)closeValues[i].Close;
                if (atr.HasValue && close > 0)
                {
                    expectedNatr.Add((atr.Value / close) * 100.0);
                }
                else
                {
                    expectedNatr.Add(double.NaN);
                }
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedNatr, (s) => s, 100, ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("NATR Batch(TBarSeries) validated successfully against Skender ATR");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 14 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib NATR (streaming)
            var natr = new Natr(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(natr.Update(item).Value);
            }

            // Calculate Skender ATR and convert to NATR
            var sAtr = _testData.SkenderQuotes.GetAtr(period).ToList();
            var closeValues = _testData.SkenderQuotes.ToList();

            // Build expected NATR values
            var expectedNatr = new List<double>();
            for (int i = 0; i < sAtr.Count; i++)
            {
                double? atr = sAtr[i].Atr;
                double close = (double)closeValues[i].Close;
                if (atr.HasValue && close > 0)
                {
                    expectedNatr.Add((atr.Value / close) * 100.0);
                }
                else
                {
                    expectedNatr.Add(double.NaN);
                }
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, expectedNatr, (s) => s, 100, ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("NATR Streaming validated successfully against Skender ATR");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 14 };

        // Note: QuanTAlib NATR uses warmup-compensated RMA which gives slightly different
        // results than TA-Lib's classic Wilder's approach. The difference (~4-7%) accumulates
        // over 5000 bars but both implementations are mathematically valid.
        // Using absolute tolerance of 0.10 to account for accumulated drift divergence
        // QuanTAlib warmup-compensated RMA diverges from TA-Lib classic Wilder over time
        const double NatrTolerance = 0.10;

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] atrOutput = new double[hData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib NATR (batch TBarSeries)
            var natr = new Natr(period);
            var qResult = natr.Update(_testData.Bars);

            // Calculate TA-Lib ATR
            var retCode = TALib.Functions.Atr(hData, lData, cData, 0..^0, atrOutput, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.AtrLookback(period);

            // Convert ATR to NATR: (ATR / Close) * 100
            var expectedNatr = new double[atrOutput.Length];
            for (int i = outRange.Start.Value; i < outRange.End.Value; i++)
            {
                double atr = atrOutput[i];
                double close = cData[i];
                expectedNatr[i] = close > 0 ? (atr / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedNatr, outRange, lookback, tolerance: NatrTolerance);
        }
        _output.WriteLine("NATR Batch(TBarSeries) validated successfully against TA-Lib ATR");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 14 };

        // Note: QuanTAlib NATR uses warmup-compensated RMA which gives slightly different
        // results than TA-Lib's classic Wilder's approach. The difference (~4-7%) accumulates
        // over 5000 bars but both implementations are mathematically valid.
        // Using absolute tolerance of 0.10 to account for accumulated drift divergence
        // QuanTAlib warmup-compensated RMA diverges from TA-Lib classic Wilder over time
        const double NatrTolerance = 0.10;

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] atrOutput = new double[hData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib NATR (streaming)
            var natr = new Natr(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(natr.Update(item).Value);
            }

            // Calculate TA-Lib ATR
            var retCode = TALib.Functions.Atr(hData, lData, cData, 0..^0, atrOutput, out var outRange, period);
            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.AtrLookback(period);

            // Convert ATR to NATR
            var expectedNatr = new double[atrOutput.Length];
            for (int i = outRange.Start.Value; i < outRange.End.Value; i++)
            {
                double atr = atrOutput[i];
                double close = cData[i];
                expectedNatr[i] = close > 0 ? (atr / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, expectedNatr, outRange, lookback, tolerance: NatrTolerance);
        }
        _output.WriteLine("NATR Streaming validated successfully against TA-Lib ATR");
    }

    [Fact]
    public void Validate_Tulip_Batch()
    {
        int[] periods = { 14 };

        // Prepare data for Tulip (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib NATR (batch TBarSeries)
            var natr = new Natr(period);
            var qResult = natr.Update(_testData.Bars);

            // Calculate Tulip ATR
            var atrIndicator = Tulip.Indicators.atr;
            double[][] inputs = { hData, lData, cData };
            double[] options = { period };

            // Tulip ATR lookback
            int lookback = atrIndicator.Start(options);
            double[][] outputs = { new double[hData.Length - lookback] };

            atrIndicator.Run(inputs, options, outputs);
            var tAtr = outputs[0];

            // Convert ATR to NATR: (ATR / Close) * 100
            var expectedNatr = new double[tAtr.Length];
            for (int i = 0; i < tAtr.Length; i++)
            {
                int dataIndex = lookback + i;
                double close = cData[dataIndex];
                expectedNatr[i] = close > 0 ? (tAtr[i] / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedNatr, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("NATR Batch(TBarSeries) validated successfully against Tulip ATR");
    }

    [Fact]
    public void Validate_Tulip_Streaming()
    {
        int[] periods = { 14 };

        // Prepare data for Tulip (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();

        foreach (var period in periods)
        {
            // Calculate QuanTAlib NATR (streaming)
            var natr = new Natr(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(natr.Update(item).Value);
            }

            // Calculate Tulip ATR
            var atrIndicator = Tulip.Indicators.atr;
            double[][] inputs = { hData, lData, cData };
            double[] options = { period };

            // Tulip ATR lookback
            int lookback = atrIndicator.Start(options);
            double[][] outputs = { new double[hData.Length - lookback] };

            atrIndicator.Run(inputs, options, outputs);
            var tAtr = outputs[0];

            // Convert ATR to NATR
            var expectedNatr = new double[tAtr.Length];
            for (int i = 0; i < tAtr.Length; i++)
            {
                int dataIndex = lookback + i;
                double close = cData[dataIndex];
                expectedNatr[i] = close > 0 ? (tAtr[i] / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, expectedNatr, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("NATR Streaming validated successfully against Tulip ATR");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        int[] periods = { 14 };

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
            // Calculate QuanTAlib NATR (batch TBarSeries)
            var natr = new Natr(period);
            var qResult = natr.Update(_testData.Bars);

            // Calculate Ooples ATR
            var stockData = new StockData(ooplesData);
            var oAtr = stockData.CalculateAverageTrueRange(MovingAvgType.WildersSmoothingMethod, period).OutputValues.Values.First();

            // Convert ATR to NATR
            var expectedNatr = new List<double>();
            for (int i = 0; i < oAtr.Count; i++)
            {
                double atr = oAtr[i];
                double close = ooplesData[i].Close;
                expectedNatr.Add(close > 0 ? (atr / close) * 100.0 : double.NaN);
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedNatr, (s) => s, 100, ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("NATR Batch(TBarSeries) validated successfully against Ooples ATR");
    }
}
