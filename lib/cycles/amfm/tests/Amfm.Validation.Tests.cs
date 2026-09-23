namespace QuanTAlib.Tests;

/// <summary>
/// Formula validation for AMFM (Ehlers AM Detector / FM Demodulator). No external library
/// implements this DSP-based decomposition, so this test independently re-derives the
/// documented pipeline (rolling max(|deriv|,4) -&gt; SMA(8) for AM; hard-limit(10x) -&gt; Super
/// Smoother for FM) and checks it bar-by-bar against the streaming implementation.
/// </summary>
public sealed class AmfmValidationTests : IDisposable
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

    [Theory]
    [InlineData(30)]
    [InlineData(15)]
    public void Amfm_MatchesIndependentFormulaReimplementation(int period)
    {
        double a1 = Math.Exp(-1.414 * Math.PI / period);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / period);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        var amfm = new Amfm(period);
        var envBuf = new double[4];
        var smaBuf = new double[8];
        double smaSum = 0;
        int envIdx = 0, smaIdx = 0;

        double ssPrev2 = 0, ss = 0, hlPrev = 0;

        var bars = _testData.Bars;
        int count = bars.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = 0; i < count; i++)
        {
            var result = amfm.Update(bars[i], isNew: true);

            double deriv = bars[i].Close - bars[i].Open;

            // AM: rolling max(|deriv|,4) -> SMA(8)
            double absDeriv = Math.Abs(deriv);
            envBuf[envIdx] = absDeriv;
            envIdx = (envIdx + 1) & 3;
            double envel = Math.Max(Math.Max(envBuf[0], envBuf[1]), Math.Max(envBuf[2], envBuf[3]));

            double oldSma = smaBuf[smaIdx];
            smaBuf[smaIdx] = envel;
            smaIdx = (smaIdx + 1) & 7;
            smaSum = smaSum - oldSma + envel;
            int smaCount = Math.Min(i + 1, 8);
            double expectedAm = smaCount > 0 ? smaSum / smaCount : 0.0;

            // FM: hard-limit(10x) -> Super Smoother
            double hl = Math.Clamp(10.0 * deriv, -1.0, 1.0);
            double expectedFm;
            if (i + 1 <= 2)
            {
                expectedFm = deriv;
            }
            else
            {
                expectedFm = (c1 * (hl + hlPrev) * 0.5) + (c2 * ss) + (c3 * ssPrev2);
            }

            ssPrev2 = ss;
            ss = expectedFm;
            hlPrev = hl;

            if (i >= start)
            {
                Assert.Equal(expectedAm, amfm.Am, precision: 8);
                Assert.Equal(expectedFm, amfm.Fm, precision: 8);
                Assert.Equal(expectedFm, result.Value, precision: 8);
            }
        }
    }

    [Fact]
    public void Amfm_AmIsAlwaysNonNegative()
    {
        var amfm = new Amfm(30);
        var bars = _testData.Bars;
        for (int i = 0; i < bars.Count; i++)
        {
            amfm.Update(bars[i], isNew: true);
            Assert.True(amfm.Am >= 0, $"AM should be non-negative, got {amfm.Am} at index {i}");
        }
    }
}
