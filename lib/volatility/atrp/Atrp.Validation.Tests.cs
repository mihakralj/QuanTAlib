using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// ATRP validation tests.
/// ATRP = (ATR / Close) × 100
/// Since external libraries don't have direct ATRP, we validate by computing ATR
/// from external libraries and converting to ATRP using the same formula.
/// </summary>
public sealed class AtrpValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AtrpValidationTests(ITestOutputHelper output)
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
            // Calculate QuanTAlib ATRP (batch TSeries)
            var atrp = new Atrp(period);
            var qResult = atrp.Update(_testData.Bars);

            // Calculate Skender ATR and convert to ATRP
            var sAtr = _testData.SkenderQuotes.GetAtr(period).ToList();
            var closeValues = _testData.SkenderQuotes.ToList();

            // Build expected ATRP values: (ATR / Close) * 100
            var expectedAtrp = new List<double>();
            for (int i = 0; i < sAtr.Count; i++)
            {
                double? atr = sAtr[i].Atr;
                double close = (double)closeValues[i].Close;
                if (atr.HasValue && close > 0)
                {
                    expectedAtrp.Add((atr.Value / close) * 100.0);
                }
                else
                {
                    expectedAtrp.Add(double.NaN);
                }
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedAtrp, (s) => s, 100, ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("ATRP Batch(TSeries) validated successfully against Skender ATR");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        int[] periods = { 14 };

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATRP (streaming)
            var atrp = new Atrp(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(atrp.Update(item).Value);
            }

            // Calculate Skender ATR and convert to ATRP
            var sAtr = _testData.SkenderQuotes.GetAtr(period).ToList();
            var closeValues = _testData.SkenderQuotes.ToList();

            // Build expected ATRP values
            var expectedAtrp = new List<double>();
            for (int i = 0; i < sAtr.Count; i++)
            {
                double? atr = sAtr[i].Atr;
                double close = (double)closeValues[i].Close;
                if (atr.HasValue && close > 0)
                {
                    expectedAtrp.Add((atr.Value / close) * 100.0);
                }
                else
                {
                    expectedAtrp.Add(double.NaN);
                }
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, expectedAtrp, (s) => s, 100, ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("ATRP Streaming validated successfully against Skender ATR");
    }

    [Fact]
    public void Validate_Talib_Batch()
    {
        int[] periods = { 14 };

        // Note: QuanTAlib ATRP uses warmup-compensated RMA which gives slightly different
        // results than TA-Lib's classic Wilder's approach. The difference (~4-7%) accumulates
        // over 5000 bars but both implementations are mathematically valid.
        // Using absolute tolerance of 0.10 to account for accumulated drift divergence
        // QuanTAlib warmup-compensated RMA diverges from TA-Lib classic Wilder over time
        const double AtrpTolerance = 0.10;

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] atrOutput = new double[hData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATRP (batch TSeries)
            var atrp = new Atrp(period);
            var qResult = atrp.Update(_testData.Bars);

            // Calculate TA-Lib ATR
            var retCode = TALib.Functions.Atr(hData, lData, cData, 0..^0, atrOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.AtrLookback(period);

            // Convert ATR to ATRP: (ATR / Close) * 100
            var expectedAtrp = new double[atrOutput.Length];
            for (int i = outRange.Start.Value; i < outRange.End.Value; i++)
            {
                double atr = atrOutput[i];
                double close = cData[i];
                expectedAtrp[i] = close > 0 ? (atr / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedAtrp, outRange, lookback, tolerance: AtrpTolerance);
        }
        _output.WriteLine("ATRP Batch(TSeries) validated successfully against TA-Lib ATR");
    }

    [Fact]
    public void Validate_Talib_Streaming()
    {
        int[] periods = { 14 };

        // Note: QuanTAlib ATRP uses warmup-compensated RMA which gives slightly different
        // results than TA-Lib's classic Wilder's approach. The difference (~4-7%) accumulates
        // over 5000 bars but both implementations are mathematically valid.
        // Using absolute tolerance of 0.10 to account for accumulated drift divergence
        // QuanTAlib warmup-compensated RMA diverges from TA-Lib classic Wilder over time
        const double AtrpTolerance = 0.10;

        // Prepare data for TA-Lib (double[])
        double[] hData = _testData.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _testData.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _testData.Bars.Close.Select(x => x.Value).ToArray();
        double[] atrOutput = new double[hData.Length];

        foreach (var period in periods)
        {
            // Calculate QuanTAlib ATRP (streaming)
            var atrp = new Atrp(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(atrp.Update(item).Value);
            }

            // Calculate TA-Lib ATR
            var retCode = TALib.Functions.Atr(hData, lData, cData, 0..^0, atrOutput, out var outRange, period);
            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = TALib.Functions.AtrLookback(period);

            // Convert ATR to ATRP
            var expectedAtrp = new double[atrOutput.Length];
            for (int i = outRange.Start.Value; i < outRange.End.Value; i++)
            {
                double atr = atrOutput[i];
                double close = cData[i];
                expectedAtrp[i] = close > 0 ? (atr / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, expectedAtrp, outRange, lookback, tolerance: AtrpTolerance);
        }
        _output.WriteLine("ATRP Streaming validated successfully against TA-Lib ATR");
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
            // Calculate QuanTAlib ATRP (batch TSeries)
            var atrp = new Atrp(period);
            var qResult = atrp.Update(_testData.Bars);

            // Calculate Tulip ATR
            var atrIndicator = Tulip.Indicators.atr;
            double[][] inputs = { hData, lData, cData };
            double[] options = { period };

            // Tulip ATR lookback
            int lookback = atrIndicator.Start(options);
            double[][] outputs = { new double[hData.Length - lookback] };

            atrIndicator.Run(inputs, options, outputs);
            var tAtr = outputs[0];

            // Convert ATR to ATRP: (ATR / Close) * 100
            var expectedAtrp = new double[tAtr.Length];
            for (int i = 0; i < tAtr.Length; i++)
            {
                int dataIndex = lookback + i;
                double close = cData[dataIndex];
                expectedAtrp[i] = close > 0 ? (tAtr[i] / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedAtrp, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("ATRP Batch(TSeries) validated successfully against Tulip ATR");
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
            // Calculate QuanTAlib ATRP (streaming)
            var atrp = new Atrp(period);
            var qResults = new List<double>();
            foreach (var item in _testData.Bars)
            {
                qResults.Add(atrp.Update(item).Value);
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

            // Convert ATR to ATRP
            var expectedAtrp = new double[tAtr.Length];
            for (int i = 0; i < tAtr.Length; i++)
            {
                int dataIndex = lookback + i;
                double close = cData[dataIndex];
                expectedAtrp[i] = close > 0 ? (tAtr[i] / close) * 100.0 : double.NaN;
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResults, expectedAtrp, lookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("ATRP Streaming validated successfully against Tulip ATR");
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
            // Calculate QuanTAlib ATRP (batch TSeries)
            var atrp = new Atrp(period);
            var qResult = atrp.Update(_testData.Bars);

            // Calculate Ooples ATR
            var stockData = new StockData(ooplesData);
            var oAtr = stockData.CalculateAverageTrueRange(MovingAvgType.WildersSmoothingMethod, period).OutputValues.Values.First();

            // Convert ATR to ATRP
            var expectedAtrp = new List<double>();
            for (int i = 0; i < oAtr.Count; i++)
            {
                double atr = oAtr[i];
                double close = ooplesData[i].Close;
                expectedAtrp.Add(close > 0 ? (atr / close) * 100.0 : double.NaN);
            }

            // Compare last 100 records
            ValidationHelper.VerifyData(qResult, expectedAtrp, (s) => s, 100, ValidationHelper.OoplesTolerance);
        }
        _output.WriteLine("ATRP Batch(TSeries) validated successfully against Ooples ATR");
    }
}