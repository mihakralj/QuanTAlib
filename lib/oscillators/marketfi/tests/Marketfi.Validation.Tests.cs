using Tulip;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for MARKETFI plus Tulip cross-validation.
/// Tulip implements <c>marketfi</c>: (High - Low) / Volume — exact formula match.
/// Tulip takes three inputs (high, low, volume) and no options (no period).
/// </summary>
public sealed class MarketfiValidationTests
{
    private const double Tolerance = 1e-10;

    // ── Identity: MFI = Range / Volume ───────────────────────────────────────

    [Theory]
    [InlineData(110, 90, 1000, 0.02)]
    [InlineData(115, 85, 500, 0.06)]
    [InlineData(100, 80, 200, 0.10)]
    [InlineData(105, 100, 50, 0.10)]
    [InlineData(100, 100, 1000, 0.0)]   // zero range
    [InlineData(110, 90, 0, 0.0)]       // zero volume guard
    public void Identity_Formula_MatchesDirectComputation(
        double high, double low, double volume, double expected)
    {
        var m = new Marketfi();
        var result = m.Update(new TBar(DateTime.UtcNow, 100, high, low, 100, volume));
        Assert.Equal(expected, result.Value, Tolerance);
    }

    // ── Batch == Streaming ───────────────────────────────────────────────────

    [Fact]
    public void BatchStreaming_AgreeOnAllBars()
    {
        const int N = 200;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 17);

        double[] hi = new double[N], lo = new double[N], vol = new double[N];
        double[] streamOut = new double[N];
        double[] batchOut = new double[N];

