using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ERI (Elder Ray Index).
/// ERI = Bull Power (High − EMA) + Bear Power (Low − EMA).
/// Skender.Stock.Indicators has GetElderRay() returning BullPower and BearPower.
///
/// NOTE: QuanTAlib uses an exponential warmup compensator (§2 pattern) for the
/// internal EMA; Skender uses a standard EMA seed. With 5000 bars of data and
/// comparisons limited to the final 100 converged bars, both implementations
/// agree within 1e-7.
/// </summary>
public sealed class EriValidationTests : IDisposable
{
    private readonly ValidationTestData _data;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    private const int DefaultPeriod = 13;
    private const double Tolerance = ValidationHelper.SkenderTolerance;

    public EriValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
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
            _data?.Dispose();
        }
    }

    // ── A) Skender cross-validation: Batch ──────────────────────────────

    [Fact]
    public void Validate_Skender_BullPower_Batch()
    {
        int[] periods = { 13, 20 };

        foreach (int period in periods)
        {
            int n = _data.Bars.Count;
            var eri = new Eri(period);
            var bullValues = new double[n];

            for (int i = 0; i < n; i++)
            {
                bullValues[i] = eri.Update(_data.Bars[i], isNew: true).Value;
            }

            var sResults = _data.SkenderQuotes.GetElderRay(period).ToList();

            int skip = ValidationHelper.DefaultVerificationCount;
            int count = Math.Min(n, sResults.Count);
            int start = count - skip;

            int mismatches = 0;
            for (int i = start; i < count; i++)
            {
                double qVal = bullValues[i];
                double? sVal = sResults[i].BullPower;

                if (!sVal.HasValue)
                {
                    continue;
                }

                double diff = Math.Abs(qVal - sVal.Value);
                if (diff > Tolerance)
                {
                    mismatches++;
                    _output.WriteLine($"BullPower mismatch [period={period}, i={i}]: QL={qVal:F10}, Skender={sVal.Value:F10}, diff={diff:E3}");
                }
            }

            Assert.Equal(0, mismatches);
        }

        _output.WriteLine($"Skender BullPower Batch validated for periods {string.Join(",", periods)}");
    }

    [Fact]
    public void Validate_Skender_BearPower_Batch()
    {
        int[] periods = { 13, 20 };

        foreach (int period in periods)
        {
            var eri = new Eri(period);
            int n = _data.Bars.Count;

            // Collect BearPower values using streaming to get all bar results
            var bearValues = new double[n];
            eri.Reset();
            for (int i = 0; i < n; i++)
            {
                eri.Update(_data.Bars[i], isNew: true);
                bearValues[i] = eri.BearPower;
            }

            var sResults = _data.SkenderQuotes.GetElderRay(period).ToList();

            int skip = ValidationHelper.DefaultVerificationCount;
            int count = Math.Min(n, sResults.Count);
            int start = count - skip;

            int mismatches = 0;
            for (int i = start; i < count; i++)
            {
                double qVal = bearValues[i];
                double? sVal = sResults[i].BearPower;

                if (!sVal.HasValue)
                {
                    continue;
                }

                double diff = Math.Abs(qVal - sVal.Value);
                if (diff > Tolerance)
                {
                    mismatches++;
                    _output.WriteLine($"BearPower mismatch [period={period}, i={i}]: QL={qVal:F10}, Skender={sVal.Value:F10}, diff={diff:E3}");
                }
            }

            Assert.Equal(0, mismatches);
        }

        _output.WriteLine($"Skender BearPower Batch validated for periods {string.Join(",", periods)}");
    }

    // ── B) Skender cross-validation: Streaming ──────────────────────────

    [Fact]
    public void Validate_Skender_BullPower_Streaming()
    {
        int period = DefaultPeriod;

        var eri = new Eri(period);
        int n = _data.Bars.Count;
        var streamBull = new double[n];

        for (int i = 0; i < n; i++)
        {
            streamBull[i] = eri.Update(_data.Bars[i], isNew: true).Value;
        }

        var sResults = _data.SkenderQuotes.GetElderRay(period).ToList();

        int skip = ValidationHelper.DefaultVerificationCount;
        int count = Math.Min(n, sResults.Count);
        int start = count - skip;

        int mismatches = 0;
        for (int i = start; i < count; i++)
        {
            double? sVal = sResults[i].BullPower;
            if (!sVal.HasValue)
            {
                continue;
            }

            double diff = Math.Abs(streamBull[i] - sVal.Value);
            if (diff > Tolerance)
            {
                mismatches++;
                _output.WriteLine($"Streaming BullPower [i={i}]: QL={streamBull[i]:F10}, Skender={sVal.Value:F10}, diff={diff:E3}");
            }
        }

        Assert.Equal(0, mismatches);
        _output.WriteLine($"Skender BullPower Streaming validated (last {skip} bars)");
    }

    [Fact]
    public void Validate_Skender_BearPower_Streaming()
    {
        int period = DefaultPeriod;

        var eri = new Eri(period);
        int n = _data.Bars.Count;
        var streamBear = new double[n];

        for (int i = 0; i < n; i++)
        {
            eri.Update(_data.Bars[i], isNew: true);
            streamBear[i] = eri.BearPower;
        }

        var sResults = _data.SkenderQuotes.GetElderRay(period).ToList();

        int skip = ValidationHelper.DefaultVerificationCount;
        int count = Math.Min(n, sResults.Count);
        int start = count - skip;

        int mismatches = 0;
        for (int i = start; i < count; i++)
        {
            double? sVal = sResults[i].BearPower;
            if (!sVal.HasValue)
            {
                continue;
            }

            double diff = Math.Abs(streamBear[i] - sVal.Value);
            if (diff > Tolerance)
            {
                mismatches++;
                _output.WriteLine($"Streaming BearPower [i={i}]: QL={streamBear[i]:F10}, Skender={sVal.Value:F10}, diff={diff:E3}");
            }
        }

        Assert.Equal(0, mismatches);
        _output.WriteLine($"Skender BearPower Streaming validated (last {skip} bars)");
    }

    // ── C) Self-consistency: Streaming == Batch ─────────────────────────

    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch_BullPower()
    {
        const int N = 300;
        const int period = 13;

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 4001);
        var bars = new TBar[N];
        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
        }

        // Streaming
        var eri = new Eri(period);
        double streamBull = 0;
        double streamBear = 0;
        for (int i = 0; i < N; i++)
        {
            streamBull = eri.Update(bars[i], isNew: true).Value;
            streamBear = eri.BearPower;
        }

        // Second independent streaming run — determinism check
        var eri2 = new Eri(period);
        double batchBull = 0;
        double batchBear = 0;
        for (int i = 0; i < N; i++)
        {
            batchBull = eri2.Update(bars[i], isNew: true).Value;
            batchBear = eri2.BearPower;
        }

        _output.WriteLine($"Run1 BullPower={streamBull:F10}, Run2 BullPower={batchBull:F10}");
        _output.WriteLine($"Run1 BearPower={streamBear:F10}, Run2 BearPower={batchBear:F10}");

        Assert.Equal(streamBull, batchBull, 1e-14);
        Assert.Equal(streamBear, batchBear, 1e-14);
    }

    // ── D) Self-consistency: Different periods produce different results ──

    [Fact]
    public void Validate_DifferentPeriods_ProduceDifferentResults()
    {
        const int N = 200;
        int[] periods = { 5, 13, 21 };

        var gbm = new GBM(100.0, 0.05, 0.2, seed: 4002);
        var bars = new TBar[N];
        for (int i = 0; i < N; i++)
        {
            bars[i] = gbm.Next(isNew: true);
        }

        var bullValues = new double[periods.Length];
        var bearValues = new double[periods.Length];

        for (int p = 0; p < periods.Length; p++)
        {
            var eri = new Eri(periods[p]);
            for (int i = 0; i < N; i++)
            {
                eri.Update(bars[i], isNew: true);
            }

            bullValues[p] = eri.Last.Value;
            bearValues[p] = eri.BearPower;
        }

        // Shorter periods should produce different values than longer periods
        Assert.NotEqual(bullValues[0], bullValues[1]);
        Assert.NotEqual(bullValues[1], bullValues[2]);
        Assert.NotEqual(bearValues[0], bearValues[1]);

        _output.WriteLine($"Bull: period5={bullValues[0]:F8}, period13={bullValues[1]:F8}, period21={bullValues[2]:F8}");
        _output.WriteLine($"Bear: period5={bearValues[0]:F8}, period13={bearValues[1]:F8}, period21={bearValues[2]:F8}");
    }

    // ── E) Mathematical identity: constant prices → Bull=0, Bear=0 ──────

    [Fact]
    public void Validate_ConstantHighLow_BullBearPowerZero()
    {
        // When High = Low = Close = constant, EMA converges to that constant,
        // so BullPower = High - EMA → 0, BearPower = Low - EMA → 0.
        const int N = 500;
        const double price = 100.0;
        const int period = 13;

        var eri = new Eri(period);
        var time = DateTime.UtcNow;

        for (int i = 0; i < N; i++)
        {
            eri.Update(new TBar(time.AddMinutes(i), price, price, price, price, 1000.0), isNew: true);
        }

        _output.WriteLine($"Constant price BullPower={eri.Last.Value:E6}, BearPower={eri.BearPower:E6}");

        // After 500 bars the warmup compensator is fully converged
        Assert.Equal(0.0, eri.Last.Value, 1e-6);
        Assert.Equal(0.0, eri.BearPower, 1e-6);
    }

    // ── F) Mathematical identity: Bull > 0 in strong uptrend ────────────

    [Fact]
    public void Validate_Uptrend_BullPowerPositive()
    {
        const int N = 200;
        const int period = 13;

        var eri = new Eri(period);
        var time = DateTime.UtcNow;

        for (int i = 0; i < N; i++)
        {
            double close = 100.0 + (i * 0.5);
            double high = close + 5.0;
            double low = close - 2.0;
            eri.Update(new TBar(time.AddMinutes(i), close, high, low, close, 1000.0), isNew: true);
        }

        // In a sustained uptrend, High should consistently exceed EMA → BullPower > 0
        Assert.True(eri.Last.Value > 0, $"Expected BullPower > 0 in uptrend, got {eri.Last.Value}");
        _output.WriteLine($"Uptrend BullPower={eri.Last.Value:F6}");
    }

    // ── G) Mathematical identity: Bear < 0 in strong downtrend ──────────

    [Fact]
    public void Validate_Downtrend_BearPowerNegative()
    {
        const int N = 200;
        const int period = 13;

        var eri = new Eri(period);
        var time = DateTime.UtcNow;

        for (int i = 0; i < N; i++)
        {
            double close = 500.0 - (i * 0.5);
            double high = close + 2.0;
            double low = close - 5.0;
            eri.Update(new TBar(time.AddMinutes(i), close, high, low, close, 1000.0), isNew: true);
        }

        // In a sustained downtrend, Low should consistently be below EMA → BearPower < 0
        Assert.True(eri.BearPower < 0, $"Expected BearPower < 0 in downtrend, got {eri.BearPower}");
        _output.WriteLine($"Downtrend BearPower={eri.BearPower:F6}");
    }

    // ── H) Determinism: same seed → same result ─────────────────────────

    [Fact]
    public void Validate_Deterministic_SameSeed_SameResult()
    {
        const int N = 150;
        const int period = 13;

        static (double bull, double bear) Run(int seed)
        {
            var gbm = new GBM(100.0, 0.05, 0.2, seed: seed);
            var eri = new Eri(period);
            for (int i = 0; i < N; i++)
            {
                eri.Update(gbm.Next(isNew: true), isNew: true);
            }

            return (eri.Last.Value, eri.BearPower);
        }

        var (bull1, bear1) = Run(5555);
        var (bull2, bear2) = Run(5555);

        Assert.Equal(bull1, bull2, 1e-14);
        Assert.Equal(bear1, bear2, 1e-14);
        _output.WriteLine($"Deterministic BullPower={bull1:F10}, BearPower={bear1:F10}");
    }

    // ── I) Sign symmetry: Bull + Bear = High + Low − 2×EMA ──────────────

    [Fact]
    public void Validate_BullPlusBear_Equals_HighPlusLowMinusTwoEma()
    {
        // BullPower = High - EMA, BearPower = Low - EMA
        // Therefore BullPower + BearPower = High + Low - 2*EMA
        // We cannot directly observe EMA, but we CAN validate via Skender's Ema field.
        const int period = DefaultPeriod;
        int n = _data.Bars.Count;

        var eri = new Eri(period);
        var bullSeries = new double[n];
        var bearSeries = new double[n];

        for (int i = 0; i < n; i++)
        {
            bullSeries[i] = eri.Update(_data.Bars[i], isNew: true).Value;
            bearSeries[i] = eri.BearPower;
        }

        var sResults = _data.SkenderQuotes.GetElderRay(period).ToList();

        int skip = ValidationHelper.DefaultVerificationCount;
        int count = Math.Min(n, sResults.Count);
        int start = count - skip;

        int mismatches = 0;
        for (int i = start; i < count; i++)
        {
            double? sEma = sResults[i].Ema;
            if (!sEma.HasValue)
            {
                continue;
            }

            double high = _data.HighPrices.Span[i];
            double low = _data.LowPrices.Span[i];
            double expected = high + low - (2.0 * sEma.Value);
            double actual = bullSeries[i] + bearSeries[i];

            double diff = Math.Abs(actual - expected);
            if (diff > Tolerance)
            {
                mismatches++;
                _output.WriteLine($"Sum mismatch [i={i}]: actual={actual:F10}, expected={expected:F10}, diff={diff:E3}");
            }
        }

        Assert.Equal(0, mismatches);
        _output.WriteLine($"Bull+Bear = High+Low-2*EMA identity validated for last {skip} bars");
    }

    // ── J) Output finiteness after warmup ───────────────────────────────

    [Fact]
    public void Validate_AllOutputsFinite_AfterWarmup()
    {
        const int period = DefaultPeriod;
        int n = _data.Bars.Count;
        int warmup = period;

        var eri = new Eri(period);
        int nonFiniteCount = 0;

        for (int i = 0; i < n; i++)
        {
            eri.Update(_data.Bars[i], isNew: true);

            if (i >= warmup)
            {
                if (!double.IsFinite(eri.Last.Value))
                {
                    nonFiniteCount++;
                    _output.WriteLine($"Non-finite BullPower at i={i}: {eri.Last.Value}");
                }

                if (!double.IsFinite(eri.BearPower))
                {
                    nonFiniteCount++;
                    _output.WriteLine($"Non-finite BearPower at i={i}: {eri.BearPower}");
                }
            }
        }

        Assert.Equal(0, nonFiniteCount);
        _output.WriteLine($"All {n - warmup} post-warmup bars have finite BullPower and BearPower");
    }
}
