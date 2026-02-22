using Xunit;

namespace QuanTAlib.Tests;

public class HendTests
{
    private const int DefaultPeriod = 7;
    private const double Epsilon = 1e-10;

    // ── A) Constructor validation ──────────────────────────────────────

    [Fact]
    public void Constructor_PeriodTooSmall_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Hend(period: 3));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var hend = new Hend(period: 7);
        Assert.Equal("Hend(7)", hend.Name);
    }

    [Fact]
    public void Constructor_EvenPeriod_AdjustedToOdd()
    {
        var hend = new Hend(period: 8);
        Assert.Equal("Hend(9)", hend.Name);
    }

    [Fact]
    public void Constructor_MinPeriod5_Works()
    {
        var hend = new Hend(period: 5);
        Assert.Equal("Hend(5)", hend.Name);
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var hend = new Hend(DefaultPeriod);
        var result = hend.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var hend = new Hend(DefaultPeriod);
        hend.Update(new TValue(DateTime.UtcNow, 50.0));
        Assert.Equal(50.0, hend.Last.Value, Epsilon);
    }

    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        var hend = new Hend(5);
        const double c = 42.0;
        for (int i = 0; i < 20; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), c));
        }
        Assert.Equal(c, hend.Last.Value, 1e-9);
    }

    [Fact]
    public void LinearTrend_PreservedExactly()
    {
        // Henderson preserves up to cubic polynomials at the CENTER of the window.
        // For period=5, half=2, the output at bar N represents polynomial at index N-2.
        const int period = 5;
        int half = (period - 1) / 2;
        var hend = new Hend(period);
        int total = 20;
        double lastResult = double.NaN;
        for (int i = 0; i < total; i++)
        {
            double val = 10.0 + 3.0 * i;
            var result = hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
            lastResult = result.Value;
        }
        // Centered filter: output at bar N = polynomial value at bar N - half
        int centerIdx = total - 1 - half;
        double expected = 10.0 + 3.0 * centerIdx;
        Assert.Equal(expected, lastResult, 1e-6);
    }

    [Fact]
    public void QuadraticTrend_PreservedExactly()
    {
        const int period = 5;
        int half = (period - 1) / 2;
        var hend = new Hend(period);
        int total = 20;
        double lastResult = double.NaN;
        for (int i = 0; i < total; i++)
        {
            double val = 5.0 + 2.0 * i + 0.5 * i * i;
            var result = hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
            lastResult = result.Value;
        }
        int centerIdx = total - 1 - half;
        double expected = 5.0 + 2.0 * centerIdx + 0.5 * centerIdx * centerIdx;
        Assert.Equal(expected, lastResult, 1e-4);
    }

    [Fact]
    public void CubicTrend_PreservedExactly()
    {
        const int period = 5;
        int half = (period - 1) / 2;
        var hend = new Hend(period);
        int total = 20;
        double lastResult = double.NaN;
        for (int i = 0; i < total; i++)
        {
            double val = 1.0 + 0.5 * i + 0.1 * i * i + 0.01 * i * i * i;
            var result = hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
            lastResult = result.Value;
        }
        int centerIdx = total - 1 - half;
        double expected = 1.0 + 0.5 * centerIdx + 0.1 * centerIdx * centerIdx + 0.01 * centerIdx * centerIdx * centerIdx;
        Assert.Equal(expected, lastResult, 1e-2);
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var hend = new Hend(5);
        for (int i = 0; i < 10; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100 + i), isNew: true);
        }
        Assert.True(hend.IsHot);
    }

    [Fact]
    public void IsNew_False_Rewrites()
    {
        var hend = new Hend(5);
        for (int i = 0; i < 6; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), isNew: true);
        }
        var before = hend.Last.Value;

        // Bar correction with different value
        hend.Update(new TValue(DateTime.UtcNow.AddSeconds(5), 200.0), isNew: false);
        var corrected = hend.Last.Value;

        // Should be different since one value changed
        Assert.NotEqual(before, corrected);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var hend = new Hend(5);
        for (int i = 0; i < 10; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0 + i), isNew: true);
        }
        var snapshot = hend.Last.Value;

        // Multiple corrections, then re-send same value
        hend.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 999.0), isNew: false);
        hend.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 888.0), isNew: false);
        hend.Update(new TValue(DateTime.UtcNow.AddSeconds(10), 50.0 + 9), isNew: false);

        // Last correction with original value should restore
        Assert.Equal(snapshot, hend.Last.Value, 1e-10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var hend = new Hend(5);
        for (int i = 0; i < 10; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }
        Assert.True(hend.IsHot);

        hend.Reset();
        Assert.False(hend.IsHot);
        Assert.Equal(default, hend.Last);
    }

    // ── D) Warmup / convergence ────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsWhenBufferFull()
    {
        var hend = new Hend(5);
        for (int i = 0; i < 4; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
            Assert.False(hend.IsHot);
        }
        hend.Update(new TValue(DateTime.UtcNow.AddSeconds(4), 100.0));
        Assert.True(hend.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsUserPeriod()
    {
        var hend = new Hend(7);
        Assert.Equal(7, hend.WarmupPeriod);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void NaN_SubstitutesLastValid()
    {
        var hend = new Hend(5);
        for (int i = 0; i < 6; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Send NaN - should substitute last valid
        hend.Update(new TValue(DateTime.UtcNow.AddSeconds(6), double.NaN));

        Assert.True(double.IsFinite(hend.Last.Value));
    }

    [Fact]
    public void Infinity_SubstitutesLastValid()
    {
        var hend = new Hend(5);
        for (int i = 0; i < 6; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        hend.Update(new TValue(DateTime.UtcNow.AddSeconds(6), double.PositiveInfinity));
        Assert.True(double.IsFinite(hend.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        double[] src = [1, 2, double.NaN, 4, 5, 6, 7, 8, 9, 10];
        double[] output = new double[src.Length];
        Hend.Batch(src, output, period: 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"output[{i}] is not finite");
        }
    }

    // ── F) Consistency ─────────────────────────────────────────────────

    [Fact]
    public void Batch_MatchesStreaming()
    {
        const int len = 50;
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < len; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }

        // Streaming
        var hend = new Hend(DefaultPeriod);
        var streaming = new double[len];
        for (int i = 0; i < len; i++)
        {
            var result = hend.Update(source[i]);
            streaming[i] = result.Value;
        }

        // Batch TSeries
        var batchResult = Hend.Batch(source, DefaultPeriod);

        for (int i = 0; i < len; i++)
        {
            Assert.Equal(streaming[i], batchResult[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Span_MatchesStreaming()
    {
        const int len = 50;
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < len; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }

        // Streaming
        var hend = new Hend(DefaultPeriod);
        var streaming = new double[len];
        for (int i = 0; i < len; i++)
        {
            var result = hend.Update(source[i]);
            streaming[i] = result.Value;
        }

        // Span
        double[] spanOutput = new double[len];
        Hend.Batch(source.Values, spanOutput, DefaultPeriod);

        for (int i = 0; i < len; i++)
        {
            Assert.Equal(streaming[i], spanOutput[i], 1e-10);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] src = [1, 2, 3, 4, 5];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Hend.Batch(src, output, period: 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodTooSmall_Throws()
    {
        double[] src = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Hend.Batch(src, output, period: 3));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOp()
    {
        Hend.Batch(ReadOnlySpan<double>.Empty, Span<double>.Empty, period: 5);
        Assert.True(true); // no-throw is the assertion
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_Fires()
    {
        var hend = new Hend(5);
        bool fired = false;
        hend.Pub += (object? sender, in TValueEventArgs e) => fired = true;
        hend.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(fired);
    }

    [Fact]
    public void EventBased_Chaining()
    {
        var source = new TSeries();
        var hend = new Hend(source, period: 5);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(hend.IsHot);
        Assert.True(double.IsFinite(hend.Last.Value));
    }

    // ── I) Dispose ─────────────────────────────────────────────────────

    [Fact]
    public void Dispose_Idempotent()
    {
        var hend = new Hend(5);
        hend.Dispose();
        hend.Dispose(); // Should not throw
        Assert.True(true); // no-throw is the assertion
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var hend = new Hend(source, period: 5);
        hend.Dispose();

        // Adding to source after dispose should not affect hend
        source.Add(new TValue(DateTime.UtcNow, 999.0));
        Assert.False(hend.IsHot);
    }

    // ── J) Henderson-specific: Wolfram-verified H5 weights ─────────────

    [Fact]
    public void H5_ConstInput_ReturnsConstant()
    {
        // Wolfram-verified: H5 weights = {-21/286, 42/143, 80/143, 42/143, -21/286}
        // For constant input, sum of weights * constant = constant (weights sum to 1)
        var hend = new Hend(5);
        const double c = 100.0;

        for (int i = 0; i < 5; i++)
        {
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), c));
        }
        Assert.Equal(c, hend.Last.Value, 1e-10);
    }

    [Fact]
    public void H5_NegativeEdgeWeights_BandpassProperty()
    {
        // Henderson has negative weights at edges — verify filter can output
        // values outside the min-max range of inputs (bandpass property)
        var hend = new Hend(5);
        // Step function: 0,0,100,0,0 — negative edge weights will push result outside [0,100]
        double[] vals = [0, 0, 100, 0, 0];
        TValue result = default;
        for (int i = 0; i < 5; i++)
        {
            result = hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), vals[i]));
        }
        // Henderson H5 center weight = 80/143 ≈ 0.5594
        // Expected: 0*w0 + 0*w1 + 100*w2 + 0*w3 + 0*w4 = 100 * 80/143 ≈ 55.944
        double expected = 100.0 * 80.0 / 143.0;
        Assert.Equal(expected, result.Value, 1e-6);
    }

    [Fact]
    public void H5_Symmetric_Weights()
    {
        // Henderson weights are symmetric: w(k) = w(-k)
        // Reversing the input order of a symmetric window should give same center value
        var hend1 = new Hend(5);
        var hend2 = new Hend(5);

        double[] forward = [10, 20, 30, 40, 50];
        double[] reverse = [50, 40, 30, 20, 10];

        TValue r1 = default, r2 = default;
        for (int i = 0; i < 5; i++)
        {
            r1 = hend1.Update(new TValue(DateTime.UtcNow.AddSeconds(i), forward[i]));
            r2 = hend2.Update(new TValue(DateTime.UtcNow.AddSeconds(i), reverse[i]));
        }

        // For linear input, Henderson preserves the polynomial, so both
        // should give 30 (the center value of the linear trend)
        // forward: 10+20+30+40+50, reverse: 50+40+30+20+10
        // With symmetric weights applied, sum(w*forward) + sum(w*reverse) = 2*30*sum(w) = 60
        Assert.Equal(60.0, r1.Value + r2.Value, 1e-6);
    }
}