        var m = new Marketfi();
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            hi[i] = bar.High;
            lo[i] = bar.Low;
            vol[i] = bar.Volume;
            m.Update(bar, isNew: true);
            streamOut[i] = m.Last.Value;
        }

        Marketfi.Batch(hi, lo, vol, batchOut);

        for (int i = 0; i < N; i++)
        {
            Assert.Equal(streamOut[i], batchOut[i], Tolerance);
        }
    }

    // ── Determinism ──────────────────────────────────────────────────────────

    [Fact]
    public void Determinism_SameInputSameOutput()
    {
        var gbm1 = new GBM(100.0, 0.05, 0.2, seed: 99);
        var gbm2 = new GBM(100.0, 0.05, 0.2, seed: 99);

        var m1 = new Marketfi();
        var m2 = new Marketfi();

        for (int i = 0; i < 100; i++)
        {
            var bar1 = gbm1.Next(isNew: true);
            var bar2 = gbm2.Next(isNew: true);
            m1.Update(bar1, isNew: true);
            m2.Update(bar2, isNew: true);
            Assert.Equal(m1.Last.Value, m2.Last.Value, Tolerance);
        }
    }

    // ── Non-negativity ───────────────────────────────────────────────────────

    [Fact]
    public void Output_AlwaysNonNegative()
    {
        var gbm = new GBM(100.0, 0.05, 0.3, seed: 123);
        var m = new Marketfi();
        for (int i = 0; i < 500; i++)
        {
            var result = m.Update(gbm.Next(isNew: true));
            Assert.True(result.Value >= 0.0, $"MFI negative at bar {i}: {result.Value}");
        }
    }

    // ── Zero volume → zero output ─────────────────────────────────────────────

    [Fact]
    public void ZeroVolume_AlwaysZero()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            var result = m.Update(new TBar(t.AddMinutes(i), 100, 110 + i, 90 - i, 100, 0.0));
            Assert.Equal(0.0, result.Value, Tolerance);
        }
    }

    // ── FlatLine: constant range and volume produce constant MFI ─────────────

    [Fact]
    public void FlatLine_ConstantBarProducesConstantMfi()
    {
        var m = new Marketfi();
        var t = DateTime.UtcNow;
        double expectedMfi = 20.0 / 1000.0; // 0.02
        for (int i = 0; i < 50; i++)
        {
            var result = m.Update(new TBar(t.AddMinutes(i), 100, 110, 90, 100, 1000));
            Assert.Equal(expectedMfi, result.Value, Tolerance);
        }
    }

    // ── Scaling: double volume halves MFI ────────────────────────────────────

    [Fact]
    public void Scaling_DoubleVolume_HalvesMfi()
    {
        var m1 = new Marketfi();
        var m2 = new Marketfi();

        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000.0);
        var bar2 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 2000.0);

        double mfi1 = m1.Update(bar1).Value;
        double mfi2 = m2.Update(bar2).Value;

        // mfi2 = mfi1 / 2: doubling volume halves the index
        Assert.Equal(mfi1 / 2.0, mfi2, Tolerance);
    }

    // ── Scaling: double range doubles MFI ────────────────────────────────────

    [Fact]
    public void Scaling_DoubleRange_DoublesMfi()
    {
        var m1 = new Marketfi();
        var m2 = new Marketfi();

        // Bar 1: range=20, vol=1000 → MFI=0.02
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000.0);
        // Bar 2: range=40, vol=1000 → MFI=0.04
        var bar2 = new TBar(DateTime.UtcNow, 100, 120, 80, 100, 1000.0);

        double mfi1 = m1.Update(bar1).Value;
        double mfi2 = m2.Update(bar2).Value;

        Assert.Equal(mfi1 * 2.0, mfi2, Tolerance);
    }

    // ── NaN safety ───────────────────────────────────────────────────────────

    [Fact]
    public void NaN_InputDoesNotProduceNaN()
    {
        var m = new Marketfi();
        m.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));

        var nanBar = new TBar(DateTime.UtcNow.AddMinutes(1), 100, double.NaN, double.NaN, 100, double.NaN);
        var result = m.Update(nanBar);
        Assert.True(double.IsFinite(result.Value));
    }

    // ── AllModes: streaming == batch final value ──────────────────────────────

    [Fact]
    public void AllModes_StreamingBatch_FinalValueMatch()
    {
        const int N = 300;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 333);

        double[] hi = new double[N], lo = new double[N], vol = new double[N];
        var m = new Marketfi();

        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            hi[i] = bar.High; lo[i] = bar.Low; vol[i] = bar.Volume;
            m.Update(bar, isNew: true);
        }

        double streamFinal = m.Last.Value;

        var batchOut = new double[N];
        Marketfi.Batch(hi, lo, vol, batchOut);
        double batchFinal = batchOut[N - 1];

        Assert.Equal(streamFinal, batchFinal, Tolerance);
    }

    // ── Tulip Cross-Validation ────────────────────────────────────────────────

    /// <summary>
    /// Validates Marketfi against Tulip <c>marketfi</c>.
    /// Tulip formula: (High - Low) / Volume per bar, no lookback, no period option.
    /// Three inputs: high[], low[], volume[]. Options: {} (empty).
    /// </summary>
    [Fact]
    public void Marketfi_Matches_Tulip_Batch()
    {
        const int N = 500;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 45001);

        double[] hiData = new double[N];
        double[] loData = new double[N];
        double[] volData = new double[N];
        double[] batchOut = new double[N];

        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            hiData[i] = bar.High;
            loData[i] = bar.Low;
            volData[i] = bar.Volume;
        }

        Marketfi.Batch(hiData, loData, volData, batchOut);

        var tulipIndicator = Tulip.Indicators.marketfi;
        double[][] inputs = { hiData, loData, volData };
        double[] options = Array.Empty<double>();
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[N - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        // lookback=0 for marketfi — element-wise direct comparison
        ValidationHelper.VerifyData(batchOut, tResult, lookback, tolerance: 1e-9);
    }

    [Fact]
    public void Marketfi_Matches_Tulip_Streaming()
    {
        const int N = 500;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 45002);

        double[] hiData = new double[N];
        double[] loData = new double[N];
        double[] volData = new double[N];

        var m = new Marketfi();
        var qResults = new List<double>();

        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            hiData[i] = bar.High;
            loData[i] = bar.Low;
            volData[i] = bar.Volume;
            qResults.Add(m.Update(bar, isNew: true).Value);
        }

        var tulipIndicator = Tulip.Indicators.marketfi;
        double[][] inputs = { hiData, loData, volData };
        double[] options = Array.Empty<double>();
        int lookback = tulipIndicator.Start(options);
        double[][] outputs = { new double[N - lookback] };
        tulipIndicator.Run(inputs, options, outputs);
        double[] tResult = outputs[0];

        ValidationHelper.VerifyData(qResults, tResult, lookback, tolerance: 1e-9);
    }
}
