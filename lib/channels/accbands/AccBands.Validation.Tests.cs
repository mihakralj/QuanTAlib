using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for AccBands indicator.
/// Note: Skender.Stock.Indicators, TA-Lib, Tulip, and OoplesFinance do not provide
/// AccBands implementation for cross-validation. These tests validate against
/// manual calculations and internal consistency across all API modes.
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
        // Manual calculation verification
        // Given: High = [12, 14, 16], Low = [8, 10, 12], Close = [10, 12, 14]
        // SMA(High, 3) = (12 + 14 + 16) / 3 = 14
        // SMA(Low, 3) = (8 + 10 + 12) / 3 = 10
        // SMA(Close, 3) = (10 + 12 + 14) / 3 = 12
        // BandWidth = (14 - 10) * 2.0 = 8
        // Upper = 14 + 8 = 22
        // Lower = 10 - 8 = 2
        // Middle = 12

        var series = new TBarSeries();
        var time = DateTime.UtcNow;
        series.Add(new TBar(time, 10, 12, 8, 10, 100));
        series.Add(new TBar(time.AddMinutes(1), 12, 14, 10, 12, 100));
        series.Add(new TBar(time.AddMinutes(2), 14, 16, 12, 14, 100));

        var accBands = new AccBands(3, 2.0);
        var (middle, upper, lower) = accBands.Update(series);

        Assert.Equal(12.0, middle.Last.Value, 1e-10);
        Assert.Equal(22.0, upper.Last.Value, 1e-10);
        Assert.Equal(2.0, lower.Last.Value, 1e-10);

        _output.WriteLine("AccBands manual calculation (period 3) validated successfully");
    }

    [Fact]
    public void Validate_ManualCalculation_Period5()
    {
        // Manual calculation verification with period 5
        var series = new TBarSeries();
        var time = DateTime.UtcNow;

        // Create predictable data: High = Close + 5, Low = Close - 5
        double[] closes = { 100, 102, 104, 106, 108 };
        for (int i = 0; i < closes.Length; i++)
        {
            double c = closes[i];
            series.Add(new TBar(time.AddMinutes(i), c, c + 5, c - 5, c, 1000));
        }

        // SMA(High, 5) = (105 + 107 + 109 + 111 + 113) / 5 = 109
        // SMA(Low, 5) = (95 + 97 + 99 + 101 + 103) / 5 = 99
        // SMA(Close, 5) = (100 + 102 + 104 + 106 + 108) / 5 = 104
        // BandWidth = (109 - 99) * 2.0 = 20
        // Upper = 109 + 20 = 129
        // Lower = 99 - 20 = 79

        var accBands = new AccBands(5, 2.0);
        var (middle, upper, lower) = accBands.Update(series);

        Assert.Equal(104.0, middle.Last.Value, 1e-10);
        Assert.Equal(129.0, upper.Last.Value, 1e-10);
        Assert.Equal(79.0, lower.Last.Value, 1e-10);

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

        // With constant H/L/C: SMA(High)=110, SMA(Low)=90, SMA(Close)=100
        // Spread = 110 - 90 = 20

        var (middle1, upper1, lower1) = AccBands.Batch(series, 5, 1.0);
        var (middle2, upper2, lower2) = AccBands.Batch(series, 5, 2.0);
        var (middle3, upper3, lower3) = AccBands.Batch(series, 5, 3.0);

        // Middle should be the same regardless of factor
        Assert.Equal(middle1.Last.Value, middle2.Last.Value, 1e-10);
        Assert.Equal(middle2.Last.Value, middle3.Last.Value, 1e-10);
        Assert.Equal(100.0, middle1.Last.Value, 1e-10);

        // BandWidth with factor 1.0 = 20
        // BandWidth with factor 2.0 = 40
        // BandWidth with factor 3.0 = 60

        // Upper = SMA(High) + BandWidth
        Assert.Equal(110.0 + 20.0, upper1.Last.Value, 1e-10);  // 130
        Assert.Equal(110.0 + 40.0, upper2.Last.Value, 1e-10);  // 150
        Assert.Equal(110.0 + 60.0, upper3.Last.Value, 1e-10);  // 170

        // Lower = SMA(Low) - BandWidth
        Assert.Equal(90.0 - 20.0, lower1.Last.Value, 1e-10);   // 70
        Assert.Equal(90.0 - 40.0, lower2.Last.Value, 1e-10);   // 50
        Assert.Equal(90.0 - 60.0, lower3.Last.Value, 1e-10);   // 30

        _output.WriteLine("AccBands factor effect validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Batch mode using instance
            var accBands = new AccBands(period, 2.0);
            var (qMiddle, qUpper, qLower) = accBands.Update(_testData.Bars);

            // Static batch
            var (sMiddle, sUpper, sLower) = AccBands.Batch(_testData.Bars, period, 2.0);

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
            var streamingAcc = new AccBands(period, 2.0);
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
            var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 2.0);

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
                          period, 2.0);

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 2.0);

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
            var eventingInd = new AccBands(pubSource, period, 2.0);
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
            var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 2.0);

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
            var ((middle, upper, lower), indicator) = AccBands.Calculate(_testData.Bars, period, 2.0);

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
        var (middle, upper, lower) = AccBands.Batch(_testData.Bars, 100, 2.0);

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
    public void Validate_BandWidth_IsSymmetric()
    {
        // Verify that Upper - SMA(High) == SMA(Low) - Lower
        // This confirms the band width is applied symmetrically

        var (middle, upper, lower) = AccBands.Batch(_testData.Bars, 20, 2.0);

        // Calculate SMA(High) and SMA(Low) separately for verification
        _ = middle; // Suppress unused variable warning - middle is not needed for symmetry test
        var smaHigh = new Sma(20);
        var smaLow = new Sma(20);

        var smaHighResults = new TSeries();
        var smaLowResults = new TSeries();

        for (int i = 0; i < _testData.Bars.Count; i++)
        {
            var bar = _testData.Bars[i];
            smaHighResults.Add(smaHigh.Update(new TValue(bar.Time, bar.High)));
            smaLowResults.Add(smaLow.Update(new TValue(bar.Time, bar.Low)));
        }

        // After warmup, verify symmetry
        for (int i = 20; i < _testData.Bars.Count; i++)
        {
            double upperDiff = upper[i].Value - smaHighResults[i].Value;
            double lowerDiff = smaLowResults[i].Value - lower[i].Value;

            Assert.Equal(upperDiff, lowerDiff, 1e-9);
        }

        _output.WriteLine("AccBands band width symmetry validated successfully");
    }

    [Fact]
    public void Validate_Prime_ProducesCorrectState()
    {
        // Prime with history and verify state matches full calculation
        const int period = 20;

        // Full batch calculation
        var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(_testData.Bars, period, 2.0);

        // Prime indicator with subset and continue
        var primedIndicator = new AccBands(period, 2.0);
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
}
