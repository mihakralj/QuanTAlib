using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ROCR (Rate of Change Ratio) against external libraries.
/// ROCR = Price / Price[N]
///
/// TALib's RocR returns the same ratio. Tulip's rocr returns the same ratio.
/// No scaling adjustment needed.
/// </summary>
public sealed class RocrValidationTests(ITestOutputHelper output) : IDisposable
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
        if (_disposed) { return; }
        _disposed = true;
        if (disposing) { _testData?.Dispose(); }
    }

    private const int TestPeriod = 9;

    #region TALib Validation

    [Fact]
    public void Rocr_MatchesTalib_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCR (batch TSeries)
        var rocr = new Rocr(TestPeriod);
        var qResult = rocr.Update(_testData.Data);

        // TALib RocR
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocR<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocRLookback(TestPeriod);

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
        _output.WriteLine("ROCR Batch validated successfully against TALib");
    }

    [Fact]
    public void Rocr_MatchesTalib_Span()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCR (Span)
        double[] qOutput = new double[tData.Length];
        Rocr.Batch(tData.AsSpan(), qOutput.AsSpan(), TestPeriod);

        // TALib RocR
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocR<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocRLookback(TestPeriod);

        int count = qOutput.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback) { continue; }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) { continue; }

            Assert.True(
                Math.Abs(qOutput[i] - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qOutput[i]:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine("ROCR Span validated successfully against TALib");
    }

    [Fact]
    public void Rocr_MatchesTalib_Streaming()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCR (streaming)
        var rocr = new Rocr(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(rocr.Update(item).Value);
        }

        // TALib RocR
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocR<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocRLookback(TestPeriod);

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
        _output.WriteLine("ROCR Streaming validated successfully against TALib");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(50)]
    public void Rocr_MatchesTalib_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        var rocr = new Rocr(period);
        var qResult = rocr.Update(_testData.Data);

        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocR<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocRLookback(period);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        int count = qResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            if (i < lookback) { continue; }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length) { continue; }

            Assert.True(
                Math.Abs(qResult[i].Value - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Period {period}, index {i}: QuanTAlib={qResult[i].Value:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine($"ROCR period={period} validated against TALib");
    }

    #endregion

    #region Tulip Validation

    [Fact]
    public void Rocr_MatchesTulip_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCR
        var rocr = new Rocr(TestPeriod);
        var qResult = rocr.Update(_testData.Data);

        // Tulip rocr
        var rocrIndicator = Tulip.Indicators.rocr;
        double[][] inputs = [tData];
        double[] options = [TestPeriod];
        int lookback = rocrIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        rocrIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        // Compare after lookback
        ValidationHelper.VerifyData(qResult, tulipResult, lookback);

        _output.WriteLine("ROCR Batch validated successfully against Tulip");
    }

    [Fact]
    public void Rocr_MatchesTulip_Streaming()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCR (streaming)
        var rocr = new Rocr(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(rocr.Update(item).Value);
        }

        // Tulip rocr
        var rocrIndicator = Tulip.Indicators.rocr;
        double[][] inputs = [tData];
        double[] options = [TestPeriod];
        int lookback = rocrIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        rocrIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qResults, tulipResult, lookback);

        _output.WriteLine("ROCR Streaming validated successfully against Tulip");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(20)]
    public void Rocr_MatchesTulip_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        var rocr = new Rocr(period);
        var qResult = rocr.Update(_testData.Data);

        var rocrIndicator = Tulip.Indicators.rocr;
        double[][] inputs = [tData];
        double[] options = [period];
        int lookback = rocrIndicator.Start(options);
        double[][] outputs = [new double[tData.Length - lookback]];

        rocrIndicator.Run(inputs, options, outputs);
        double[] tulipResult = outputs[0];

        ValidationHelper.VerifyData(qResult, tulipResult, lookback);
    }

    #endregion

    #region Mathematical Validation

    [Fact]
    public void Rocr_ManualCalculation_MatchesExpected()
    {
        var rocr = new Rocr(3);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 115, 120, 125 };

        for (int i = 0; i < values.Length; i++)
        {
            var result = rocr.Update(new TValue(time.AddSeconds(i), values[i]), true);

            if (i >= 3)
            {
                double expected = values[i] / values[i - 3];
                Assert.Equal(expected, result.Value, 10);
            }
            else
            {
                Assert.Equal(1.0, result.Value, 10);
            }
        }
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        var source = _testData.Data;

        // Streaming
        var streamingRocr = new Rocr(TestPeriod);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults.Add(streamingRocr.Update(source[i]).Value);
        }

        // Batch
        var batchRocr = new Rocr(TestPeriod);
        var batchResult = batchRocr.Update(source);

        int count = source.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("ROCR Batch vs Streaming consistency validated");
    }

    #endregion
}
