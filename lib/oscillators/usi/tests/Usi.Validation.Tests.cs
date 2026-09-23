namespace QuanTAlib.Tests;

/// <summary>
/// Formula validation for USI (Ehlers Ultimate Strength Index). No external library implements
/// USI (a 2024 Ehlers indicator), so this test independently re-derives the documented pipeline
/// (SU/SD -&gt; SMA(4) -&gt; UltimateSmoother -&gt; normalize) using the already-validated <see cref="Usf"/>
/// filter as the smoothing stage, and checks it against Usi's streaming output bar-by-bar.
/// </summary>
public sealed class UsiValidationTests : IDisposable
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
    [InlineData(14)]
    [InlineData(28)]
    public void Usi_MatchesIndependentFormulaReimplementation(int period)
    {
        double[] close = _testData.RawData.ToArray();
        var usi = new Usi(period);

        // Independent re-derivation of the SU/SD -> SMA(4) -> UltimateSmoother -> normalize pipeline.
        var suHistory = new List<double>();
        var sdHistory = new List<double>();
        var usfSu = new Usf(period);
        var usfSd = new Usf(period);

        double? prevClose = null;
        int count = close.Length;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = 0; i < count; i++)
        {
            double qlValue = usi.Update(new TValue(DateTime.UtcNow.AddSeconds(i), close[i])).Value;

            if (prevClose is null)
            {
                prevClose = close[i];
                continue;
            }

            double diff = close[i] - prevClose.Value;
            prevClose = close[i];

            double su = diff > 0 ? diff : 0.0;
            double sd = diff < 0 ? -diff : 0.0;

            suHistory.Add(su);
            sdHistory.Add(sd);
            if (suHistory.Count > 4)
            {
                suHistory.RemoveAt(0);
                sdHistory.RemoveAt(0);
            }

            double avgSu = suHistory.Sum() / 4.0; // matches Usi's fixed /4 (not /count) during warmup
            double avgSd = sdHistory.Sum() / 4.0;

            double usu = usfSu.Update(new TValue(DateTime.UtcNow, avgSu)).Value;
            double usd = usfSd.Update(new TValue(DateTime.UtcNow, avgSd)).Value;

            // Usi bootstraps with raw (unfiltered) averages for the first 8 bars.
            bool bootstrap = i + 1 < 8; // Usi's _s.Count is 1-based and includes the seed bar
            double expectedUsu = bootstrap ? avgSu : usu;
            double expectedUsd = bootstrap ? avgSd : usd;

            double denom = expectedUsu + expectedUsd;
            double expected = denom > 0.01 ? Math.Clamp((expectedUsu - expectedUsd) / denom, -1.0, 1.0) : double.NaN;

            if (i >= start && double.IsFinite(expected))
            {
                Assert.Equal(expected, qlValue, precision: 8);
            }
        }
    }

    [Fact]
    public void Usi_OutputAlwaysBoundedWithinUnitRange()
    {
        double[] close = _testData.RawData.ToArray();
        var usi = new Usi(28);

        for (int i = 0; i < close.Length; i++)
        {
            var result = usi.Update(new TValue(DateTime.UtcNow.AddSeconds(i), close[i]));
            Assert.InRange(result.Value, -1.0, 1.0);
        }
    }

    [Fact]
    public void Usi_BatchMatchesStreaming()
    {
        var source = _testData.Data;
        var batch = Usi.Batch(source, 28);

        var streaming = new Usi(28);
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
