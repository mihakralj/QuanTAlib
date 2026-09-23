namespace QuanTAlib.Tests;

/// <summary>
/// Formula validation for PTA (Ehlers Precision Trend Analysis). No external library implements
/// this dual-highpass bandpass approach, so this test independently re-derives the documented
/// pipeline (two 2-pole Butterworth highpass filters on the 2nd-order price difference,
/// subtracted) and checks it bar-by-bar against the streaming implementation.
/// </summary>
public sealed class PtaValidationTests : IDisposable
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

    private static (double c1, double c2, double c3) HpCoefficients(int period)
    {
        double f = 1.414 * Math.PI / period;
        double a1 = Math.Exp(-f);
        double b1 = 2.0 * a1 * Math.Cos(f);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = (1.0 + c2 - c3) * 0.25;
        return (c1, c2, c3);
    }

    [Theory]
    [InlineData(250, 40)]
    [InlineData(100, 20)]
    public void Pta_MatchesIndependentFormulaReimplementation(int longPeriod, int shortPeriod)
    {
        double[] data = _testData.RawData.ToArray();
        var pta = new Pta(longPeriod, shortPeriod);

        var (c1L, c2L, c3L) = HpCoefficients(longPeriod);
        var (c1S, c2S, c3S) = HpCoefficients(shortPeriod);

        double src1 = 0, src2 = 0;
        double hp1 = 0, hp1Prev = 0;
        double hp2 = 0, hp2Prev = 0;

        int count = data.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = 0; i < count; i++)
        {
            double qlValue = pta.Update(new TValue(DateTime.UtcNow.AddSeconds(i), data[i])).Value;

            double expected;
            if (i < 2)
            {
                expected = 0.0;
                if (i == 0)
                {
                    src1 = data[i];
                    src2 = data[i];
                }
                else
                {
                    src2 = src1;
                    src1 = data[i];
                }
            }
            else
            {
                double diff = data[i] - (2.0 * src1) + src2;

                double newHp1 = (c1L * diff) + (c2L * hp1) + (c3L * hp1Prev);
                double newHp2 = (c1S * diff) + (c2S * hp2) + (c3S * hp2Prev);

                expected = newHp1 - newHp2;

                hp1Prev = hp1; hp1 = newHp1;
                hp2Prev = hp2; hp2 = newHp2;
                src2 = src1; src1 = data[i];
            }

            if (i >= start)
            {
                Assert.Equal(expected, qlValue, precision: 8);
            }
        }
    }

    [Fact]
    public void Pta_BatchMatchesStreaming()
    {
        var source = _testData.Data;
        var batch = Pta.Batch(source, 250, 40);

        var streaming = new Pta(250, 40);
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
