using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for MAXINDEX (Rolling Maximum Index) against TA-Lib.
/// MAXINDEX returns the absolute array index of the highest value within a rolling window.
/// Tie-breaking (last occurrence wins) matches TA-Lib's `tmp &gt;= highest` rule exactly,
/// so QuanTAlib and TA-Lib should agree bit-for-bit once past the lookback.
/// </summary>
public sealed class MaxindexValidationTests(ITestOutputHelper output) : IDisposable
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
    public void Maxindex_MatchesTalib_Span(int period)
    {
        double[] tData = _testData.RawData.ToArray();

        // QuanTAlib MAXINDEX (Span, absolute array indices)
        double[] qOutput = new double[tData.Length];
        Maxindex.Batch(tData.AsSpan(), qOutput.AsSpan(), period);

        // TA-Lib MAXINDEX
        int[] tOutput = new int[tData.Length];
        var retCode = TALib.Functions.MaxIndex<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MaxIndexLookback(period);
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
        _output.WriteLine($"MAXINDEX period={period}: {compared} values matched TALib exactly");
    }

    [Fact]
    public void Maxindex_MatchesTalib_Streaming()
    {
        const int period = 14;
        double[] tData = _testData.RawData.ToArray();

        int[] tOutput = new int[tData.Length];
        var retCode = TALib.Functions.MaxIndex<double>(tData, 0..^0, tOutput, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = TALib.Functions.MaxIndexLookback(period);
        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);

        // QuanTAlib MAXINDEX (streaming): bars-ago offset, converted to absolute index for comparison.
        var maxindex = new Maxindex(period);
        int compared = 0;
        for (int i = 0; i < tData.Length; i++)
        {
            var result = maxindex.Update(new TValue(DateTime.UtcNow.AddSeconds(i), tData[i]), true);

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
        _output.WriteLine($"MAXINDEX streaming: {compared} values matched TALib exactly");
    }
}
