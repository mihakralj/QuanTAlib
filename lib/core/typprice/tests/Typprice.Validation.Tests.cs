using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation for Typprice (Typical Price) = (O+H+L)/3.
/// Cross-validates against Skender.Stock.Indicators GetBaseQuote(CandlePart.OHL3),
/// plus formula verification, streaming-vs-batch consistency, and determinism.
/// </summary>
public sealed class TyppriceValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public TyppriceValidationTests(ITestOutputHelper output)
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

    // ── A) Skender OHL3 batch validation ──────────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_OHL3_Batch()
    {
        // Skender GetBaseQuote(CandlePart.OHL3) computes (Open+High+Low)/3
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OHL3)
            .ToList();

        var qlResult = Typprice.Batch(_data.Bars);

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

        _output.WriteLine($"TYPPRICE vs Skender OHL3 batch: {count} bars, last {count - start} verified within {ValidationHelper.SkenderTolerance}: PASSED");
    }

    // ── B) Skender OHL3 streaming validation ──────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_OHL3_Streaming()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OHL3)
            .ToList();

        var ind = new Typprice();
        int count = _data.Bars.Count;
        double[] streamValues = new double[count];

        for (int i = 0; i < count; i++)
        {
            var result = ind.Update(_data.Bars[i], isNew: true);
            streamValues[i] = result.Value;
        }

        // Verify last N bars
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            double qlVal = streamValues[i];
            double skVal = skenderResults[i].Value;

            Assert.True(
                Math.Abs(qlVal - skVal) <= ValidationHelper.SkenderTolerance,
                $"Mismatch at index {i}: QuanTAlib={qlVal:G17}, Skender={skVal:G17}");
        }

        _output.WriteLine($"TYPPRICE streaming vs Skender OHL3: {count} bars, last {count - start} verified: PASSED");
    }

    // ── C) Skender OHL3 span validation ───────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Skender_OHL3_Span()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OHL3)
            .ToList();

        int count = _data.Bars.Count;
        double[] o = new double[count], h = new double[count], l = new double[count];
        for (int i = 0; i < count; i++)
        {
            o[i] = _data.Bars[i].Open;
            h[i] = _data.Bars[i].High;
            l[i] = _data.Bars[i].Low;
        }

        var qlOut = new double[count];
        Typprice.Batch(o.AsSpan(), h.AsSpan(), l.AsSpan(), qlOut.AsSpan());

        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            double qlVal = qlOut[i];
            double skVal = skenderResults[i].Value;

            Assert.True(
                Math.Abs(qlVal - skVal) <= ValidationHelper.SkenderTolerance,
                $"Span mismatch at index {i}: QuanTAlib={qlVal:G17}, Skender={skVal:G17}");
        }

        _output.WriteLine($"TYPPRICE span vs Skender OHL3: {count} bars, last {count - start} verified: PASSED");
    }

    // ── D) Formula verification: (O+H+L)/3 ───────────────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        var bar = new TBar(DateTime.UtcNow, open: 10.0, high: 18.0, low: 6.0, close: 15.0, volume: 1000);
        var ind = new Typprice();
        var result = ind.Update(bar, isNew: true);
        double expected = (10.0 + 18.0 + 6.0) / 3.0; // = 11.333...
        Assert.Equal(expected, result.Value, 1e-12);
        _output.WriteLine($"TYPPRICE formula: expected={expected}, actual={result.Value}: PASSED");
    }

    // ── E) Streaming == Batch span ────────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Streaming_Equals_Batch()
    {
        const int N = 200;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 1002);
        var bars = new TBar[N];
        for (int i = 0; i < N; i++) { bars[i] = gbm.Next(isNew: true); }

        // Streaming
        var ind = new Typprice();
        for (int i = 0; i < N; i++) { ind.Update(bars[i], isNew: true); }
        double streamVal = ind.Last.Value;

        // Batch span
        double[] o = new double[N], h = new double[N], l = new double[N];
        for (int i = 0; i < N; i++) { o[i] = bars[i].Open; h[i] = bars[i].High; l[i] = bars[i].Low; }
        var qlOut = new double[N];
        Typprice.Batch(o.AsSpan(), h.AsSpan(), l.AsSpan(), qlOut.AsSpan());

        _output.WriteLine($"Streaming={streamVal:F10}, Batch={qlOut[N - 1]:F10}");
        Assert.Equal(streamVal, qlOut[N - 1], 1e-12);
    }

    // ── F) Matches TBar.OHL3 property ─────────────────────────────────────────
    [Fact]
    public void Validate_MatchesTBarOHL3()
    {
        const int N = 100;
        var gbm = new GBM(100.0, 0.05, 0.2, seed: 2001);
        var ind = new Typprice();

        int mismatches = 0;
        for (int i = 0; i < N; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = ind.Update(bar, isNew: true);
            double err = Math.Abs(result.Value - bar.OHL3);
            if (err > 1e-12) { mismatches++; }
        }

        _output.WriteLine($"TBar.OHL3 comparison: {N} bars, {mismatches} mismatches");
        Assert.Equal(0, mismatches);
    }

    // ── G) Batch(TBarSeries) == Calculate ─────────────────────────────────────
    [Fact]
    public void Validate_BatchBarSeries_Equals_Calculate()
    {
        var (results, _) = Typprice.Calculate(_data.Bars);
        var batchResult = Typprice.Batch(_data.Bars);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], results.Values[i], 1e-12);
        }
        _output.WriteLine("TYPPRICE Batch(TBarSeries) == Calculate: PASSED");
    }

    // ── H) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        var r1 = Typprice.Batch(_data.Bars);
        var r2 = Typprice.Batch(_data.Bars);
        for (int i = 0; i < r1.Count; i++) { Assert.Equal(r1.Values[i], r2.Values[i], 15); }
        _output.WriteLine("TYPPRICE determinism: PASSED");
    }

    // ── I) Skender OC2 structural validation (bonus) ──────────────────────────
    [Fact]
    public void Validate_Skender_OC2_MatchesTBarOC2()
    {
        // Verify Skender CandlePart.OC2 = (Open+Close)/2 matches TBar.OC2
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OC2)
            .ToList();

        int count = _data.Bars.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        for (int i = start; i < count; i++)
        {
            double skVal = skenderResults[i].Value;
            double tbarVal = _data.Bars[i].OC2;

            Assert.True(
                Math.Abs(skVal - tbarVal) <= ValidationHelper.SkenderTolerance,
                $"OC2 mismatch at {i}: Skender={skVal:G17}, TBar={tbarVal:G17}");
        }

        _output.WriteLine($"Skender OC2 vs TBar.OC2: {count} bars, last {count - start} verified: PASSED");
    }
}
