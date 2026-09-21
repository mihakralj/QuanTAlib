using TALib;
using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for AtrBands against external libraries.
/// AtrBands: Middle = SMA(Close), Upper/Lower = Middle ± ATR × multiplier.
/// TALib provides SMA and ATR sub-component validation.
/// Skender provides SMA and ATR sub-component validation.
/// </summary>
public sealed class AtrBandsValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AtrBandsValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose() => Dispose(true);

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Internal Consistency Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] multipliers = { 1.0, 2.0, 2.5 };

        foreach (int period in periods)
        {
            foreach (double multiplier in multipliers)
            {
                // Batch (static)
                var (bMid, bUp, bLo) = AtrBands.Batch(_testData.Bars, period, multiplier);

                // Streaming
                var streaming = new AtrBands(period, multiplier);
                var sMid = new TSeries();
                var sUp = new TSeries();
                var sLo = new TSeries();
                foreach (var bar in _testData.Bars)
                {
                    streaming.Update(bar);
                    sMid.Add(streaming.Last);
                    sUp.Add(streaming.Upper);
                    sLo.Add(streaming.Lower);
                }

                ValidationHelper.VerifySeriesEqual(bMid, sMid);
                ValidationHelper.VerifySeriesEqual(bUp, sUp);
                ValidationHelper.VerifySeriesEqual(bLo, sLo);

                // Span
                double[] high = _testData.HighPrices.ToArray();
                double[] low = _testData.LowPrices.ToArray();
                double[] close = _testData.ClosePrices.ToArray();
                double[] spanMid = new double[high.Length];
                double[] spanUp = new double[high.Length];
                double[] spanLo = new double[high.Length];
                AtrBands.Batch(
                    new AtrBands.AtrBandsInput(high.AsSpan(), low.AsSpan(), close.AsSpan()),
                    new AtrBands.AtrBandsOutput(spanMid.AsSpan(), spanUp.AsSpan(), spanLo.AsSpan()),
                    period, multiplier);

                for (int i = 0; i < high.Length; i++)
                {
                    Assert.Equal(bMid[i].Value, spanMid[i], 9);
                    Assert.Equal(bUp[i].Value, spanUp[i], 9);
                    Assert.Equal(bLo[i].Value, spanLo[i], 9);
                }
            }
        }

        _output.WriteLine("AtrBands mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_BandSymmetry()
    {
        var (mid, up, lo) = AtrBands.Batch(_testData.Bars, 20, 2.0);

        for (int i = 0; i < mid.Count; i++)
        {
            double upperWidth = up[i].Value - mid[i].Value;
            double lowerWidth = mid[i].Value - lo[i].Value;
            Assert.Equal(upperWidth, lowerWidth, 1e-10);
        }

        _output.WriteLine("AtrBands band symmetry validated");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (mid, up, lo) = AtrBands.Batch(_testData.Bars, 50, 2.0);

        ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
        ValidationHelper.VerifyAllFinite(up, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

        for (int i = 1; i < mid.Count; i++)
        {
            Assert.True(up[i].Value >= lo[i].Value, $"Upper >= Lower at {i}");
        }

        _output.WriteLine("AtrBands large dataset validated");
    }

    [Fact]
    public void Validate_MultiplierScaling()
    {
        double[] multipliers = { 1.0, 2.0, 3.0, 4.0 };
        double[] widths = new double[multipliers.Length];

        for (int i = 0; i < multipliers.Length; i++)
        {
            var ind = new AtrBands(20, multipliers[i]);
            foreach (var bar in _testData.Bars)
            {
                ind.Update(bar);
            }
            widths[i] = ind.Upper.Value - ind.Lower.Value;
        }

        double baseWidth = widths[0];
        for (int i = 1; i < multipliers.Length; i++)
        {
            double expected = baseWidth * multipliers[i];
            Assert.Equal(expected, widths[i], 1e-9);
        }

        _output.WriteLine("AtrBands multiplier scaling validated");
    }

    // ═══════════════════════════════════════════════════════════════
    //  TALib Sub-Component Validation
    //  AtrBands middle band = SMA(Close, period) → validates against TALib SMA
    //  AtrBands band width ∝ ATR → validates ATR component against TALib ATR
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_Talib_SMA_MiddleBand()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        double[] closeData = _testData.ClosePrices.ToArray();
        double[] smaOutput = new double[closeData.Length];

        foreach (var period in periods)
        {
            var (qMid, _, _) = AtrBands.Batch(_testData.Bars, period, 2.0);

            var retCode = Functions.Sma<double>(
                closeData,
                0..^0,
                smaOutput,
                out var outRange,
                period);

            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = Functions.SmaLookback(period);

            ValidationHelper.VerifyData(qMid, smaOutput, outRange, lookback);
        }
        _output.WriteLine("AtrBands middle band validated against TALib SMA for all periods");
    }

    [Fact]
    public void Validate_Talib_ATR_BandWidth()
    {
        // AtrBands: width = 2 × multiplier × ATR, so half-width = multiplier × ATR
        // We validate that (Upper - Middle) / multiplier ≈ ATR from TALib
        int[] periods = { 10, 20, 50 };
        double multiplier = 2.0;

        double[] highData = _testData.HighPrices.ToArray();
        double[] lowData = _testData.LowPrices.ToArray();
        double[] closeData = _testData.ClosePrices.ToArray();
        double[] atrOutput = new double[closeData.Length];

        foreach (var period in periods)
        {
            var (qMid, qUp, _) = AtrBands.Batch(_testData.Bars, period, multiplier);

            var retCode = Functions.Atr<double>(
                highData,
                lowData,
                closeData,
                0..^0,
                atrOutput,
                out var outRange,
                period);

            Assert.Equal(TALib.Core.RetCode.Success, retCode);

            int lookback = Functions.AtrLookback(period);
            var (offset, _) = outRange.GetOffsetAndLength(atrOutput.Length);

            // Compare extracted ATR from our bands vs TALib ATR
            int count = qMid.Count;
            int start = Math.Max(0, count - 100);
            for (int i = start; i < count; i++)
            {
                double ourAtr = (qUp[i].Value - qMid[i].Value) / multiplier;

                if (i < lookback)
                {
                    continue;
                }

                int tIndex = i - offset;
                if (tIndex < 0 || tIndex >= atrOutput.Length)
                {
                    continue;
                }

                double talibAtr = atrOutput[tIndex];

                Assert.True(
                    Math.Abs(ourAtr - talibAtr) <= ValidationHelper.TalibTolerance,
                    $"ATR mismatch at {i}: QuanTAlib={ourAtr:G17}, TALib={talibAtr:G17}");
            }
        }
        _output.WriteLine("AtrBands ATR component validated against TALib ATR for all periods");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Skender Sub-Component Validation
    //  Middle band = SMA → validates against Skender GetSma()
    //  Band width ∝ ATR → validates against Skender GetAtr()
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_Skender_SMA_MiddleBand()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            var (qMid, _, _) = AtrBands.Batch(_testData.Bars, period, 2.0);

            var sResult = _testData.SkenderQuotes
                .GetSma(period)
                .ToList();

            ValidationHelper.VerifyData(qMid, sResult, s => s.Sma);
        }
        _output.WriteLine("AtrBands middle band validated against Skender SMA for all periods");
    }

    [Fact]
    public void Validate_Skender_ATR_BandWidth()
    {
        int[] periods = { 10, 20, 50 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            var (qMid, qUp, _) = AtrBands.Batch(_testData.Bars, period, multiplier);

            var sResult = _testData.SkenderQuotes
                .GetAtr(period)
                .ToList();

            // Compare extracted ATR from our bands vs Skender ATR
            int count = qMid.Count;
            int start = Math.Max(0, count - 100);
            for (int i = start; i < count; i++)
            {
                double ourAtr = (qUp[i].Value - qMid[i].Value) / multiplier;
                double? skenderAtr = sResult[i].Atr;

                if (!skenderAtr.HasValue)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(ourAtr - skenderAtr.Value) <= ValidationHelper.SkenderTolerance,
                    $"ATR mismatch at {i}: QuanTAlib={ourAtr:G17}, Skender={skenderAtr.Value:G17}");
            }
        }
        _output.WriteLine("AtrBands ATR component validated against Skender ATR for all periods");
    }

    [Fact]
    public void Validate_Skender_BandStructure()
    {
        var period = 20;
        var multiplier = 2.0;

        var (qMid, qUp, qLo) = AtrBands.Batch(_testData.Bars, period, multiplier);

        var smaResult = _testData.SkenderQuotes.GetSma(period).ToList();
        var atrResult = _testData.SkenderQuotes.GetAtr(period).ToList();

        // Compare only the last 100 fully-converged values to avoid
        // warmup divergence between QuanTAlib and Skender ATR implementations
        int count = qMid.Count;
        int start = Math.Max(0, count - 100);
        int matched = 0;
        for (int i = start; i < count; i++)
        {
            if (!smaResult[i].Sma.HasValue || !atrResult[i].Atr.HasValue)
            {
                continue;
            }

            double expectedMid = smaResult[i].Sma!.Value;
            double expectedAtr = atrResult[i].Atr!.Value;
            double expectedUp = expectedMid + (multiplier * expectedAtr);
            double expectedLo = expectedMid - (multiplier * expectedAtr);

            Assert.True(
                Math.Abs(qMid[i].Value - expectedMid) <= ValidationHelper.SkenderTolerance,
                $"Middle mismatch at {i}: QuanTAlib={qMid[i].Value:G17}, Skender={expectedMid:G17}");
            Assert.True(
                Math.Abs(qUp[i].Value - expectedUp) <= ValidationHelper.SkenderTolerance,
                $"Upper mismatch at {i}: QuanTAlib={qUp[i].Value:G17}, Skender={expectedUp:G17}");
            Assert.True(
                Math.Abs(qLo[i].Value - expectedLo) <= ValidationHelper.SkenderTolerance,
                $"Lower mismatch at {i}: QuanTAlib={qLo[i].Value:G17}, Skender={expectedLo:G17}");
            matched++;
        }

        Assert.True(matched >= 50, $"Expected at least 50 matched values, got {matched}");
        _output.WriteLine($"AtrBands full band structure validated against Skender SMA+ATR ({matched} converged values)");
    }
}
