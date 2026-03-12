namespace QuanTAlib.Tests;

public sealed class SakValidationTests
{
    // ── EMA cross-validation ──────────────────────────────────────────────

    [Fact]
    public void Sak_EMA_BatchEqualsStreaming()
    {
        // SAK "EMA" uses Ehlers' trig alpha (cos+sin-1)/cos, which differs from
        // the classic 2/(P+1) formula used by standalone Ema. Cross-library
        // comparison is not valid. Verify internal self-consistency instead.
        const int period = 14;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var sakStreaming = new Sak("EMA", period: period);
        for (int i = 0; i < series.Count; i++)
        {
            sakStreaming.Update(series[i]);
        }

        var (batchResult, _) = Sak.Calculate(series, "EMA", period);

        Assert.Equal(batchResult.Last.Value, sakStreaming.Last.Value, 1e-10);
    }

    [Fact]
    public void Sak_EMA_BatchMatchesStreaming()
    {
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 99);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var batchResult = Sak.Calculate(series, "EMA", period).Results;

        var streaming = new Sak("EMA", period);
        for (int i = 0; i < series.Count; i++)
        {
            streaming.Update(series[i]);
        }

        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 1e-13);
    }

    // ── Gauss cross-validation ────────────────────────────────────────────

    [Fact]
    public void Sak_Gauss_BatchMatchesStreaming()
    {
        const int period = 20;
        var gbm = new GBM(startPrice: 100, mu: 0.03, sigma: 0.2, seed: 77);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var batchResult = Sak.Calculate(series, "Gauss", period).Results;

        var streaming = new Sak("Gauss", period);
        for (int i = 0; i < series.Count; i++)
        {
            streaming.Update(series[i]);
        }

        Assert.Equal(batchResult.Last.Value, streaming.Last.Value, 1e-13);
    }

    // ── Smooth mode FIR verification ──────────────────────────────────────

    [Fact]
    public void Sak_Smooth_FIR_VerifyThreeBar()
    {
        // Smooth: a1=a2=0, so y[t] = c0 * (b0*x[t] + b1*x[t-1] + b2*x[t-2])
        //                          = (alpha^2/4) * (x[t] + 2*x[t-1] + x[t-2])
        // For period=10:
        //   theta = 2π/10, alpha = (cos(theta)+sin(theta)-1)/cos(theta)
        const int period = 10;
        double theta = 2.0 * Math.PI / period;
        double cosT = Math.Cos(theta);
        double sinT = Math.Sin(theta);
        double alpha = (cosT + sinT - 1.0) / cosT;
        double c0 = alpha * alpha / 4.0;

        double x0 = 10.0, x1 = 20.0, x2 = 30.0;
        double expectedY = c0 * (x0 + (2.0 * x1) + x2);   // pure FIR formula

        var sak = new Sak("Smooth", period: period);
        var now = DateTime.UtcNow;
        sak.Update(new TValue(now, x2));           // oldest first
        sak.Update(new TValue(now.AddSeconds(1), x1));
        var result = sak.Update(new TValue(now.AddSeconds(2), x0));

        Assert.Equal(expectedY, result.Value, 1e-12);
    }

    // ── SMA cross-validation ──────────────────────────────────────────────

    [Fact]
    public void Sak_SMA_MatchesStandaloneSma()
    {
        const int n = 15;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 55);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var sakSma = new Sak("SMA", period: 20, n: n);
        var standaloneSma = new Sma(n);

        for (int i = 0; i < series.Count; i++)
        {
            sakSma.Update(series[i]);
            standaloneSma.Update(series[i]);
        }

        // SAK SMA uses RingBuffer exact windowed sum; standalone Sma uses
        // compensated running-sum — both are O(1) but accumulate FP error
        // differently. Tolerance 1e-10 covers the rounding gap.
        Assert.Equal(standaloneSma.Last.Value, sakSma.Last.Value, 1e-10);
    }

    // ── Internal consistency: constant input ─────────────────────────────

    [Theory]
    [InlineData("EMA")]
    [InlineData("Gauss")]
    [InlineData("Butter")]
    [InlineData("SMA")]
    public void Sak_LowPassModes_ConstantInput_ConvergesToConstant(string mode)
    {
        const double constVal = 123.456;
        int n = string.Equals(mode, "SMA", StringComparison.Ordinal) ? 10 : 5;
        var sak = new Sak(mode, period: 10, n: n, delta: 0.1);
        var now = DateTime.UtcNow;

        TValue last = default;
        for (int i = 0; i < 500; i++)
        {
            last = sak.Update(new TValue(now.AddSeconds(i), constVal));
        }

        Assert.Equal(constVal, last.Value, 1e-4);
    }

    // ── BP/BS: DC rejection ───────────────────────────────────────────────

    [Fact]
    public void Sak_BP_ConstantInput_ConvergesToZero()
    {
        // BP is a bandpass filter: DC (zero-frequency) input is in the stop-band.
        // Ehlers' BP IIR needs ~5*period bars to fully attenuate the DC transient.
        var sak = new Sak("BP", period: 20, delta: 0.1);
        var now = DateTime.UtcNow;
        TValue last = default;
        for (int i = 0; i < 2000; i++)
        {
            last = sak.Update(new TValue(now.AddSeconds(i), 100.0));
        }
        Assert.True(Math.Abs(last.Value) < 1e-3, $"BP DC not rejected after 2000 bars: {last.Value}");
    }

    [Fact]
    public void Sak_HP_ConstantInput_ConvergesToZero()
    {
        var sak = new Sak("HP", period: 20);
        var now = DateTime.UtcNow;
        TValue last = default;
        for (int i = 0; i < 500; i++)
        {
            last = sak.Update(new TValue(now.AddSeconds(i), 100.0));
        }
        Assert.True(Math.Abs(last.Value) < 1e-3, $"HP DC not rejected: {last.Value}");
    }

    // ── Span == Streaming consistency ─────────────────────────────────────

    [Theory]
    [InlineData("EMA")]
    [InlineData("BP")]
    [InlineData("Butter")]
    [InlineData("SMA")]
    public void Sak_Span_MatchesStreaming(string mode)
    {
        const int period = 12;
        const int n = 6;
        var gbm = new GBM(startPrice: 100, mu: 0.04, sigma: 0.18, seed: 333);
        var bars = gbm.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var srcArr = series.Values.ToArray();
        var outArr = new double[srcArr.Length];
        Sak.Calculate(srcArr.AsSpan(), outArr.AsSpan(), mode, period, n);

        var streaming = new Sak(mode, period, n);
        for (int i = 0; i < series.Count; i++)
        {
            streaming.Update(series[i]);
        }

        Assert.Equal(streaming.Last.Value, outArr[^1], 1e-9);
    }
}
