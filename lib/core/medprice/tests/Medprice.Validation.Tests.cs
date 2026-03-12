using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;
using TALib;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation for Medprice (Median Price) = (H+L)/2.
/// Cross-validated against TA-Lib MEDPRICE and Skender CandlePart.HL2.
/// </summary>
public sealed class MedpriceValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public MedpriceValidationTests(ITestOutputHelper output)
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

    // ── A) Cross-validate with TA-Lib MEDPRICE ────────────────────────────────
    [Fact]
    public void TALib_MedPrice_Batch_Validates()
    {
        double[] high = _data.HighPrices.ToArray();
        double[] low = _data.LowPrices.ToArray();

        // TA-Lib MedPrice
        var taOut = new double[high.Length];
        var retCode = Functions.MedPrice(high.AsSpan(), low.AsSpan(), 0..^0, taOut, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);
        var (offset, length) = outRange.GetOffsetAndLength(taOut.Length);

        // QuanTAlib batch via TBarSeries
        var qlOut = new double[high.Length];
        Medprice.Batch(_data.Bars, qlOut.AsSpan());

        int mismatches = 0;
        for (int j = 0; j < length; j++)
        {
            int qi = j + offset;
            double err = Math.Abs(qlOut[qi] - taOut[j]);
            if (err > ValidationHelper.TalibTolerance) { mismatches++; }
        }

        double mismatchRate = (double)mismatches / length;
        _output.WriteLine($"TALib MEDPRICE: {length} compared, {mismatches} mismatches ({mismatchRate:P2})");
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
        var ind = new Medprice();
        for (int i = 0; i < N; i++) { ind.Update(bars[i], isNew: true); }
        double streamVal = ind.Last.Value;

        // Batch span
        double[] h = new double[N], l = new double[N];
        for (int i = 0; i < N; i++) { h[i] = bars[i].High; l[i] = bars[i].Low; }
        var qlOut = new double[N];
        Medprice.Batch(h.AsSpan(), l.AsSpan(), qlOut.AsSpan());

        _output.WriteLine($"Streaming={streamVal:F10}, Batch={qlOut[N - 1]:F10}");
        Assert.Equal(streamVal, qlOut[N - 1], 1e-12);
    }

    // ── C) Formula verification: (H+L)/2 ──────────────────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        var bar = new TBar(DateTime.UtcNow, open: 10.0, high: 20.0, low: 5.0, close: 15.0, volume: 1000);
        var ind = new Medprice();
        var result = ind.Update(bar, isNew: true);
        double expected = (20.0 + 5.0) / 2.0; // = 12.5
        Assert.Equal(expected, result.Value, 1e-12);
        _output.WriteLine($"MEDPRICE formula: expected={expected}, actual={result.Value}: PASSED");
    }

    // ── D) Always hot after first bar ─────────────────────────────────────────
    [Fact]
    public void Validate_AlwaysHotAfterFirstBar()
    {
        var ind = new Medprice();
        Assert.False(ind.IsHot);
        ind.Update(new TBar(DateTime.UtcNow, 10, 12, 8, 11, 1000), isNew: true);
        Assert.True(ind.IsHot);
        _output.WriteLine("MEDPRICE always hot after first bar: PASSED");
    }

    // ── E) Batch(TBarSeries) == Calculate ─────────────────────────────────────
    [Fact]
    public void Validate_BatchBarSeries_Equals_Calculate()
    {
        var (results, _) = Medprice.Calculate(_data.Bars);
        var batchResult = Medprice.Batch(_data.Bars);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], results.Values[i], 1e-12);
        }
        _output.WriteLine("MEDPRICE Batch(TBarSeries) == Calculate: PASSED");
    }

    // ── F) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        var r1 = Medprice.Batch(_data.Bars);
        var r2 = Medprice.Batch(_data.Bars);
        for (int i = 0; i < r1.Count; i++) { Assert.Equal(r1.Values[i], r2.Values[i], 15); }
        _output.WriteLine("MEDPRICE determinism: PASSED");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Skender.Stock.Indicators Validation — CandlePart.HL2
    // ═══════════════════════════════════════════════════════════════════════════

    // ── G) Skender HL2 batch validation ───────────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_HL2_Batch()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.HL2)
            .ToList();

        var qlResult = Medprice.Batch(_data.Bars);

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

        _output.WriteLine($"MEDPRICE vs Skender HL2 batch: {count} bars, last {count - start} verified within {ValidationHelper.SkenderTolerance}: PASSED");
    }

    // ── H) Skender HL2 streaming validation ───────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_HL2_Streaming()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.HL2)
            .ToList();

        var ind = new Medprice();
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

        _output.WriteLine($"MEDPRICE streaming vs Skender HL2: {count} bars, last {count - start} verified: PASSED");
    }

    // ── I) Skender HL2 span validation ────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Skender_HL2_Span()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.HL2)
            .ToList();

        int count = _data.Bars.Count;
        double[] h = new double[count], l = new double[count];
        for (int i = 0; i < count; i++)
        {
            h[i] = _data.Bars[i].High;
            l[i] = _data.Bars[i].Low;
        }

        var qlOut = new double[count];
        Medprice.Batch(h.AsSpan(), l.AsSpan(), qlOut.AsSpan());

        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            double qlVal = qlOut[i];
            double skVal = skenderResults[i].Value;

            Assert.True(
                Math.Abs(qlVal - skVal) <= ValidationHelper.SkenderTolerance,
                $"Span mismatch at index {i}: QuanTAlib={qlVal:G17}, Skender={skVal:G17}");
        }

        _output.WriteLine($"MEDPRICE span vs Skender HL2: {count} bars, last {count - start} verified: PASSED");
    }
}
