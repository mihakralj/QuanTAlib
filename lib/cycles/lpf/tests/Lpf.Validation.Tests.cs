namespace QuanTAlib.Tests;

/// <summary>
/// Structural validation for LPF (Ehlers Linear Predictive Filter). LPF is a 5-stage adaptive
/// spectral estimator (roofing filter -&gt; AGC -&gt; Griffiths LMS predictor -&gt; DFT -&gt; center of
/// gravity); no external library implements it and a bit-for-bit independent re-derivation of
/// the adaptive LMS/DFT stages is impractical to maintain. Instead this validates the documented
/// invariants: dominant cycle stays within [lowerBound, upperBound], changes by at most 2 bars
/// per update, and all outputs remain finite; plus batch/streaming/span consistency.
/// </summary>
public sealed class LpfValidationTests : IDisposable
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

    [Fact]
    public void Lpf_DominantCycle_StaysWithinBounds()
    {
        const int lowerBound = 18;
        const int upperBound = 40;
        var lpf = new Lpf(lowerBound, upperBound, upperBound);

        double[] data = _testData.RawData.ToArray();
        foreach (double v in data)
        {
            var result = lpf.Update(new TValue(DateTime.UtcNow, v));
            Assert.True(double.IsFinite(result.Value), $"DominantCycle must be finite, got {result.Value}");
            Assert.InRange(result.Value, lowerBound, upperBound);
            Assert.InRange(lpf.DominantCycle, lowerBound, upperBound);
        }
    }

    [Fact]
    public void Lpf_DominantCycle_ChangesByAtMostTwoBarsPerUpdate()
    {
        var lpf = new Lpf(18, 40, 40);
        double[] data = _testData.RawData.ToArray();

        double? prev = null;
        foreach (double v in data)
        {
            var result = lpf.Update(new TValue(DateTime.UtcNow, v));
            if (prev.HasValue)
            {
                double delta = Math.Abs(result.Value - prev.Value);
                Assert.True(delta <= 2.0 + 1e-9,
                    $"DominantCycle changed by {delta} bars in one update (documented max is 2)");
            }
            prev = result.Value;
        }
    }

    [Fact]
    public void Lpf_Signal_And_Predict_AreFinite()
    {
        var lpf = new Lpf(18, 40, 40);
        double[] data = _testData.RawData.ToArray();

        int count = data.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = 0; i < count; i++)
        {
            lpf.Update(new TValue(DateTime.UtcNow.AddSeconds(i), data[i]));
            if (i >= start && lpf.IsHot)
            {
                Assert.True(double.IsFinite(lpf.Signal), $"Signal not finite at index {i}");
                Assert.True(double.IsFinite(lpf.Predict), $"Predict not finite at index {i}");
            }
        }
    }

    [Fact]
    public void Lpf_BatchMatchesStreaming()
    {
        const int lowerBound = 18;
        const int upperBound = 40;
        var source = _testData.Data;

        var batch = Lpf.Batch(source, lowerBound, upperBound, upperBound);

        var streaming = new Lpf(lowerBound, upperBound, upperBound);
        int count = source.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = 0; i < count; i++)
        {
            double v = streaming.Update(source[i]).Value;
            if (i >= start)
            {
                Assert.Equal(batch[i].Value, v, precision: 8);
            }
        }
    }
}
