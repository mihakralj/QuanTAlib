// Ha Validation Tests
// Cross-validates Heikin-Ashi against Skender.Stock.Indicators GetHeikinAshi()
// plus self-consistency tests for batch/streaming/span equivalence.

using System.Runtime.CompilerServices;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation for Ha (Heikin-Ashi) indicator.
/// Cross-validates all 4 OHLC channels against Skender GetHeikinAshi(),
/// plus self-consistency (batch == streaming == span), constant convergence,
/// formula verification, and bar correction.
/// </summary>
public sealed class HaValidationTests : IDisposable
{
    private readonly ValidationTestData _data = new();
    private readonly ITestOutputHelper _output;
    private readonly GBM _gbm;
    private bool _disposed;

    private const double SelfTolerance = 1e-10;
    private const int DataSize = 5000;

    public HaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.5, seed: 42);
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

    private TBarSeries GenerateBars(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Skender Cross-Validation Tests
    // ════════════════════════════════════════════════════════════════════════

    // ── A) Skender GetHeikinAshi batch validation (all 4 OHLC channels) ──
    [Fact]
    public void Validate_Against_Skender_HeikinAshi_Batch()
    {
        var skenderResults = _data.SkenderQuotes
            .GetHeikinAshi()
            .ToList();

        var qlResult = Ha.Batch(_data.Bars);

        Assert.Equal(qlResult.Count, skenderResults.Count);

        int count = qlResult.Count;
        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);

        int failures = 0;
        for (int i = start; i < count; i++)
        {
            double qlOpen = qlResult[i].Open;
            double qlHigh = qlResult[i].High;
            double qlLow = qlResult[i].Low;
            double qlClose = qlResult[i].Close;

            double skOpen = (double)skenderResults[i].Open;
            double skHigh = (double)skenderResults[i].High;
            double skLow = (double)skenderResults[i].Low;
            double skClose = (double)skenderResults[i].Close;

            if (Math.Abs(qlOpen - skOpen) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Open mismatch at {i}: QL={qlOpen:G17}, SK={skOpen:G17}, Δ={Math.Abs(qlOpen - skOpen):G17}");
                failures++;
            }
            if (Math.Abs(qlHigh - skHigh) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"High mismatch at {i}: QL={qlHigh:G17}, SK={skHigh:G17}, Δ={Math.Abs(qlHigh - skHigh):G17}");
                failures++;
            }
            if (Math.Abs(qlLow - skLow) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Low mismatch at {i}: QL={qlLow:G17}, SK={skLow:G17}, Δ={Math.Abs(qlLow - skLow):G17}");
                failures++;
            }
            if (Math.Abs(qlClose - skClose) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Close mismatch at {i}: QL={qlClose:G17}, SK={skClose:G17}, Δ={Math.Abs(qlClose - skClose):G17}");
                failures++;
            }
        }

        Assert.True(failures == 0, $"Skender batch validation: {failures} OHLC channel mismatches in bars {start}..{count - 1}");
        _output.WriteLine($"HA vs Skender GetHeikinAshi batch: {count} bars, last {count - start} verified (4 channels) within {ValidationHelper.SkenderTolerance}: PASSED");
    }

    // ── B) Skender GetHeikinAshi streaming validation ─────────────────────
    [Fact]
    public void Validate_Against_Skender_HeikinAshi_Streaming()
    {
        var skenderResults = _data.SkenderQuotes
            .GetHeikinAshi()
            .ToList();

        var ind = new Ha();
        int count = _data.Bars.Count;
        double[] sOpen = new double[count];
        double[] sHigh = new double[count];
        double[] sLow = new double[count];
        double[] sClose = new double[count];

        for (int i = 0; i < count; i++)
        {
            var ha = ind.UpdateBar(_data.Bars[i], isNew: true);
            sOpen[i] = ha.Open;
            sHigh[i] = ha.High;
            sLow[i] = ha.Low;
            sClose[i] = ha.Close;
        }

        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        int failures = 0;

        for (int i = start; i < count; i++)
        {
            double skOpen = (double)skenderResults[i].Open;
            double skHigh = (double)skenderResults[i].High;
            double skLow = (double)skenderResults[i].Low;
            double skClose = (double)skenderResults[i].Close;

            if (Math.Abs(sOpen[i] - skOpen) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Stream Open mismatch at {i}: QL={sOpen[i]:G17}, SK={skOpen:G17}");
                failures++;
            }
            if (Math.Abs(sHigh[i] - skHigh) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Stream High mismatch at {i}: QL={sHigh[i]:G17}, SK={skHigh:G17}");
                failures++;
            }
            if (Math.Abs(sLow[i] - skLow) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Stream Low mismatch at {i}: QL={sLow[i]:G17}, SK={skLow:G17}");
                failures++;
            }
            if (Math.Abs(sClose[i] - skClose) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Stream Close mismatch at {i}: QL={sClose[i]:G17}, SK={skClose:G17}");
                failures++;
            }
        }

        Assert.True(failures == 0, $"Skender streaming validation: {failures} OHLC channel mismatches");
        _output.WriteLine($"HA streaming vs Skender GetHeikinAshi: {count} bars, last {count - start} verified: PASSED");
    }

    // ── C) Skender GetHeikinAshi span validation ─────────────────────────
    [Fact]
    [SkipLocalsInit]
    public void Validate_Against_Skender_HeikinAshi_Span()
    {
        var skenderResults = _data.SkenderQuotes
            .GetHeikinAshi()
            .ToList();

        int count = _data.Bars.Count;
        double[] o = new double[count];
        double[] h = new double[count];
        double[] l = new double[count];
        double[] c = new double[count];

        for (int i = 0; i < count; i++)
        {
            o[i] = _data.Bars[i].Open;
            h[i] = _data.Bars[i].High;
            l[i] = _data.Bars[i].Low;
            c[i] = _data.Bars[i].Close;
        }

        double[] haO = new double[count];
        double[] haH = new double[count];
        double[] haL = new double[count];
        double[] haC = new double[count];
        Ha.Batch(o.AsSpan(), h.AsSpan(), l.AsSpan(), c.AsSpan(),
            haO.AsSpan(), haH.AsSpan(), haL.AsSpan(), haC.AsSpan());

        int start = Math.Max(0, count - ValidationHelper.DefaultVerificationCount);
        int failures = 0;

        for (int i = start; i < count; i++)
        {
            double skOpen = (double)skenderResults[i].Open;
            double skHigh = (double)skenderResults[i].High;
            double skLow = (double)skenderResults[i].Low;
            double skClose = (double)skenderResults[i].Close;

            if (Math.Abs(haO[i] - skOpen) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Span Open mismatch at {i}: QL={haO[i]:G17}, SK={skOpen:G17}");
                failures++;
            }
            if (Math.Abs(haH[i] - skHigh) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Span High mismatch at {i}: QL={haH[i]:G17}, SK={skHigh:G17}");
                failures++;
            }
            if (Math.Abs(haL[i] - skLow) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Span Low mismatch at {i}: QL={haL[i]:G17}, SK={skLow:G17}");
                failures++;
            }
            if (Math.Abs(haC[i] - skClose) > ValidationHelper.SkenderTolerance)
            {
                _output.WriteLine($"Span Close mismatch at {i}: QL={haC[i]:G17}, SK={skClose:G17}");
                failures++;
            }
        }

        Assert.True(failures == 0, $"Skender span validation: {failures} OHLC channel mismatches");
        _output.WriteLine($"HA span vs Skender GetHeikinAshi: {count} bars, last {count - start} verified: PASSED");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Self-Consistency Tests
    // ════════════════════════════════════════════════════════════════════════

    // ── D) Batch == Streaming ─────────────────────────────────────────────
    [Fact]
    public void BatchAndStreaming_Match()
    {
        var bars = GenerateBars(DataSize);

        // Streaming
        var streaming = new Ha();
        var streamingBars = new List<TBar>(DataSize);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingBars.Add(streaming.UpdateBar(bars[i], isNew: true));
        }

        // Batch
        var batchResult = Ha.Batch(bars);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingBars[i].Open, batchResult[i].Open, SelfTolerance);
            Assert.Equal(streamingBars[i].High, batchResult[i].High, SelfTolerance);
            Assert.Equal(streamingBars[i].Low, batchResult[i].Low, SelfTolerance);
            Assert.Equal(streamingBars[i].Close, batchResult[i].Close, SelfTolerance);
        }

        _output.WriteLine($"Batch == Streaming: {bars.Count} bars, all 4 OHLC channels matched within {SelfTolerance}: PASSED");
    }

    // ── E) Span == Streaming ──────────────────────────────────────────────
    [Fact]
    public void SpanAndStreaming_Match()
    {
        var bars = GenerateBars(DataSize);

        // Streaming
        var streaming = new Ha();
        double[] sOpen = new double[bars.Count];
        double[] sHigh = new double[bars.Count];
        double[] sLow = new double[bars.Count];
        double[] sClose = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var ha = streaming.UpdateBar(bars[i], isNew: true);
            sOpen[i] = ha.Open;
            sHigh[i] = ha.High;
            sLow[i] = ha.Low;
            sClose[i] = ha.Close;
        }

        // Span batch
        double[] haO = new double[bars.Count];
        double[] haH = new double[bars.Count];
        double[] haL = new double[bars.Count];
        double[] haC = new double[bars.Count];
        Ha.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues,
            haO, haH, haL, haC);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(sOpen[i], haO[i], SelfTolerance);
            Assert.Equal(sHigh[i], haH[i], SelfTolerance);
            Assert.Equal(sLow[i], haL[i], SelfTolerance);
            Assert.Equal(sClose[i], haC[i], SelfTolerance);
        }

        _output.WriteLine($"Span == Streaming: {bars.Count} bars matched within {SelfTolerance}: PASSED");
    }

    // ── F) Constant bars converge ─────────────────────────────────────────
    [Fact]
    public void ConstantBars_ConvergeToConstant()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;
        double price = 50.0;

        TBar last = default;
        for (int i = 0; i < 100; i++)
        {
            last = indicator.UpdateBar(new TBar(time.AddMinutes(i), price, price, price, price, 1000), isNew: true);
        }

        Assert.Equal(price, last.Open, 1e-6);
        Assert.Equal(price, last.High, 1e-6);
        Assert.Equal(price, last.Low, 1e-6);
        Assert.Equal(price, last.Close, 1e-6);

        _output.WriteLine($"Constant convergence: price={price}, all OHLC matched: PASSED");
    }

    // ── G) HA Close always equals OHLC4 of source bar ─────────────────────
    [Fact]
    public void HaClose_AlwaysEqualsOHLC4()
    {
        var bars = GenerateBars(DataSize);
        var indicator = new Ha();

        for (int i = 0; i < bars.Count; i++)
        {
            var ha = indicator.UpdateBar(bars[i], isNew: true);
            double expected = bars[i].OHLC4;
            Assert.Equal(expected, ha.Close, SelfTolerance);
        }

        _output.WriteLine($"HA Close == source OHLC4: {bars.Count} bars verified: PASSED");
    }

    // ── H) HA High/Low always contain body ────────────────────────────────
    [Fact]
    public void HaHighLow_AlwaysContainBody()
    {
        var bars = GenerateBars(DataSize);
        var indicator = new Ha();

        for (int i = 0; i < bars.Count; i++)
        {
            var ha = indicator.UpdateBar(bars[i], isNew: true);
            Assert.True(ha.High >= ha.Open, $"Bar {i}: High {ha.High} < Open {ha.Open}");
            Assert.True(ha.High >= ha.Close, $"Bar {i}: High {ha.High} < Close {ha.Close}");
            Assert.True(ha.Low <= ha.Open, $"Bar {i}: Low {ha.Low} > Open {ha.Open}");
            Assert.True(ha.Low <= ha.Close, $"Bar {i}: Low {ha.Low} > Close {ha.Close}");
        }

        _output.WriteLine($"HA High/Low contain body: {bars.Count} bars verified: PASSED");
    }

    // ── I) Bar correction consistency ─────────────────────────────────────
    [Fact]
    public void BarCorrection_Consistency()
    {
        var bars = GenerateBars(100);
        var indicator1 = new Ha();
        var indicator2 = new Ha();

        // Run indicator1 normally
        for (int i = 0; i < bars.Count; i++)
        {
            indicator1.UpdateBar(bars[i], isNew: true);
        }

        // Run indicator2 with corrections
        for (int i = 0; i < bars.Count; i++)
        {
            indicator2.UpdateBar(bars[i], isNew: true);
            // Simulate correction
            if (i > 0 && i % 5 == 0)
            {
                indicator2.UpdateBar(bars[i], isNew: false);
            }
        }

        Assert.Equal(indicator1.LastBar.Open, indicator2.LastBar.Open, SelfTolerance);
        Assert.Equal(indicator1.LastBar.Close, indicator2.LastBar.Close, SelfTolerance);

        _output.WriteLine("Bar correction consistency: PASSED");
    }

    // ── J) Calculate returns hot indicator ─────────────────────────────────
    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var bars = GenerateBars(50);
        var (results, indicator) = Ha.Calculate(bars);
        Assert.True(indicator.IsHot);
        Assert.Equal(bars.Count, results.Count);

        _output.WriteLine($"Calculate returns hot indicator: {results.Count} bars, IsHot=true: PASSED");
    }
}
