using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ROCP (Rate of Change Percentage) against external libraries.
/// ROCP = 100 × (Price - Price[N]) / Price[N]
///
/// Note: TALib's RocP returns a decimal fraction (0.05 for 5%), while QuanTAlib returns
/// a percentage (5.0 for 5%). Tests account for this scaling difference.
/// Tulip does not have a direct ROCP indicator.
/// </summary>
public sealed class RocpValidationTests(ITestOutputHelper output) : IDisposable
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

    private const int TestPeriod = 10;

    #region TALib Validation

    [Fact]
    public void Rocp_MatchesTalib_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCP (batch TSeries)
        var rocp = new Rocp(TestPeriod);
        var qResult = rocp.Update(_testData.Data);

        // TALib RocP (returns decimal fraction)
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocP<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocPLookback(TestPeriod);

        // Compare: TALib returns decimal, QuanTAlib returns percentage → multiply TALib by 100
        int count = qResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback)
            {
                continue;
            }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length)
            {
                continue;
            }

            double talibScaled = tOutput[tIndex] * 100.0;
            Assert.True(
                Math.Abs(qResult[i].Value - talibScaled) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResult[i].Value:G17}, TALib(×100)={talibScaled:G17}");
        }
        _output.WriteLine("ROCP Batch validated successfully against TALib");
    }

    [Fact]
    public void Rocp_MatchesTalib_Span()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCP (Span)
        double[] qOutput = new double[tData.Length];
        Rocp.Batch(tData.AsSpan(), qOutput.AsSpan(), TestPeriod);

        // TALib RocP
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocP<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocPLookback(TestPeriod);

        int count = qOutput.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback)
            {
                continue;
            }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length)
            {
                continue;
            }

            double talibScaled = tOutput[tIndex] * 100.0;
            Assert.True(
                Math.Abs(qOutput[i] - talibScaled) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qOutput[i]:G17}, TALib(×100)={talibScaled:G17}");
        }
        _output.WriteLine("ROCP Span validated successfully against TALib");
    }

    [Fact]
    public void Rocp_MatchesTalib_Streaming()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROCP (streaming)
        var rocp = new Rocp(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(rocp.Update(item).Value);
        }

        // TALib RocP
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocP<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocPLookback(TestPeriod);

        int count = qResults.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        for (int i = start; i < count; i++)
        {
            if (i < lookback)
            {
                continue;
            }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length)
            {
                continue;
            }

            double talibScaled = tOutput[tIndex] * 100.0;
            Assert.True(
                Math.Abs(qResults[i] - talibScaled) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, TALib(×100)={talibScaled:G17}");
        }
        _output.WriteLine("ROCP Streaming validated successfully against TALib");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(50)]
    public void Rocp_MatchesTalib_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        var rocp = new Rocp(period);
        var qResult = rocp.Update(_testData.Data);

        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.RocP<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocPLookback(period);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        int count = qResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            if (i < lookback)
            {
                continue;
            }
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length)
            {
                continue;
            }

            double talibScaled = tOutput[tIndex] * 100.0;
            Assert.True(
                Math.Abs(qResult[i].Value - talibScaled) <= ValidationHelper.TalibTolerance,
                $"Period {period}, index {i}: QuanTAlib={qResult[i].Value:G17}, TALib(×100)={talibScaled:G17}");
        }
        _output.WriteLine($"ROCP period={period} validated against TALib");
    }

    #endregion

    #region Mathematical Validation

    [Fact]
    public void Rocp_ManualCalculation_MatchesExpected()
    {
        var rocp = new Rocp(3);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 115, 120, 125 };

        for (int i = 0; i < values.Length; i++)
        {
            var result = rocp.Update(new TValue(time.AddSeconds(i), values[i]), true);

            if (i >= 3)
            {
                double expected = 100.0 * (values[i] - values[i - 3]) / values[i - 3];
                Assert.Equal(expected, result.Value, 10);
            }
            else
            {
                Assert.Equal(0.0, result.Value, 10);
            }
        }
    }

    [Fact]
    public void Batch_MatchesStreaming_IdenticalResults()
    {
        var source = _testData.Data;

        // Streaming
        var streamingRocp = new Rocp(TestPeriod);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults.Add(streamingRocp.Update(source[i]).Value);
        }

        // Batch
        var batchRocp = new Rocp(TestPeriod);
        var batchResult = batchRocp.Update(source);

        int count = source.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("ROCP Batch vs Streaming consistency validated");
    }

    #endregion
}
