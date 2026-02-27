using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CCI (Commodity Channel Index) against external libraries.
/// CCI = (Typical Price - SMA of TP) / (0.015 × Mean Deviation)
/// where TP = (High + Low + Close) / 3
///
/// TALib, Tulip, Skender, and Ooples all implement CCI.
/// </summary>
public sealed class CciValidationTests(ITestOutputHelper output) : IDisposable
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private bool _disposed;

    private const int TestPeriod = 20;

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    #region TALib Validation

    [Fact]
    public void Cci_MatchesTalib_Batch()
    {
        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        // QuanTAlib CCI
        var qResult = Cci.Batch(_testData.Bars, TestPeriod);

        // TALib CCI
        double[] tOutput = new double[high.Length];
        var retCode = TALib.Functions.Cci<double>(high, low, close, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.CciLookback(TestPeriod);

        int count = qResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback) { continue; }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) { continue; }

            Assert.True(
                Math.Abs(qResult[i].Value - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResult[i].Value:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine("CCI Batch validated successfully against TALib");
    }

    [Fact]
    public void Cci_MatchesTalib_Streaming()
    {
        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        // QuanTAlib CCI (streaming)
        var cci = new Cci(TestPeriod);
        var qResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            qResults.Add(cci.Update(bar).Value);
        }

        // TALib CCI
        double[] tOutput = new double[high.Length];
        var retCode = TALib.Functions.Cci<double>(high, low, close, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.CciLookback(TestPeriod);

        int count = qResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback) { continue; }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) { continue; }

            Assert.True(
                Math.Abs(qResults[i] - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine("CCI Streaming validated successfully against TALib");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(50)]
    public void Cci_MatchesTalib_DifferentPeriods(int period)
    {
        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        var qResult = Cci.Batch(_testData.Bars, period);

        double[] tOutput = new double[high.Length];
        var retCode = TALib.Functions.Cci<double>(high, low, close, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.CciLookback(period);

        int count = qResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback) { continue; }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) { continue; }

            Assert.True(
                Math.Abs(qResult[i].Value - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Period {period}, index {i}: QuanTAlib={qResult[i].Value:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine($"CCI period={period} validated against TALib");
    }

    #endregion

    #region Tulip Validation

    [Fact]
    public void Cci_MatchesTulip_Batch()
    {
        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        var qResult = Cci.Batch(_testData.Bars, TestPeriod);

        // Tulip CCI
        var cciIndicator = Tulip.Indicators.cci;
        double[][] inputs = [high, low, close];
        double[] options = [TestPeriod];
        int lookback = cciIndicator.Start(options);
        double[][] outputs = [new double[high.Length - lookback]];

        cciIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        // Compare after warmup
        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], qResult[qIdx].Value, 1e-6);
        }

        _output.WriteLine("CCI Batch validated successfully against Tulip");
    }

    [Fact]
    public void Cci_MatchesTulip_Streaming()
    {
        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        // QuanTAlib streaming
        var cci = new Cci(TestPeriod);
        var qResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            qResults.Add(cci.Update(bar).Value);
        }

        // Tulip CCI
        var cciIndicator = Tulip.Indicators.cci;
        double[][] inputs = [high, low, close];
        double[] options = [TestPeriod];
        int lookback = cciIndicator.Start(options);
        double[][] outputs = [new double[high.Length - lookback]];

        cciIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], qResults[qIdx], 1e-6);
        }

        _output.WriteLine("CCI Streaming validated successfully against Tulip");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(50)]
    public void Cci_MatchesTulip_DifferentPeriods(int period)
    {
        double[] high = _testData.Bars.Select(b => b.High).ToArray();
        double[] low = _testData.Bars.Select(b => b.Low).ToArray();
        double[] close = _testData.Bars.Select(b => b.Close).ToArray();

        var qResult = Cci.Batch(_testData.Bars, period);

        var cciIndicator = Tulip.Indicators.cci;
        double[][] inputs = [high, low, close];
        double[] options = [period];
        int lookback = cciIndicator.Start(options);
        double[][] outputs = [new double[high.Length - lookback]];

        cciIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        for (int i = 0; i < tulipResult.Length; i++)
        {
            int qIdx = i + lookback;
            Assert.Equal(tulipResult[i], qResult[qIdx].Value, 1e-6);
        }
    }

    #endregion

    #region Skender Validation

    [Fact]
    public void Cci_MatchesSkender_Batch()
    {
        var qResult = Cci.Batch(_testData.Bars, TestPeriod);

        var sResult = _testData.SkenderQuotes.GetCci(TestPeriod).ToList();

        // Compare last 100 records
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Cci);

        _output.WriteLine("CCI Batch validated successfully against Skender");
    }

    [Fact]
    public void Cci_MatchesSkender_Streaming()
    {
        var cci = new Cci(TestPeriod);
        var qResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            qResults.Add(cci.Update(bar).Value);
        }

        var sResult = _testData.SkenderQuotes.GetCci(TestPeriod).ToList();

        int count = qResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            if (sResult[i].Cci is null) { continue; }
            Assert.True(
                Math.Abs(qResults[i] - sResult[i].Cci!.Value) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, Skender={sResult[i].Cci:G17}");
        }

        _output.WriteLine("CCI Streaming validated successfully against Skender");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(50)]
    public void Cci_MatchesSkender_DifferentPeriods(int period)
    {
        var qResult = Cci.Batch(_testData.Bars, period);

        var sResult = _testData.SkenderQuotes.GetCci(period).ToList();

        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Cci);
    }

    #endregion

    // NOTE: Ooples CCI validation removed — OoplesFinance.StockIndicators uses a
    // fundamentally different internal mean-deviation calculation that diverges up to
    // ~10.6 from the standard CCI formula. TALib, Tulip, and Skender all match at 1e-6+,
    // confirming QuanTAlib's CCI correctness via the standard algorithm.

    #region Mathematical Validation

    [Fact]
    public void Cci_ManualCalculation_MatchesExpected()
    {
        int period = 5;

        var bars = new TBarSeries();
        var baseTime = DateTime.UtcNow.Ticks;
        var timeStep = TimeSpan.FromMinutes(1).Ticks;

        double[] highs = [22, 24, 23, 25, 26, 27, 26, 28, 27, 29];
        double[] lows = [20, 22, 21, 23, 24, 25, 24, 26, 25, 27];
        double[] closes = [21, 23, 22, 24, 25, 26, 25, 27, 26, 28];

        for (int i = 0; i < highs.Length; i++)
        {
            bars.Add(new TBar(
                baseTime + (i * timeStep),
                21.0 + i,  // open
                highs[i],
                lows[i],
                closes[i],
                1000));    // volume
        }

        var cci = new Cci(period);
        var qResult = cci.Update(bars);

        // Manual calculation for last value (index 9)
        double tp5 = (27.0 + 25.0 + 26.0) / 3.0;
        double tp6 = (26.0 + 24.0 + 25.0) / 3.0;
        double tp7 = (28.0 + 26.0 + 27.0) / 3.0;
        double tp8 = (27.0 + 25.0 + 26.0) / 3.0;
        double tp9 = (29.0 + 27.0 + 28.0) / 3.0;

        double smaTP = (tp5 + tp6 + tp7 + tp8 + tp9) / 5.0;
        double meanDev = (Math.Abs(tp5 - smaTP) + Math.Abs(tp6 - smaTP) + Math.Abs(tp7 - smaTP) + Math.Abs(tp8 - smaTP) + Math.Abs(tp9 - smaTP)) / 5.0;
        double expectedCci = (tp9 - smaTP) / (0.015 * meanDev);

        Assert.Equal(expectedCci, qResult[9].Value, 1e-10);
    }

    [Fact]
    public void Cci_FlatMarket_HandlesGracefully()
    {
        var bars = new TBarSeries();
        var baseTime = DateTime.UtcNow.Ticks;
        var timeStep = TimeSpan.FromMinutes(1).Ticks;

        for (int i = 0; i < 30; i++)
        {
            bars.Add(new TBar(baseTime + (i * timeStep), 100, 100, 100, 100, 1000));
        }

        var cci = new Cci(10);
        var result = cci.Update(bars);

        // In flat market, deviation = 0 → should handle gracefully
        for (int i = 10; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result[i].Value) || result[i].Value == 0,
                $"CCI at index {i} should be finite or zero, got {result[i].Value}");
        }
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        // Batch
        var batchResult = Cci.Batch(_testData.Bars, TestPeriod);

        // Streaming
        var cci = new Cci(TestPeriod);
        var streamingResults = new List<double>();
        foreach (var bar in _testData.Bars)
        {
            streamingResults.Add(cci.Update(bar).Value);
        }

        Assert.Equal(batchResult.Count, streamingResults.Count);
        int count = batchResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], 1e-10);
        }
        _output.WriteLine("CCI Batch vs Streaming consistency validated");
    }

    #endregion
}
