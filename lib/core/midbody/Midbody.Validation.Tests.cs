using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation for Midbody (Open-Close Average) = (O+C)/2.
/// Cross-validated against Skender CandlePart.OC2.
/// Note: TA-Lib does not have an OC2 function.
/// </summary>
public sealed class Oc2ValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public Oc2ValidationTests(ITestOutputHelper output)
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

    // ── A) Formula verification: (O+C)/2 ──────────────────────────────────────
    [Fact]
    public void Validate_Formula_Manual()
    {
        var bar = new TBar(DateTime.UtcNow, open: 10.0, high: 20.0, low: 5.0, close: 15.0, volume: 1000);
        var ind = new Midbody();
        var result = ind.Update(bar, isNew: true);
        double expected = (10.0 + 15.0) / 2.0; // = 12.5
        Assert.Equal(expected, result.Value, 1e-12);
        _output.WriteLine($"Midbody formula: expected={expected}, actual={result.Value}: PASSED");
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
        var ind = new Midbody();
        for (int i = 0; i < N; i++) { ind.Update(bars[i], isNew: true); }
        double streamVal = ind.Last.Value;

        // Batch span
        double[] o = new double[N], c = new double[N];
        for (int i = 0; i < N; i++) { o[i] = bars[i].Open; c[i] = bars[i].Close; }
        var qlOut = new double[N];
        Midbody.Batch(o.AsSpan(), c.AsSpan(), qlOut.AsSpan());

        _output.WriteLine($"Streaming={streamVal:F10}, Batch={qlOut[N - 1]:F10}");
        Assert.Equal(streamVal, qlOut[N - 1], 1e-12);
    }

    // ── C) Always hot after first bar ─────────────────────────────────────────
    [Fact]
    public void Validate_AlwaysHotAfterFirstBar()
    {
        var ind = new Midbody();
        Assert.False(ind.IsHot);
        ind.Update(new TBar(DateTime.UtcNow, 10, 12, 8, 11, 1000), isNew: true);
        Assert.True(ind.IsHot);
        _output.WriteLine("Midbody always hot after first bar: PASSED");
    }

    // ── D) Batch(TBarSeries) == Calculate ─────────────────────────────────────
    [Fact]
    public void Validate_BatchBarSeries_Equals_Calculate()
    {
        var (results, _) = Midbody.Calculate(_data.Bars);
        var batchResult = Midbody.Batch(_data.Bars);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], results.Values[i], 1e-12);
        }
        _output.WriteLine("Midbody Batch(TBarSeries) == Calculate: PASSED");
    }

    // ── E) Determinism ────────────────────────────────────────────────────────
    [Fact]
    public void Validate_Deterministic()
    {
        var r1 = Midbody.Batch(_data.Bars);
        var r2 = Midbody.Batch(_data.Bars);
        for (int i = 0; i < r1.Count; i++) { Assert.Equal(r1.Values[i], r2.Values[i], 15); }
        _output.WriteLine("Midbody determinism: PASSED");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Skender.Stock.Indicators Validation — CandlePart.OC2
    // ═══════════════════════════════════════════════════════════════════════════

    // ── F) Skender OC2 batch validation (Midbody mapping) ───────────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_OC2_Batch()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OC2)
            .ToList();

        var qlResult = Midbody.Batch(_data.Bars);

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

        _output.WriteLine($"Midbody vs Skender OC2 batch: {count} bars, last {count - start} verified within {ValidationHelper.SkenderTolerance}: PASSED");
    }

    // ── G) Skender OC2 streaming validation (Midbody mapping) ───────────────────────────────────
    [Fact]
    public void Validate_Against_Skender_OC2_Streaming()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OC2)
            .ToList();

        var ind = new Midbody();
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

        _output.WriteLine($"Midbody streaming vs Skender OC2: {count} bars, last {count - start} verified: PASSED");
    }

    // ── H) Skender OC2 span validation (Midbody mapping) ────────────────────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Skender_OC2_Span()
    {
        var skenderResults = _data.SkenderQuotes
            .GetBaseQuote(CandlePart.OC2)
            .ToList();

        int count = _data.Bars.Count;
        double[] o = new double[count], c = new double[count];
        for (int i = 0; i < count; i++)
        {
            o[i] = _data.Bars[i].Open;
            c[i] = _data.Bars[i].Close;
        }

        var qlOut = new double[count];
        Midbody.Batch(o.AsSpan(), c.AsSpan(), qlOut.AsSpan());

        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        for (int i = start; i < count; i++)
        {
            double qlVal = qlOut[i];
            double skVal = skenderResults[i].Value;

            Assert.True(
                Math.Abs(qlVal - skVal) <= ValidationHelper.SkenderTolerance,
                $"Span mismatch at index {i}: QuanTAlib={qlVal:G17}, Skender={skVal:G17}");
        }

        _output.WriteLine($"Midbody span vs Skender OC2: {count} bars, last {count - start} verified: PASSED");
    }
}










