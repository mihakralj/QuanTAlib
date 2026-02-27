using System.Runtime.CompilerServices;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation for Midprice (Midpoint Price) = (Highest(H,N) + Lowest(L,N)) / 2.
/// Cross-validated against TA-Lib MIDPRICE (exact match expected).
/// Skender, Tulip, and Ooples do not implement MIDPRICE as a standalone function.
/// </summary>
public sealed class MidpriceValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public MidpriceValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _data.Dispose();
            _disposed = true;
        }
    }

    // ── A) Cross-validate with TA-Lib MIDPRICE ────────────────────────────────
    [Fact]
    public void TALib_MidPrice_Batch_Validates_Period14()
    {
        const int period = 14;
        double[] high = _data.HighPrices.ToArray();
        double[] low = _data.LowPrices.ToArray();

        // TA-Lib MidPrice
        var taOut = new double[high.Length];
        var retCode = Functions.MidPrice(high.AsSpan(), low.AsSpan(), 0..^0, taOut, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);
        var (offset, length) = outRange.GetOffsetAndLength(taOut.Length);

        // QuanTAlib batch span
        var qlOut = new double[high.Length];
        Midprice.Batch(high.AsSpan(), low.AsSpan(), qlOut.AsSpan(), period);

        int mismatches = 0;
        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            double err = Math.Abs(qlOut[qi] - taOut[j]);
            if (err > ValidationHelper.TalibTolerance) { mismatches++; }
        }

        double mismatchRate = (double)mismatches / length;
        _output.WriteLine($"TALib MIDPRICE(14): {length} compared, {mismatches} mismatches ({mismatchRate:P2})");
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void TALib_MidPrice_Batch_Validates_Period5()
    {
        const int period = 5;
        double[] high = _data.HighPrices.ToArray();
        double[] low = _data.LowPrices.ToArray();

        var taOut = new double[high.Length];
        var retCode = Functions.MidPrice(high.AsSpan(), low.AsSpan(), 0..^0, taOut, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);
        var (offset, length) = outRange.GetOffsetAndLength(taOut.Length);

        var qlOut = new double[high.Length];
        Midprice.Batch(high.AsSpan(), low.AsSpan(), qlOut.AsSpan(), period);

        int mismatches = 0;
        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            double err = Math.Abs(qlOut[qi] - taOut[j]);
            if (err > ValidationHelper.TalibTolerance) { mismatches++; }
        }

        _output.WriteLine($"TALib MIDPRICE(5): {length} compared, {mismatches} mismatches");
        Assert.Equal(0, mismatches);
    }

    // ── B) Streaming == Batch span ────────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch()
    {
        const int N = 200;
        const int period = 14;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var bars = new TBar[N];
        for (int i = 0; i < N; i++) { bars[i] = gbm.Next(isNew: true); }

        // Streaming
        var ind = new Midprice(period);
        for (int i = 0; i < N; i++) { ind.Update(bars[i], isNew: true); }
        double streamVal = ind.Last.Value;

        // Batch span
        double[] h = new double[N], l = new double[N];
        for (int i = 0; i < N; i++) { h[i] = bars[i].High; l[i] = bars[i].Low; }
        var qlOut = new double[N];
        Midprice.Batch(h.AsSpan(), l.AsSpan(), qlOut.AsSpan(), period);

        _output.WriteLine($"Streaming={streamVal:F10}, Batch={qlOut[N - 1]:F10}");
        Assert.Equal(streamVal, qlOut[N - 1], 1e-12);
    }

    // ── C) Formula verification: (HH5 + LL5) / 2 ─────────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        // Prices for 5 bars: H=[10,12,15,11,13], L=[8,9,10,7,9]
        // Highest H over 5 = 15, Lowest L over 5 = 7 → midprice = (15+7)/2 = 11
        const int period = 5;
        double[] highs = [10.0, 12.0, 15.0, 11.0, 13.0];
        double[] lows  = [8.0,  9.0, 10.0,  7.0,  9.0];

        var output = new double[5];
        Midprice.Batch(highs.AsSpan(), lows.AsSpan(), output.AsSpan(), period);

        double expected = (15.0 + 7.0) / 2.0;
        Assert.Equal(expected, output[4], 1e-12);
        _output.WriteLine($"MIDPRICE formula: expected={expected}, actual={output[4]}: PASSED");
    }

    // ── D) Batch(TBarSeries) == Calculate ─────────────────────────────────────
    [Fact]
    public void Validate_BatchBarSeries_Equals_Calculate()
    {
        const int period = 14;
        var (results, _) = Midprice.Calculate(_data.Bars, period);
        var batchResult = Midprice.Batch(_data.Bars, period);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], results.Values[i], 1e-12);
        }
        _output.WriteLine("MIDPRICE Batch(TBarSeries) == Calculate: PASSED");
    }

    // ── E) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        const int period = 14;
        var r1 = Midprice.Batch(_data.Bars, period);
        var r2 = Midprice.Batch(_data.Bars, period);
        for (int i = 0; i < r1.Count; i++) { Assert.Equal(r1.Values[i], r2.Values[i], 15); }
        _output.WriteLine("MIDPRICE determinism: PASSED");
    }
}
