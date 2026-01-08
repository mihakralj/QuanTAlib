using Skender.Stock.Indicators;
using TALib;
using Xunit.Abstractions;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class DwmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public DwmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData(count: 1000, seed: 42);
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
    public void Validate_Against_DoubleWma()
    {
        // DWMA should be exactly WMA(WMA(source, period), period)

        int period = 10;

        var dwma = new Dwma(period);
        var wma1 = new Wma(period);
        var wma2 = new Wma(period);

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var val = _testData.Data[i];

            // Calculate DWMA
            var dwmaVal = dwma.Update(val);

            // Calculate WMA(WMA) manually
            var wma1Val = wma1.Update(val);
            var wma2Val = wma2.Update(wma1Val);

            Assert.Equal(wma2Val.Value, dwmaVal.Value, ValidationHelper.DefaultTolerance);
        }
    }

    [Fact]
    public void Validate_Against_Ooples()
    {
        // Ooples Finance does not have a specific DWMA indicator, but it can be calculated
        // by chaining two Weighted Moving Averages

        int period = 14;

        var dwma = new Dwma(period);
        var wma1 = new Wma(period); // Simulates first CalculateWeightedMovingAverage
        var wma2 = new Wma(period); // Simulates second CalculateWeightedMovingAverage

        for (int i = 0; i < _testData.Data.Count; i++)
        {
            var val = _testData.Data[i];

            // QuanTAlib DWMA
            var qVal = dwma.Update(val);

            // Ooples Logic (Chained WMA)
            var w1 = wma1.Update(val);
            var w2 = wma2.Update(w1);

            Assert.Equal(w2.Value, qVal.Value, ValidationHelper.DefaultTolerance);
        }
    }

    [Fact]
    public void Validate_Against_Tulip()
    {
        // Tulip does not have DWMA, so we chain two WMAs
        int[] periods = { 10, 20 };
        foreach (var period in periods)
        {
            var dwma = new Dwma(period);
            var qResult = dwma.Update(_testData.Data);

            // Tulip WMA 1
            var wmaIndicator = Tulip.Indicators.wma;
            double[][] inputs1 = { _testData.RawData.ToArray() };
            double[] options = { period };
            int lookback1 = period - 1;
            double[][] outputs1 = { new double[_testData.RawData.Length - lookback1] };
            wmaIndicator.Run(inputs1, options, outputs1);

            // Tulip WMA 2
            double[][] inputs2 = { outputs1[0] };
            int lookback2 = period - 1;
            double[][] outputs2 = { new double[inputs2[0].Length - lookback2] };
            wmaIndicator.Run(inputs2, options, outputs2);

            var tResult = outputs2[0];
            int totalLookback = lookback1 + lookback2;

            ValidationHelper.VerifyData(qResult, tResult, totalLookback, tolerance: ValidationHelper.TulipTolerance);
        }
        _output.WriteLine("DWMA validated against Tulip (Chained WMA)");
    }

    [Fact]
    public void Validate_Against_Skender()
    {
        // Skender does not have DWMA, so we chain two WMAs
        int[] periods = { 10, 20 };
        foreach (var period in periods)
        {
            var dwma = new Dwma(period);
            var qResult = dwma.Update(_testData.Data);

            // Skender WMA 1
            var wma1Results = _testData.SkenderQuotes.GetWma(period)
                .Where(x => x.Wma.HasValue)
                .Select(x => new Quote { Date = x.Date, Close = (decimal)x.Wma!.Value })
                .ToList();

            // Skender WMA 2
            var wma2Results = wma1Results.GetWma(period)
                .Where(x => x.Wma.HasValue)
                .Select(x => x.Wma!.Value)
                .ToArray();

            int totalLookback = (period - 1) * 2;
            ValidationHelper.VerifyData(qResult, wma2Results, totalLookback, tolerance: ValidationHelper.SkenderTolerance);
        }
        _output.WriteLine("DWMA validated against Skender (Chained WMA)");
    }

    [Fact]
    public void Validate_Against_Talib()
    {
        // TA-Lib does not have DWMA, so we chain two WMAs
        int[] periods = { 10, 20 };
        foreach (var period in periods)
        {
            var dwma = new Dwma(period);
            var qResult = dwma.Update(_testData.Data);

            // TA-Lib WMA 1
            double[] wma1Output = new double[_testData.RawData.Length];
            var retCode1 = TALib.Functions.Wma(_testData.RawData.Span, 0..^0, wma1Output, out var outRange1, period);
            Assert.Equal(Core.RetCode.Success, retCode1);

            // Prepare input for WMA 2 (only valid data from WMA 1)
            int count1 = outRange1.End.Value - outRange1.Start.Value;
            double[] wma1Valid = new double[count1];
            Array.Copy(wma1Output, 0, wma1Valid, 0, count1);

            // TA-Lib WMA 2
            double[] dwmaOutput = new double[wma1Valid.Length];
            var retCode2 = TALib.Functions.Wma(wma1Valid, 0..^0, dwmaOutput, out _, period);
            Assert.Equal(Core.RetCode.Success, retCode2);

            int totalLookback = (period - 1) * 2;
            ValidationHelper.VerifyData(qResult, dwmaOutput, totalLookback, tolerance: ValidationHelper.TalibTolerance);
        }
        _output.WriteLine("DWMA validated against TA-Lib (Chained WMA)");
    }
}
