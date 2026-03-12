using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for RVGI.
/// RVGI is not implemented by TA-Lib, Skender, Tulip, or Ooples,
/// so validation uses streaming == batch == span mode consistency
/// plus mathematical identity checks.
/// </summary>
public sealed class RvgiValidationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const double Tolerance = 1e-12;

    // ───── Self-consistency: streaming == batch span ─────

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period10()
    {
        const int N = 200;
        const int period = 10;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            opens[i] = bars[i].Open; highs[i] = bars[i].High;
            lows[i] = bars[i].Low; closes[i] = bars[i].Close;
        }

        // Streaming
        var rvgi = new Rvgi(period);
        for (int i = 0; i < N; i++) { rvgi.Update(bars[i], isNew: true); }
        double streamRvgi = rvgi.RvgiValue;
        double streamSig = rvgi.Signal;

        // Batch span
        var rvgiBatch = new double[N];
        var sigBatch = new double[N];
        Rvgi.Batch(opens, highs, lows, closes, rvgiBatch, sigBatch, period);

        _output.WriteLine($"Streaming RVGI={streamRvgi:F8}, Batch RVGI={rvgiBatch[N-1]:F8}");
        _output.WriteLine($"Streaming Signal={streamSig:F8}, Batch Signal={sigBatch[N-1]:F8}");

        Assert.Equal(streamRvgi, rvgiBatch[N - 1], Tolerance);
        Assert.Equal(streamSig, sigBatch[N - 1], Tolerance);
    }

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_Period20()
    {
        const int N = 300;
        const int period = 20;

        var gbm = new GBM(100.0, 0.05, 0.3, seed: 2002);
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            opens[i] = bars[i].Open; highs[i] = bars[i].High;
            lows[i] = bars[i].Low; closes[i] = bars[i].Close;
        }

        var rvgi = new Rvgi(period);
        for (int i = 0; i < N; i++) { rvgi.Update(bars[i], isNew: true); }

        var rvgiBatch = new double[N];
        var sigBatch = new double[N];
        Rvgi.Batch(opens, highs, lows, closes, rvgiBatch, sigBatch, period);

        Assert.Equal(rvgi.RvgiValue, rvgiBatch[N - 1], Tolerance);
        Assert.Equal(rvgi.Signal, sigBatch[N - 1], Tolerance);
    }

    // ───── Mathematical identity checks ─────

    [Fact]
    public void Validate_ConstantUpBars_RvgiConvergesToRatio()
    {
        // Constant bars: O=100, H=106, L=98, C=105 → C-O=5, H-L=8
        // SWMA(5) = 5, SWMA(8) = 8, SMA(5)/SMA(8) = 5/8 = 0.625
        const int N = 50;
        const int period = 5;

        var rvgi = new Rvgi(period);
        for (int i = 0; i < N; i++)
        {
            rvgi.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 100.0, high: 106.0, low: 98.0, close: 105.0, volume: 1000), isNew: true);
        }

        Assert.Equal(5.0 / 8.0, rvgi.RvgiValue, 1e-9);
        _output.WriteLine($"Constant up RVGI (expect 0.625): {rvgi.RvgiValue}");
    }

    [Fact]
    public void Validate_ZeroCloseOpenDiff_RvgiIsZero()
    {
        // Close == Open → numerator always 0 → RVGI = 0
        const int N = 50;
        const int period = 10;

        var rvgi = new Rvgi(period);
        for (int i = 0; i < N; i++)
        {
            rvgi.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 100.0, high: 105.0, low: 95.0, close: 100.0, volume: 1000), isNew: true);
        }

        Assert.Equal(0.0, rvgi.RvgiValue, Tolerance);
        _output.WriteLine($"Zero C-O RVGI (expect 0): {rvgi.RvgiValue}");
    }

    [Fact]
    public void Validate_DojiBars_ZeroDenominator_ReturnsZero()
    {
        // High == Low → denominator = 0 → RVGI = 0 (defensive division)
        const int N = 50;
        const int period = 10;

        var rvgi = new Rvgi(period);
        for (int i = 0; i < N; i++)
        {
            rvgi.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 100.0, high: 100.0, low: 100.0, close: 105.0, volume: 0), isNew: true);
        }

        Assert.Equal(0.0, rvgi.RvgiValue, Tolerance);
        _output.WriteLine($"Zero range (doji) RVGI (expect 0): {rvgi.RvgiValue}");
    }

    [Fact]
    public void Validate_SignalConverges_WhenConstantRvgi()
    {
        // When RVGI is constant, SWMA signal converges to that constant
        const int N = 50;
        const int period = 5;

        var rvgi = new Rvgi(period);
        for (int i = 0; i < N; i++)
        {
            rvgi.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: 100.0, high: 106.0, low: 98.0, close: 105.0, volume: 1000), isNew: true);
        }

        // Signal = SWMA(RVGI, 4) — when RVGI is constant, SWMA(constant) = constant
        Assert.Equal(rvgi.RvgiValue, rvgi.Signal, 1e-9);
        _output.WriteLine($"Signal converges to RVGI: {rvgi.Signal} == {rvgi.RvgiValue}");
    }

    [Fact]
    public void Validate_AllBars_Streaming_Vs_Batch_Match()
    {
        const int N = 100;
        const int period = 10;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 3333);
        var opens = new double[N]; var highs = new double[N];
        var lows = new double[N]; var closes = new double[N];
        var bars = new TBar[N];

        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
            opens[i] = bars[i].Open; highs[i] = bars[i].High;
            lows[i] = bars[i].Low; closes[i] = bars[i].Close;
        }

        var rvgiBatch = new double[N];
        var sigBatch = new double[N];
        Rvgi.Batch(opens, highs, lows, closes, rvgiBatch, sigBatch, period);

        var rvgi = new Rvgi(period);
        int mismatches = 0;
        for (int i = 0; i < N; i++)
        {
            rvgi.Update(bars[i], isNew: true);
            double diffRvgi = Math.Abs(rvgi.RvgiValue - rvgiBatch[i]);
            double diffSig = Math.Abs(rvgi.Signal - sigBatch[i]);
            if (diffRvgi > Tolerance || diffSig > Tolerance)
            {
                mismatches++;
                _output.WriteLine($"Mismatch at i={i}: RVGI stream={rvgi.RvgiValue}, batch={rvgiBatch[i]}, diff={diffRvgi:E3}; Signal stream={rvgi.Signal}, batch={sigBatch[i]}, diffSig={diffSig:E3}");
            }
        }

        Assert.Equal(0, mismatches);
        _output.WriteLine($"All {N} bars match between streaming and batch");
    }

    // ───── Determinism ─────

    [Fact]
    public void Validate_Deterministic_SameSeed_SameResult()
    {
        const int N = 150;
        const int period = 14;

        static (double rvgi, double sig) Compute(int n, int p, int seed)
        {
            var gbm = new GBM(100.0, 0.05, 0.2, seed: seed);
            var ind = new Rvgi(p);
            for (int i = 0; i < n; i++) { ind.Update(gbm.Next(isNew: true), isNew: true); }
            return (ind.RvgiValue, ind.Signal);
        }

        var (rv1, sg1) = Compute(N, period, 777);
        var (rv2, sg2) = Compute(N, period, 777);

        Assert.Equal(rv1, rv2, Tolerance);
        Assert.Equal(sg1, sg2, Tolerance);
        _output.WriteLine($"Deterministic RVGI: {rv1}, Signal: {sg1}");
    }

    // ───── Directional correctness ─────

    [Fact]
    public void Validate_PersistentUpTrend_PositiveRvgi()
    {
        // Persistent strong up bars: RVGI must be positive
        const int period = 10;
        var rvgi = new Rvgi(period);

        for (int i = 0; i < 50; i++)
        {
            double basePrice = 100.0 + i * 0.5;
            rvgi.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: basePrice,
                high: basePrice + 3.0,
                low: basePrice - 1.0,
                close: basePrice + 2.0, volume: 1000), isNew: true);
        }

        Assert.True(rvgi.RvgiValue > 0.0, $"Expected RVGI > 0 in uptrend, got {rvgi.RvgiValue}");
        _output.WriteLine($"Uptrend RVGI: {rvgi.RvgiValue}");
    }

    [Fact]
    public void Validate_PersistentDownTrend_NegativeRvgi()
    {
        // Persistent down bars: RVGI must be negative
        const int period = 10;
        var rvgi = new Rvgi(period);

        for (int i = 0; i < 50; i++)
        {
            double basePrice = 200.0 - i * 0.5;
            rvgi.Update(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                open: basePrice + 2.0,
                high: basePrice + 3.0,
                low: basePrice - 1.0,
                close: basePrice, volume: 1000), isNew: true);
        }

        Assert.True(rvgi.RvgiValue < 0.0, $"Expected RVGI < 0 in downtrend, got {rvgi.RvgiValue}");
        _output.WriteLine($"Downtrend RVGI: {rvgi.RvgiValue}");
    }
}
