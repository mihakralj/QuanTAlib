using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for LAG against a direct reference implementation of x[t-n]
/// (Pine Script's history-referencing operator).
/// </summary>
public class LagValidationTests
{
    private readonly GBM _gbm = new(sigma: 0.5, mu: 0.0, seed: 47);
    private const double Tolerance = 1e-10;

    private static double[] ReferenceLag(double[] source, int n)
    {
        var expected = new double[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            expected[i] = i >= n ? source[i - n] : 0.0;
        }
        return expected;
    }

    [Fact]
    public void Lag_Batch_MatchesReference()
    {
        var series = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        int n = 7;

        var result = Lag.Batch(series, n);
        var expected = ReferenceLag(series.Values.ToArray(), n);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(expected[i], result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Lag_Streaming_MatchesReference()
    {
        var series = _gbm.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        int n = 3;
        var expected = ReferenceLag(series.Values.ToArray(), n);

        var lag = new Lag(n);
        for (int i = 0; i < series.Count; i++)
        {
            var r = lag.Update(series[i], true);
            Assert.Equal(expected[i], r.Value, Tolerance);
        }
    }

    [Fact]
    public void Lag_Composed_WithSub_ProducesMomentum()
    {
        // close - close[n] is the textbook definition of Momentum (see momentum/Mom).
        var series = _gbm.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        int n = 10;

        var lagged = Lag.Batch(series, n);
        var momViaCompose = Sub.Batch(series, lagged);
        var momDirect = Mom.Batch(series, n);

        // Both are zero during warmup by convention; compare the hot region.
        for (int i = n + 1; i < series.Count; i++)
        {
            Assert.Equal(momDirect[i].Value, momViaCompose[i].Value, Tolerance);
        }
    }
}
