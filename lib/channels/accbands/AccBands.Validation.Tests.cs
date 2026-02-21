using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for AccBands indicator.
/// Now using Headley's original formula: Upper = SMA(High*(1+factor*(H-L)/(H+L))),
/// Lower = SMA(Low*(1-factor*(H-L)/(H+L))), Middle = SMA(Close).
/// TA-Lib uses the same per-bar Headley formula with factor=4, so all three bands
/// should match exactly. Skender, Tulip, and OoplesFinance do not provide AccBands.
/// </summary>
public sealed class AccBandsValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AccBandsValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
    }

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

    [Fact]
    public void Validate_ManualCalculation_Period3()
    {
        // Manual calculation verification with Headley's formula
        // Given: High = [12, 14, 16], Low = [8, 10, 12], Close = [10, 12, 14]
        // Bar 0: w=4/20=0.2, adjH=12*(1+4*0.2)=12*1.8=21.6, adjL=8*(1-4*0.2)=8*0.2=1.6
        // Bar 1: w=4/24≈0.16667, adjH=14*(1+4/6)=14*1.66667≈23.33333, adjL=10*(1-4/6)=10*0.33333≈3.33333
        // Bar 2: w=4/28≈0.14286, adjH=16*(1+4*4/28)=16*1.57143≈25.14286, adjL=12*(1-4*4/28)=12*0.42857≈5.14286
        // SMA(3) Middle = (10+12+14)/3 = 12
        // SMA(3) Upper = (21.6 + 23.33333 + 25.14286) / 3
        // SMA(3) Lower = (1.6 + 3.33333 + 5.14286) / 3

        var series = new TBarSeries();
        var time = DateTime.UtcNow;
        series.Add(new TBar(time, 10, 12, 8, 10, 100));
        series.Add(new TBar(time.AddMinutes(1), 12, 14, 10, 12, 100));
        series.Add(new TBar(time.AddMinutes(2), 14, 16, 12, 14, 100));

        var accBands = new AccBands(3, 4.0);
        var (middle, upper, lower) = accBands.Update(series);

        double adjH0 = 12.0 * (1.0 + 4.0 * 4.0 / 20.0);
        double adjH1 = 14.0 * (1.0 + 4.0 * 4.0 / 24.0);
        double adjH2 = 16.0 * (1.0 + 4.0 * 4.0 / 28.0);
        double adjL0 = 8.0 * (1.0 - 4.0 * 4.0 / 20.0);
        double adjL1 = 10.0 * (1.0 - 4.0 * 4.0 / 24.0);
        double adjL2 = 12.0 * (1.0 - 4.0 * 4.0 / 28.0);

        Assert.Equal(12.0, middle.Last.Value, 1e-10);
        Assert.Equal((adjH0 + adjH1 + adjH2) / 3.0, upper.Last.Value, 1e-10);
        Assert.Equal((adjL0 + adjL1 + adjL2) / 3.0, lower.Last.Value, 1e-10);

        _output.WriteLine("AccBands manual calculation (period 3) validated successfully");
    }

    [Fact]
    public void Validate_ManualCalculation_Period5()
    {
        // Manual calculation verification with period 5, Headley formula
        var series = new TBarSeries();
        var time = DateTime.UtcNow;

        // Create predictable data: High = Close + 5, Low = Close - 5
        double[] closes = { 100, 102, 104, 106, 108 };
        for (int i = 0; i < closes.Length; i++)
        {
            double c = closes[i];
            series.Add(new TBar(time.AddMinutes(i), c, c + 5, c - 5, c, 1000));
        }

        var accBands = new AccBands(5, 4.0);
        var (middle, upper, lower) = accBands.Update(series);

        // SMA(Close, 5) = (100 + 102 + 104 + 106 + 108) / 5 = 104
        Assert.Equal(104.0, middle.Last.Value, 1e-10);

        // Each bar: H=c+5, L=c-5, w=10/(2c), adjH=(c+5)*(1+40/(2c)), adjL=(c-5)*(1-40/(2c))
        double sumAdjH = 0, sumAdjL = 0;
        foreach (double c in closes)
        {
            double h = c + 5;
            double l = c - 5;
            double denom = h + l;
            double w = (h - l) / denom;
            sumAdjH += h * (1.0 + 4.0 * w);
            sumAdjL += l * (1.0 - 4.0 * w);
        }
        Assert.Equal(sumAdjH / 5.0, upper.Last.Value, 1e-10);
        Assert.Equal(sumAdjL / 5.0, lower.Last.Value, 1e-10);

        _output.WriteLine("AccBands manual calculation (period 5) validated successfully");
    }

    [Fact]
    public void Validate_Factor_Effect()
    {
        // Verify factor affects band width correctly
        var series = new TBarSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            series.Add(new TBar(time.AddMinutes(i), 100, 110, 90, 100, 1000));
        }

        // With constant H=110,L=90: w = 20/200 = 0.1 per bar

        var (middle1, upper1, lower1) = AccBands.Batch(series, 5, 2.0);
        var (middle2, upper2, lower2) = AccBands.Batch(series, 5, 4.0);
        var (middle3, upper3, lower3) = AccBands.Batch(series, 5, 6.0);

        // Middle should be the same regardless of factor (SMA of Close = 100)
        Assert.Equal(middle1.Last.Value, middle2.Last.Value, 1e-10);
        Assert.Equal(middle2.Last.Value, middle3.Last.Value, 1e-10);
        Assert.Equal(100.0, middle1.Last.Value, 1e-10);

        // factor=2: adjH=110*(1+2*0.1)=110*1.2=132, adjL=90*(1-2*0.1)=90*0.8=72
        // factor=4: adjH=110*(1+4*0.1)=110*1.4=154, adjL=90*(1-4*0.1)=90*0.6=54
        // factor=6: adjH=110*(1+6*0.1)=110*1.6=176, adjL=90*(1-6*0.1)=90*0.4=36

        Assert.Equal(132.0, upper1.Last.Value, 1e-10);
        Assert.Equal(154.0, upper2.Last.Value, 1e-10);
        Assert.Equal(176.0, upper3.Last.Value, 1e-10);

        Assert.Equal(72.0, lower1.Last.Value, 1e-10);
        Assert.Equal(54.0, lower2.Last.Value, 1e-10);
        Assert.Equal(36.0, lower3.Last.Value, 1e-10);

        _output.WriteLine("AccBands factor effect validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Batch mode using instance
            var accBands = new AccBands(period, 4.0);
            var (qMiddle, qUpper, qLower) = accBands.Update(_testData.Bars);

            // Static batch
            var (sMiddle, sUpper, sLower) = AccBands.Batch(_testData.Bars, period, 4.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(qMiddle, sMiddle);
            ValidationHelper.VerifySeriesEqual(qUpper, sUpper);
            ValidationHelper.VerifySeriesEqual(qLower, sLower);
        }
        _output.WriteLine("AccBands Batch modes consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Streaming mode
            var streamingAcc = new AccBands(period, 4.0);
            var streamMiddle = new TSeries();
            var streamUpper = new TSeries();
            var streamLower = new TSeries();
            foreach (var bar in _testData.Bars)
            {
                streamingAcc.Update(bar);
                streamMiddle.Add(streamingAcc.Last);
                streamUpper.Add(streamingAcc.Upper);
                streamLower.Add(streamingAcc.Lower);
            }

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 4.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(batchMiddle, streamMiddle);
            ValidationHelper.VerifySeriesEqual(batchUpper, streamUpper);
            ValidationHelper.VerifySeriesEqual(batchLower, streamLower);
        }
        _output.WriteLine("AccBands Streaming mode consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        double[] high = _testData.HighPrices.ToArray();
        double[] low = _testData.LowPrices.ToArray();
        double[] close = _testData.ClosePrices.ToArray();

        foreach (var period in periods)
        {
            // Span mode
            int len = close.Length;
            double[] spanMiddle = new double[len];
            double[] spanUpper = new double[len];
            double[] spanLower = new double[len];

            AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan(),
                          period, 4.0);

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 4.0);

            // Verify match
            for (int i = 0; i < len; i++)
            {
                Assert.Equal(batchMiddle[i].Value, spanMiddle[i], 9);
                Assert.Equal(batchUpper[i].Value, spanUpper[i], 9);
                Assert.Equal(batchLower[i].Value, spanLower[i], 9);
            }
        }
        _output.WriteLine("AccBands Span mode consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Eventing()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Eventing mode
            var pubSource = new TBarSeries();
            var eventingInd = new AccBands(pubSource, period, 4.0);
            var eventMiddle = new TSeries();
            var eventUpper = new TSeries();
            var eventLower = new TSeries();

            foreach (var bar in _testData.Bars)
            {
                pubSource.Add(bar);
                eventMiddle.Add(eventingInd.Last);
                eventUpper.Add(eventingInd.Upper);
                eventLower.Add(eventingInd.Lower);
            }

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 4.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(batchMiddle, eventMiddle);
            ValidationHelper.VerifySeriesEqual(batchUpper, eventUpper);
            ValidationHelper.VerifySeriesEqual(batchLower, eventLower);
        }
        _output.WriteLine("AccBands Eventing mode consistency validated successfully");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var ((middle, upper, lower), indicator) = AccBands.Calculate(_testData.Bars, period, 4.0);

            // Verify indicator is hot
            Assert.True(indicator.IsHot);
            Assert.Equal(period, indicator.WarmupPeriod);

            // Verify results match indicator state
            Assert.Equal(middle.Last.Value, indicator.Last.Value, 1e-10);
            Assert.Equal(upper.Last.Value, indicator.Upper.Value, 1e-10);
            Assert.Equal(lower.Last.Value, indicator.Lower.Value, 1e-10);

            // Verify can continue streaming
            var nextBar = new TBar(DateTime.UtcNow.AddDays(1), 100, 110, 90, 105, 1000);
            indicator.Update(nextBar);
            Assert.True(indicator.IsHot);
        }
        _output.WriteLine("AccBands Calculate method validated successfully");
    }

    [Fact]
    public void Validate_LargeDataset_NoOverflow()
    {
        // Test with the full 5000 bar dataset
        var (middle, upper, lower) = AccBands.Batch(_testData.Bars, 100, 4.0);

        // All outputs should be finite
        ValidationHelper.VerifyAllFinite(middle, startIndex: 0);
        ValidationHelper.VerifyAllFinite(upper, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lower, startIndex: 0);

        // Upper should always be >= Middle, Middle should always be >= Lower (for normal data)
        for (int i = 100; i < middle.Count; i++)
        {
            Assert.True(upper[i].Value >= middle[i].Value,
                $"Upper ({upper[i].Value}) should be >= Middle ({middle[i].Value}) at index {i}");
            Assert.True(middle[i].Value >= lower[i].Value,
                $"Middle ({middle[i].Value}) should be >= Lower ({lower[i].Value}) at index {i}");
        }

        _output.WriteLine("AccBands large dataset (5000 bars) validated successfully");
    }

    [Fact]
    public void Validate_Prime_ProducesCorrectState()
    {
        // Prime with history and verify state matches full calculation
        const int period = 20;

        // Full batch calculation
        var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 4.0);

        // Prime indicator with subset and continue
        var primedIndicator = new AccBands(period, 4.0);
        var subset = new TBarSeries();
        for (int i = 0; i < 100; i++)
        {
            subset.Add(_testData.Bars[i]);
        }
        primedIndicator.Prime(subset);

        // Continue streaming from where Prime left off
        for (int i = 100; i < _testData.Bars.Count; i++)
        {
            primedIndicator.Update(_testData.Bars[i]);
        }

        // Final values should match
        Assert.Equal(batchMiddle.Last.Value, primedIndicator.Last.Value, 1e-9);
        Assert.Equal(batchUpper.Last.Value, primedIndicator.Upper.Value, 1e-9);
        Assert.Equal(batchLower.Last.Value, primedIndicator.Lower.Value, 1e-9);

        _output.WriteLine("AccBands Prime method validated successfully");
    }

    [Fact]
    public void Validate_Talib_AllBands_Batch()
    {
        // TA-Lib ACCBANDS uses the same Headley formula:
        // Upper = SMA(High*(1+4*(H-L)/(H+L)), period)
        // Lower = SMA(Low*(1-4*(H-L)/(H+L)), period)
        // Middle = SMA(Close, period)
        // Now all three bands should match exactly.
        int[] periods = { 5, 10, 20, 50, 100 };

        double[] high = _testData.HighPrices.ToArray();
        double[] low = _testData.LowPrices.ToArray();
        double[] close = _testData.ClosePrices.ToArray();
        int len = close.Length;

        double[] talibUpper = new double[len];
        double[] talibMiddle = new double[len];
        double[] talibLower = new double[len];

        foreach (var period in periods)
        {
            // QuanTAlib AccBands (batch) with factor=4 to match TA-Lib default
            var (qMiddle, qUpper, qLower) = AccBands.Batch(_testData.Bars, period, 4.0);

            // TALib Accbands
            var retCode = Functions.Accbands<double>(
                high, low, close,
                0..^0,
                talibUpper, talibMiddle, talibLower,
                out var outRange,
                period);

            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = Functions.AccbandsLookback(period);

            // All three bands should match (same Headley formula)
            ValidationHelper.VerifyData(qMiddle, talibMiddle, outRange, lookback);
            ValidationHelper.VerifyData(qUpper, talibUpper, outRange, lookback);
            ValidationHelper.VerifyData(qLower, talibLower, outRange, lookback);
        }
        _output.WriteLine("AccBands all bands validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_AllBands_Span()
    {
        // Validate all band match using Span API
        int[] periods = { 5, 10, 20, 50, 100 };

        double[] high = _testData.HighPrices.ToArray();
        double[] low = _testData.LowPrices.ToArray();
        double[] close = _testData.ClosePrices.ToArray();
        int len = close.Length;

        double[] talibUpper = new double[len];
        double[] talibMiddle = new double[len];
        double[] talibLower = new double[len];

        foreach (var period in periods)
        {
            // QuanTAlib AccBands (Span API) with factor=4
            double[] qMiddle = new double[len];
            double[] qUpper = new double[len];
            double[] qLower = new double[len];
            AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          qMiddle.AsSpan(), qUpper.AsSpan(), qLower.AsSpan(),
                          period, 4.0);

            // TALib Accbands
            var retCode = Functions.Accbands<double>(
                high, low, close,
                0..^0,
                talibUpper, talibMiddle, talibLower,
                out var outRange,
                period);

            Assert.Equal(Core.RetCode.Success, retCode);

            int lookback = Functions.AccbandsLookback(period);

            // All three bands should match
            ValidationHelper.VerifyData(qMiddle, talibMiddle, outRange, lookback);
            ValidationHelper.VerifyData(qUpper, talibUpper, outRange, lookback);
            ValidationHelper.VerifyData(qLower, talibLower, outRange, lookback);
        }
        _output.WriteLine("AccBands Span all bands validated successfully against TA-Lib");
    }

    [Fact]
    public void Validate_Talib_StructuralRelationships()
    {
        // Verify structural relationships hold for both implementations
        const int period = 20;

        double[] high = _testData.HighPrices.ToArray();
        double[] low = _testData.LowPrices.ToArray();
        double[] close = _testData.ClosePrices.ToArray();
        int len = close.Length;

        double[] talibUpper = new double[len];
        double[] talibMiddle = new double[len];
        double[] talibLower = new double[len];

        var retCode = Functions.Accbands<double>(
            high, low, close,
            0..^0,
            talibUpper, talibMiddle, talibLower,
            out var outRange,
            period);

        Assert.Equal(Core.RetCode.Success, retCode);

        var (qMiddle, qUpper, qLower) = AccBands.Batch(_testData.Bars, period, 4.0);

        int lookback = Functions.AccbandsLookback(period);
        int talibStart = outRange.Start.Value;

        // Both should have Upper > Middle > Lower
        int structuralCount = 0;
        for (int i = lookback; i < qMiddle.Count && (i - talibStart) < len; i++)
        {
            int tIdx = i - talibStart;
            if (tIdx >= 0 && tIdx < len && talibUpper[tIdx] != 0)
            {
                Assert.True(qUpper[i].Value > qMiddle[i].Value, $"Q: Upper > Middle at {i}");
                Assert.True(qLower[i].Value < qMiddle[i].Value, $"Q: Lower < Middle at {i}");
                Assert.True(talibUpper[tIdx] > talibMiddle[tIdx], $"TALib: Upper > Middle at {i}");
                Assert.True(talibLower[tIdx] < talibMiddle[tIdx], $"TALib: Lower < Middle at {i}");
                structuralCount++;
            }
        }

        Assert.True(structuralCount > 100, $"Validated {structuralCount} bars structurally");
        _output.WriteLine($"AccBands structural relationships validated ({structuralCount} bars)");
    }
}
