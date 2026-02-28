using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation for Avgprice (Average Price) = (O+H+L+C)/4.
/// Cross-validated against TA-Lib AVGPRICE and Skender CandlePart.OHLC4.
/// </summary>
public sealed class AvgpriceValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AvgpriceValidationTests(ITestOutputHelper output)
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

    // ── A) Cross-validate with TA-Lib AVGPRICE ────────────────────────────────
    [Fact]
    public void TALib_AvgPrice_Batch_Validates()
    {
        double[] open = _data.OpenPrices.ToArray();
        double[] high = _data.HighPrices.ToArray();
        double[] low = _data.LowPrices.ToArray();
        double[] close = _data.ClosePrices.ToArray();

        // TA-Lib AvgPrice
        var taOut = new double[open.Length];
        var retCode = Functions.AvgPrice(open.AsSpan(), high.AsSpan(), low.AsSpan(), close.AsSpan(),
            0..^0, taOut, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);
        var (offset, length) = outRange.GetOffsetAndLength(taOut.Length);

        // QuanTAlib batch span
        var qlOut = new double[open.Length];
        Avgprice.Batch(open.AsSpan(), high.AsSpan(), low.AsSpan(), close.AsSpan(), qlOut.AsSpan());

        int mismatches = 0;
        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            double err = Math.Abs(qlOut[qi] - taOut[j]);
            if (err > ValidationHelper.TalibTolerance) { mismatches++; }
        }

        double mismatchRate = (double)mismatches / length;
        _output.WriteLine($"TALib AVGPRICE: {length} compared, {mismatches} mismatches ({mismatchRate:P2})");
        Assert.Equal(0, mismatches);
    }

    // ── B) Streaming == Batch span ────────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch()
    {
        const int N = 200;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1001);
        var bars = new TBar[N];
        for (int i = 0; i < N; i++) { bars[i] = gbm.Next(isNew: true); }

        // Streaming
        var ind = new Avgprice();
        for (int i = 0; i < N; i++) { ind.Update(bars[i], isNew: true); }
        double streamVal = ind.Last.Value;

        // Batch span
        double[] o = new double[N], h = new double[N], l = new double[N], c = new double[N];
        for (int i = 0; i < N; i++) { o[i] = bars[i].Open; h[i] = bars[i].High; l[i] = bars[i].Low; c[i] = bars[i].Close; }
        var qlOut = new double[N];
        Avgprice.Batch(o.AsSpan(), h.AsSpan(), l.AsSpan(), c.AsSpan(), qlOut.AsSpan());

        _output.WriteLine($"Streaming={streamVal:F10}, Batch={qlOut[N - 1]:F10}");
        Assert.Equal(streamVal, qlOut[N - 1], 1e-12);
    }

    // ── C) Formula verification: (O+H+L+C)/4 ─────────────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        var bar = new TBar(DateTime.UtcNow, open: 10.0, high: 20.0, low: 5.0, close: 15.0, volume: 1000);
        var ind = new Avgprice();
        var result = ind.Update(bar, isNew: true);
        double expected = (10.0 + 20.0 + 5.0 + 15.0) / 4.0; // = 12.5
        Assert.Equal(expected, result.Value, 1e-12);
        _output.WriteLine($"AVGPRICE formula: expected={expected}, actual={result.Value}: PASSED");
    }

    // ── D) Batch(TBarSeries) == Calculate ─────────────────────────────────────
    [Fact]
    public void Validate_BatchBarSeries_Equals_Calculate()
    {
        var (results, _) = Avgprice.Calculate(_data.Bars);
        var batchResult = Avgprice.Batch(_data.Bars);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], results.Values[i], 1e-12);
        }
        _output.WriteLine("AVGPRICE Batch(TBarSeries) == Calculate: PASSED");
    }

    // ── E) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        var r1 = Avgprice.Batch(_data.Bars);
        var r2 = Avgprice.Batch(_data.Bars);
        for (int i = 0; i < r1.Count; i++) { Assert.Equal(r1.Values[i], r2.Values[i], 15); }
        _output.WriteLine("AVGPRICE determinism: PASSED");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Skender.Stock.Indicators Validation — CandlePart.OHLC4
    // ═══════════════════════════════════════════════════════════════════════════

    // ── F) Skender OHLC4 batch validation ─────────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_OHLC4_Batch()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OHLC4)
            .ToList();

        var qlResult = Avgprice.Batch(_data.Bars);

        Assert.Equal(qlResult.Count, skenderResults.Count);

        int count = qlResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            double qlVal = qlResult.Values[i];
            double skVal = skenderResults[i].Value;

            Assert.True(
                Math.Abs(qlVal - skVal) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qlVal:G17}, Skender={skVal:G17}, Diff={Math.Abs(qlVal - skVal):G17}");
        }

        _output.WriteLine($"AVGPRICE vs Skender OHLC4 batch: {count} bars, last {count - start} verified within {ValidationHelper.SkenderTolerance}: PASSED");
    }

    // ── G) Skender OHLC4 streaming validation ─────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_OHLC4_Streaming()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OHLC4)
            .ToList();

        var ind = new Avgprice();
        int count = _data.Bars.Count;
        double[] streamValues = new double[count];

        for (int i = 0; i < count; i++)
        {
            var result = ind.Update(_data.Bars[i], isNew: true);
            streamValues[i] = result.Value;
        }

        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            double qlVal = streamValues[i];
            double skVal = skenderResults[i].Value;

            Assert.True(
                Math.Abs(qlVal - skVal) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qlVal:G17}, Skender={skVal:G17}");
        }

        _output.WriteLine($"AVGPRICE streaming vs Skender OHLC4: {count} bars, last {count - start} verified: PASSED");
    }

    // ── H) Skender OHLC4 span validation ──────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Skender_OHLC4_Span()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OHLC4)
            .ToList();

        int count = _data.Bars.Count;
        double[] o = new double[count], h = new double[count], l = new double[count], c = new double[count];
        for (int i = 0; i < count; i++)
        {
            o[i] = _data.Bars[i].Open;
            h[i] = _data.Bars[i].High;
            l[i] = _data.Bars[i].Low;
            c[i] = _data.Bars[i].Close;
        }

        var qlOut = new double[count];
        Avgprice.Batch(o.AsSpan(), h.AsSpan(), l.AsSpan(), c.AsSpan(), qlOut.AsSpan());

        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            double qlVal = qlOut[i];
            double skVal = skenderResults[i].Value;

            Assert.True(
                Math.Abs(qlVal - skVal) <= ValidationHelper.SkenderTolerance,
                $"Span mismatch at index {i}: QuanTAlib={qlVal:G17}, Skender={skVal:G17}");
        }

        _output.WriteLine($"AVGPRICE span vs Skender OHLC4: {count} bars, last {count - start} verified: PASSED");
    }
}
