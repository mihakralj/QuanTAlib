using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ROC (Rate of Change Percentage) against external libraries.
/// ROC = 100 × (Price - Price[N]) / Price[N]
///
/// QuanTAlib's Roc matches TA-Lib's ROC function directly (both return percentage, e.g. 5.0 = 5%).
/// Tulip does not have a direct ROC indicator.
/// </summary>
public sealed class RocValidationTests(ITestOutputHelper output) : IDisposable
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
    public void Roc_MatchesTalib_Batch()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROC (batch TSeries)
        var roc = new Roc(TestPeriod);
        var qResult = roc.Update(_testData.Data);

        // TALib ROC (returns percentage, matching QuanTAlib directly)
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Roc<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocLookback(TestPeriod);

        // Compare directly — both QuanTAlib and TA-Lib return percentage form
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

            Assert.True(
                Math.Abs(qResult[i].Value - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResult[i].Value:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine("ROC Batch validated successfully against TALib");
    }

    [Fact]
    public void Roc_MatchesTalib_Span()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROC (Span)
        double[] qOutput = new double[tData.Length];
        Roc.Batch(tData.AsSpan(), qOutput.AsSpan(), TestPeriod);

        // TALib ROC (returns percentage, matching QuanTAlib directly)
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Roc<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocLookback(TestPeriod);

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

            Assert.True(
                Math.Abs(qOutput[i] - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qOutput[i]:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine("ROC Span validated successfully against TALib");
    }

    [Fact]
    public void Roc_MatchesTalib_Streaming()
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib ROC (streaming)
        var roc = new Roc(TestPeriod);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(roc.Update(item).Value);
        }

        // TALib ROC (returns percentage, matching QuanTAlib directly)
        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Roc<double>(tData, 0..^0, tOutput, out var outRange, TestPeriod);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocLookback(TestPeriod);

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

            Assert.True(
                Math.Abs(qResults[i] - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Mismatch at index {i}: QuanTAlib={qResults[i]:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine("ROC Streaming validated successfully against TALib");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(50)]
    public void Roc_MatchesTalib_DifferentPeriods(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        var roc = new Roc(period);
        var qResult = roc.Update(_testData.Data);

        double[] tOutput = new double[tData.Length];
        var retCode = TALib.Functions.Roc<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.RocLookback(period);
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

            Assert.True(
                Math.Abs(qResult[i].Value - tOutput[tIndex]) <= ValidationHelper.TalibTolerance,
                $"Period {period}, index {i}: QuanTAlib={qResult[i].Value:G17}, TALib={tOutput[tIndex]:G17}");
        }
        _output.WriteLine($"ROC period={period} validated against TALib");
    }

    #endregion

    #region Mathematical Validation

    [Fact]
    public void Roc_ManualCalculation_MatchesExpected()
    {
        var roc = new Roc(3);
        var time = DateTime.UtcNow;

        var values = new double[] { 100, 105, 110, 115, 120, 125 };

        for (int i = 0; i < values.Length; i++)
        {
            var result = roc.Update(new TValue(time.AddSeconds(i), values[i]), true);

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
        var streamingRoc = new Roc(TestPeriod);
        var streamingResults = new List<double>();
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults.Add(streamingRoc.Update(source[i]).Value);
        }

        // Batch
        var batchRoc = new Roc(TestPeriod);
        var batchResult = batchRoc.Update(source);

        int count = source.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResults[i], ValidationHelper.DefaultTolerance);
        }
        _output.WriteLine("ROC Batch vs Streaming consistency validated");
    }

    #endregion
}
