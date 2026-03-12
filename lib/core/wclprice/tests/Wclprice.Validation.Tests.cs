using System.Runtime.CompilerServices;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation for Wclprice (Weighted Close Price) = (H+L+2*C)/4.
/// Cross-validated against TA-Lib WCLPRICE (exact match expected).
/// Skender, Tulip, and Ooples do not implement WCLPRICE as a standalone function.
/// </summary>
public sealed class WclpriceValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public WclpriceValidationTests(ITestOutputHelper output)
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

    // ── A) Cross-validate with TA-Lib WCLPRICE ────────────────────────────────
    [Fact]
    public void TALib_WclPrice_Batch_Validates()
    {
        double[] high = _data.HighPrices.ToArray();
        double[] low = _data.LowPrices.ToArray();
        double[] close = _data.ClosePrices.ToArray();

        // TA-Lib WclPrice
        var taOut = new double[high.Length];
        var retCode = Functions.WclPrice(high.AsSpan(), low.AsSpan(), close.AsSpan(),
            0..^0, taOut, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);
        var (offset, length) = outRange.GetOffsetAndLength(taOut.Length);

        // QuanTAlib batch span
        var qlOut = new double[high.Length];
        Wclprice.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(), qlOut.AsSpan());

        int mismatches = 0;
        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            double err = Math.Abs(qlOut[qi] - taOut[j]);
            if (err > ValidationHelper.TalibTolerance) { mismatches++; }
        }

        double mismatchRate = (double)mismatches / length;
        _output.WriteLine($"TALib WCLPRICE: {length} compared, {mismatches} mismatches ({mismatchRate:P2})");
        Assert.Equal(0, mismatches);
    }

    // ── B) Streaming == Batch span ────────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch()
    {
        const int N = 200;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1003);
        var bars = new TBar[N];
        for (int i = 0; i < N; i++) { bars[i] = gbm.Next(isNew: true); }

        // Streaming
        var ind = new Wclprice();
        for (int i = 0; i < N; i++) { ind.Update(bars[i], isNew: true); }
        double streamVal = ind.Last.Value;

        // Batch span
        double[] h = new double[N], l = new double[N], c = new double[N];
        for (int i = 0; i < N; i++) { h[i] = bars[i].High; l[i] = bars[i].Low; c[i] = bars[i].Close; }
        var qlOut = new double[N];
        Wclprice.Batch(h.AsSpan(), l.AsSpan(), c.AsSpan(), qlOut.AsSpan());

        _output.WriteLine($"Streaming={streamVal:F10}, Batch={qlOut[N - 1]:F10}");
        Assert.Equal(streamVal, qlOut[N - 1], 1e-12);
    }

    // ── C) Formula verification: (H+L+2*C)/4 ─────────────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        var bar = new TBar(DateTime.UtcNow, open: 10.0, high: 20.0, low: 8.0, close: 16.0, volume: 1000);
        var ind = new Wclprice();
        var result = ind.Update(bar, isNew: true);
        double expected = (20.0 + 8.0 + 2.0 * 16.0) / 4.0; // = 15.0
        Assert.Equal(expected, result.Value, 1e-12);
        _output.WriteLine($"WCLPRICE formula: expected={expected}, actual={result.Value}: PASSED");
    }

    // ── D) Batch(TBarSeries) == Calculate ─────────────────────────────────────
    [Fact]
    public void Validate_BatchBarSeries_Equals_Calculate()
    {
        var (results, _) = Wclprice.Calculate(_data.Bars);
        var batchResult = Wclprice.Batch(_data.Bars);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], results.Values[i], 1e-12);
        }
        _output.WriteLine("WCLPRICE Batch(TBarSeries) == Calculate: PASSED");
    }

    // ── E) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        var r1 = Wclprice.Batch(_data.Bars);
        var r2 = Wclprice.Batch(_data.Bars);
        for (int i = 0; i < r1.Count; i++) { Assert.Equal(r1.Values[i], r2.Values[i], 15); }
        _output.WriteLine("WCLPRICE determinism: PASSED");
    }
}
