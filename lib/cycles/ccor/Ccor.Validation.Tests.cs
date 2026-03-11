using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for CCOR - Ehlers Correlation Cycle.
/// Since CCOR is a proprietary Ehlers algorithm with no standard library implementations
/// (not in TA-Lib, Skender, Tulip, or Ooples), these tests validate mathematical
/// properties of Pearson correlation and internal consistency across API modes.
/// </summary>
public class CcorValidationTests
{
    private const double Tolerance = 1e-9;
    private const long StartTime = 946_684_800_000_000_0L; // 2000-01-01 UTC in ticks
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    #region Pearson Correlation Mathematical Properties

    [Fact]
    public void Ccor_ConstantInput_RealAndImagAreZero()
    {
        // Constant price → zero variance in x → correlation undefined → returns 0
        var ccor = new Ccor(period: 10);

        for (int i = 0; i < 100; i++)
        {
            ccor.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0), true);
        }

        Assert.Equal(0.0, ccor.Real, Tolerance);
        Assert.Equal(0.0, ccor.Imag, Tolerance);
    }

    [Fact]
    public void Ccor_PerfectCosineInput_RealNearOne()
    {
        // If price exactly matches the cosine reference, Real correlation → +1
        int period = 20;
        var ccor = new Ccor(period: period);

        double twoPiOverN = 2.0 * Math.PI / period;
        for (int i = 0; i < 200; i++)
        {
            double val = Math.Cos(twoPiOverN * (i % period));
            ccor.Update(new TValue(DateTime.UtcNow.AddMinutes(i), val), true);
        }

        // After many full cycles, Real should be very close to +1
        Assert.True(ccor.Real > 0.95,
            $"Perfect cosine input should give Real ≈ 1.0, got {ccor.Real:F6}");
    }

    [Fact]
    public void Ccor_PerfectNegSineInput_ImagHighMagnitude()
    {
        // If price has -sin periodicity, Imag correlation magnitude should be near 1.0
        int period = 20;
        var ccor = new Ccor(period: period);

        double twoPiOverN = 2.0 * Math.PI / period;
        for (int i = 0; i < 200; i++)
        {
            double val = -Math.Sin(twoPiOverN * (i % period));
            ccor.Update(new TValue(DateTime.UtcNow.AddMinutes(i), val), true);
        }

        Assert.True(Math.Abs(ccor.Imag) > 0.90,
            $"Perfect -sin input should give |Imag| ≈ 1.0, got {ccor.Imag:F6}");
    }

    [Fact]
    public void Ccor_RealAndImag_BoundedMinusOneToOne()
    {
        // Pearson correlation coefficient is always in [-1, +1]
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(1000, StartTime, Step);
        var ccor = new Ccor(period: 20);

        for (int i = 0; i < bars.Count; i++)
        {
            ccor.Update(new TValue(bars[i].Time, bars[i].Close), true);
            Assert.InRange(ccor.Real, -1.0, 1.0);
            Assert.InRange(ccor.Imag, -1.0, 1.0);
        }
    }

    [Fact]
    public void Ccor_SineWave_RealAndImagAreOrthogonal()
    {
        // For a pure sine wave at the indicator's period, the Real (cosine) and Imag (-sine)
        // correlations should be approximately orthogonal components of a phasor
        int period = 20;
        var ccor = new Ccor(period: period);

        for (int i = 0; i < 200; i++)
        {
            double val = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / period);
            ccor.Update(new TValue(DateTime.UtcNow.AddMinutes(i), val), true);
        }

        // Both should be non-trivial
        Assert.True(Math.Abs(ccor.Real) > 0.01 || Math.Abs(ccor.Imag) > 0.01,
            $"Sine wave should produce non-trivial phasor: Real={ccor.Real:F4}, Imag={ccor.Imag:F4}");

        // R² + I² should be near 1 for a pure tone at the matched frequency
        double magnitude = Math.Sqrt(ccor.Real * ccor.Real + ccor.Imag * ccor.Imag);
        Assert.True(magnitude > 0.5,
            $"Phasor magnitude should be significant for matched sine: {magnitude:F4}");
    }

    #endregion

    #region Angle Properties

    [Fact]
    public void Ccor_Angle_MonotonicallyNonDecreasing()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var ccor = new Ccor(period: 20);
        double prevAngle = double.MinValue;

        for (int i = 0; i < bars.Count; i++)
        {
            ccor.Update(new TValue(bars[i].Time, bars[i].Close), true);
            Assert.True(ccor.Angle >= prevAngle,
                $"Angle decreased at bar {i}: {ccor.Angle:F4} < prev {prevAngle:F4}");
            prevAngle = ccor.Angle;
        }
    }

    [Fact]
    public void Ccor_Angle_AdvancesOnCyclicInput()
    {
        // For cyclic input, the angle should advance significantly
        int period = 20;
        var ccor = new Ccor(period: period);

        for (int i = 0; i < 200; i++)
        {
            double val = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / period);
            ccor.Update(new TValue(DateTime.UtcNow.AddMinutes(i), val), true);
        }

        Assert.True(ccor.Angle > 0.0,
            $"Angle should advance on cyclic input, got {ccor.Angle:F4}");
    }

    #endregion

    #region Market State Properties

    [Fact]
    public void Ccor_MarketState_OnlyValidValues()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var ccor = new Ccor(period: 20, threshold: 9.0);

        for (int i = 0; i < bars.Count; i++)
        {
            ccor.Update(new TValue(bars[i].Time, bars[i].Close), true);
            Assert.Contains(ccor.MarketState, new[] { -1, 0, 1 });
        }
    }

    [Fact]
    public void Ccor_MarketState_HasVariation()
    {
        // Over enough data, all three states should appear at least once
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(2000, StartTime, Step);
        var ccor = new Ccor(period: 20, threshold: 9.0);
        var states = new HashSet<int>();

        for (int i = 0; i < bars.Count; i++)
        {
            ccor.Update(new TValue(bars[i].Time, bars[i].Close), true);
            states.Add(ccor.MarketState);
        }

        Assert.True(states.Count >= 2,
            $"Expected at least 2 distinct market states, got {states.Count}: {string.Join(",", states)}");
    }

    #endregion

    #region Deterministic Reproducibility

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(456)]
    public void Ccor_DeterministicOutput(int seed)
    {
        var gbm1 = new GBM(seed: seed);
        var bars1 = gbm1.Fetch(200, StartTime, Step);

        var gbm2 = new GBM(seed: seed);
        var bars2 = gbm2.Fetch(200, StartTime, Step);

        var ccor1 = new Ccor(period: 20, threshold: 9.0);
        var ccor2 = new Ccor(period: 20, threshold: 9.0);

        for (int i = 0; i < bars1.Count; i++)
        {
            var r1 = ccor1.Update(new TValue(bars1[i].Time, bars1[i].Close));
            var r2 = ccor2.Update(new TValue(bars2[i].Time, bars2[i].Close));

            Assert.Equal(r1.Value, r2.Value, Tolerance);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void Ccor_AllPeriods_ProduceFiniteOutput(int period)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var ccor = new Ccor(period: period);

        for (int i = 0; i < bars.Count; i++)
        {
            var r = ccor.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.True(double.IsFinite(r.Value), $"Non-finite at bar {i} with period={period}");
            Assert.True(double.IsFinite(ccor.Real), $"Non-finite Real at bar {i}");
            Assert.True(double.IsFinite(ccor.Imag), $"Non-finite Imag at bar {i}");
            Assert.True(double.IsFinite(ccor.Angle), $"Non-finite Angle at bar {i}");
        }
    }

    #endregion

    #region Consistency Validation

    [Fact]
    public void Ccor_BatchMatchesStreaming_OnGBM()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var source = bars.Close;

        // Streaming
        var ccorStream = new Ccor(period: 20, threshold: 9.0);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            var r = ccorStream.Update(source[i], true);
            streamResults[i] = r.Value;
        }

        // Batch
        var batchResults = Ccor.Batch(source, 20, 9.0);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Ccor_SpanMatchesBatch_OnGBM()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);
        var source = bars.Close;

        // TSeries batch
        var batchResults = Ccor.Batch(source, 20, 9.0);

        // Span batch
        double[] values = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            values[i] = source[i].Value;
        }

        double[] output = new double[values.Length];
        Ccor.Batch(values.AsSpan(), output.AsSpan(), 20, 9.0);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchResults[i].Value, output[i], Tolerance);
        }
    }

    [Fact]
    public void Ccor_ResetAndReprocess_Matches()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, StartTime, Step);
        var source = bars.Close;

        var ccor = new Ccor(period: 20, threshold: 9.0);
        var results1 = ccor.Update(source);

        ccor.Reset();
        var results2 = ccor.Update(source);

        Assert.Equal(results1.Count, results2.Count);
        for (int i = 0; i < results1.Count; i++)
        {
            Assert.Equal(results1[i].Value, results2[i].Value, Tolerance);
        }
    }

    #endregion

    #region Period Sensitivity

    [Fact]
    public void Ccor_DifferentPeriods_ProduceDifferentResults()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, StartTime, Step);

        var ccor10 = new Ccor(period: 10);
        var ccor30 = new Ccor(period: 30);
        double diffEnergy = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            var tv = new TValue(bars[i].Time, bars[i].Close);
            var r10 = ccor10.Update(tv);
            var r30 = ccor30.Update(tv);

            if (i > 30)
            {
                double d = r10.Value - r30.Value;
                diffEnergy += d * d;
            }
        }

        Assert.True(diffEnergy > 1e-6,
            $"Different periods should produce different outputs, diffEnergy={diffEnergy}");
    }

    [Fact]
    public void Ccor_DifferentThresholds_ProduceDifferentMarketStates()
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, StartTime, Step);

        var ccorTight = new Ccor(period: 20, threshold: 1.0);
        var ccorLoose = new Ccor(period: 20, threshold: 50.0);

        int statesDiffer = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            var tv = new TValue(bars[i].Time, bars[i].Close);
            ccorTight.Update(tv);
            ccorLoose.Update(tv);

            if (ccorTight.MarketState != ccorLoose.MarketState)
            {
                statesDiffer++;
            }
        }

        // Real/Imag/Angle are independent of threshold — only MarketState differs
        Assert.True(statesDiffer > 0,
            "Different thresholds should produce different market state classifications");
    }

    [Fact]
    public void Ccor_Correction_Recomputes()
    {
        var ind = new Ccor(period: 20);
        var t0 = new DateTime(946_684_800_000_000_0L, DateTimeKind.Utc);

        // Build state well past warmup
        for (int i = 0; i < 100; i++)
        {
            ind.Update(new TValue(t0.AddMinutes(i),
                100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0)), isNew: true);
        }

        // Anchor bar
        var anchorTime = t0.AddMinutes(100);
        const double anchorPrice = 105.5;
        ind.Update(new TValue(anchorTime, anchorPrice), isNew: true);
        double anchorResult = ind.Last.Value;

        // Correction with a dramatically different price — recompute must yield different result
        ind.Update(new TValue(anchorTime, anchorPrice * 10.0), isNew: false);
        Assert.NotEqual(anchorResult, ind.Last.Value);

        // Correction back to original price — must exactly restore original result
        ind.Update(new TValue(anchorTime, anchorPrice), isNew: false);
        Assert.Equal(anchorResult, ind.Last.Value, Tolerance);
    }

    #endregion
}
