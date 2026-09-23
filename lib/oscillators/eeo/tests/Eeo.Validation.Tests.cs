namespace QuanTAlib.Tests;

/// <summary>
/// Formula validation for EEO (Ehlers Elegant Oscillator). No external library implements EEO,
/// so this test independently re-derives the documented pipeline (2-bar momentum -&gt; rolling RMS(50)
/// normalization -&gt; Inverse Fisher Transform -&gt; 2-pole Super Smoother) and checks it bar-by-bar
/// against the streaming implementation.
/// </summary>
public sealed class EeoValidationTests : IDisposable
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
    [InlineData(10)]
    [InlineData(20)]
    public void Eeo_MatchesIndependentFormulaReimplementation(int bandEdge)
    {
        double[] close = _testData.RawData.ToArray();
        var eeo = new Eeo(bandEdge);

        // Super Smoother (2-pole Butterworth) coefficients, per Ehlers' formula.
        double a1 = Math.Exp(-1.414 * Math.PI / bandEdge);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / bandEdge);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        const int rmsWindow = 50;
        var derivSqWindow = new Queue<double>(rmsWindow);
        double sumSquared = 0;

        double src1 = 0, src2 = 0;
        double iFish1 = 0, ss1 = 0, ss = 0;

        int count = close.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = 0; i < count; i++)
        {
            double qlValue = eeo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), close[i])).Value;

            double deriv = close[i] - src2;
            double derivSq = deriv * deriv;

            if (derivSqWindow.Count == rmsWindow)
            {
                sumSquared -= derivSqWindow.Dequeue();
            }
            derivSqWindow.Enqueue(derivSq);
            sumSquared += derivSq;

            double rms = Math.Sqrt(Math.Max(sumSquared / rmsWindow, 1e-10));
            double nDeriv = rms > 1e-10 ? deriv / rms : 0.0;
            double iFish = Math.Tanh(nDeriv);

            double newSs = i + 1 <= 2 ? 0.0 : ((c1 * 0.5) * (iFish + iFish1)) + (c2 * ss) + (c3 * ss1);

            if (i >= start)
            {
                Assert.Equal(newSs, qlValue, precision: 6);
            }

            iFish1 = iFish;
            ss1 = ss;
            ss = newSs;
            src2 = src1;
            src1 = close[i];
        }
    }

    [Fact]
    public void Eeo_BatchMatchesStreaming()
    {
        var source = _testData.Data;
        var batch = Eeo.Batch(source, 20);

        var streaming = new Eeo(20);
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
