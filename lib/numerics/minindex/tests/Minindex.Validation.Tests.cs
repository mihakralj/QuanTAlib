using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for MININDEX (Rolling Minimum Index) against TA-Lib.
/// MININDEX returns the absolute array index of the lowest value within a rolling window.
/// Tie-breaking (last occurrence wins) matches TA-Lib's `tmp &lt;= lowest` rule exactly,
/// so QuanTAlib and TA-Lib should agree bit-for-bit once past the lookback.
/// </summary>
public sealed class MinindexValidationTests(ITestOutputHelper output) : IDisposable
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

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(50)]
    public void Minindex_MatchesTalib_Span(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib MININDEX (Span, absolute array indices)
        double[] qOutput = new double[tData.Length];
        Minindex.Batch(tData.AsSpan(), qOutput.AsSpan(), period);

        // TA-Lib MININDEX
        int[] tOutput = new int[tData.Length];
        var retCode = TALib.Functions.MinIndex<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MinIndexLookback(period);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        int compared = 0;
        for (int i = lookback; i < tData.Length; i++)
        {
            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length)
            {
                continue;
            }

            Assert.Equal(tOutput[tIndex], (int)qOutput[i]);
            compared++;
        }

        Assert.True(compared > 100, $"Only {compared} values compared for period={period}");
        _output.WriteLine($"MININDEX period={period}: {compared} values matched TALib exactly");
    }

    [Fact]
    public void Minindex_MatchesTalib_Streaming()
    {
        const int period = 14;
        double[] tData = _testData.RawData.ToArray();

        int[] tOutput = new int[tData.Length];
        var retCode = TALib.Functions.MinIndex<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MinIndexLookback(period);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        // QuanTAlib MININDEX (streaming): bars-ago offset, converted to absolute index for comparison.
        var minindex = new Minindex(period);
        int compared = 0;
        for (int i = 0; i < tData.Length; i++)
        {
            var result = minindex.Update(new TValue(DateTime.UtcNow.AddSeconds(i), tData[i]), true);

            if (i < lookback)
            {
                continue;
            }

            int tIndex = i - offset;
            if (tIndex < 0 || tIndex >= length)
            {
                continue;
            }

            int absoluteIndex = i - (int)result.Value;
            Assert.Equal(tOutput[tIndex], absoluteIndex);
            compared++;
        }

        Assert.True(compared > 100, $"Only {compared} values compared");
        _output.WriteLine($"MININDEX streaming: {compared} values matched TALib exactly");
    }
}
