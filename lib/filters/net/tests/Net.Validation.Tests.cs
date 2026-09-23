namespace QuanTAlib.Tests;

/// <summary>
/// Validation for NET (Ehlers Noise Elimination Technology / Kendall Tau-a).
/// Kendall's Tau-a is a well-known rank-correlation statistic; this test computes it
/// independently via a brute-force O(n^2) pairwise sign comparison over the same rolling
/// window and checks it against NET's streaming output.
/// </summary>
public sealed class NetValidationTests : IDisposable
{
    private readonly ValidationTestData _testData = new();
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

    // Independent brute-force Kendall Tau-a over window[start..end), newest last.
    private static double KendallTauA(IReadOnlyList<double> window)
    {
        int n = window.Count;
        if (n < 2)
        {
            return 0.0;
        }

        int concordantMinusDiscordant = 0;
        for (int a = 0; a < n; a++)
        {
            for (int b = a + 1; b < n; b++)
            {
                double diff = window[b] - window[a]; // b is newer than a
                concordantMinusDiscordant += Math.Sign(diff);
            }
        }

        double denom = 0.5 * n * (n - 1);
        return concordantMinusDiscordant / denom;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(30)]
    public void Net_MatchesIndependentKendallTauA(int period)
    {
        double[] data = _testData.RawData.ToArray();
        var net = new Net(period);

        int count = data.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = 0; i < count; i++)
        {
            double qlValue = net.Update(new TValue(DateTime.UtcNow.AddSeconds(i), data[i])).Value;

            if (i < start)
            {
                continue;
            }

            int windowStart = Math.Max(0, i - period + 1);
            var window = data[windowStart..(i + 1)];
            double expected = window.Length < 2 ? 0.0 : KendallTauA(window);

            Assert.Equal(expected, qlValue, precision: 8);
        }
    }

    [Fact]
    public void Net_OutputAlwaysBoundedWithinUnitRange()
    {
        double[] data = _testData.RawData.ToArray();
        var net = new Net(14);

        foreach (double v in data)
        {
            var result = net.Update(new TValue(DateTime.UtcNow, v));
            Assert.InRange(result.Value, -1.0, 1.0);
        }
    }

    [Fact]
    public void Net_BatchMatchesStreaming()
    {
        var source = _testData.Data;
        var batch = Net.Batch(source, 14);

        var streaming = new Net(14);
        int count = source.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = 0; i < count; i++)
        {
            double v = streaming.Update(source[i]).Value;
            if (i >= start)
            {
                Assert.Equal(batch[i].Value, v, precision: 10);
            }
        }
    }
}
