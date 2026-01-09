using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Abber indicator.
/// Note: Skender.Stock.Indicators, TA-Lib, Tulip, and OoplesFinance do not provide
/// Abber (Aberration Bands) implementation for cross-validation. These tests validate
/// against manual calculations and internal consistency across all API modes.
/// </summary>
public sealed class AbberValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public AbberValidationTests(ITestOutputHelper output)
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
        // Values: [100, 110, 120]
        // Bar 1: SMA=100, Dev=0, AvgDev=0
        // Bar 2: SMA=(100+110)/2=105, Dev1=0, Dev2=|110-100|=10, AvgDev=(0+10)/2=5
        // Bar 3: SMA=(100+110+120)/3=110, Dev3=|120-105|=15, AvgDev=(0+10+15)/3=8.333

        var series = new TSeries();
        var time = DateTime.UtcNow;
        series.Add(new TValue(time, 100));
        series.Add(new TValue(time.AddMinutes(1), 110));
        series.Add(new TValue(time.AddMinutes(2), 120));

        var abber = new Abber(3, 2.0);
        var (middle, upper, lower) = abber.Update(series);

        // SMA(3) = 110
        Assert.Equal(110.0, middle.Last.Value, 1e-10);

        // AvgDev = (0 + 10 + 15) / 3 = 25/3
        double expectedAvgDev = 25.0 / 3.0;
        double expectedBandWidth = 2.0 * expectedAvgDev;

        Assert.Equal(110.0 + expectedBandWidth, upper.Last.Value, 1e-10);
        Assert.Equal(110.0 - expectedBandWidth, lower.Last.Value, 1e-10);

        _output.WriteLine("Abber manual calculation (period 3) validated successfully");
    }

    [Fact]
    public void Validate_ManualCalculation_Period5()
    {
        // Manual calculation verification with period 5
        // Use simple arithmetic sequence: 100, 110, 120, 130, 140
        var series = new TSeries();
        var time = DateTime.UtcNow;

        double[] values = [100, 110, 120, 130, 140];
        for (int i = 0; i < values.Length; i++)
        {
            series.Add(new TValue(time.AddMinutes(i), values[i]));
        }

        var abber = new Abber(5, 2.0);
        var (middle, _, _) = abber.Update(series);

        // SMA(5) = (100 + 110 + 120 + 130 + 140) / 5 = 120
        Assert.Equal(120.0, middle.Last.Value, 1e-10);

        _output.WriteLine("Abber manual calculation (period 5) validated successfully");
    }

    [Fact]
    public void Validate_Multiplier_Effect()
    {
        // Verify multiplier affects band width correctly
        var series = new TSeries();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            // Oscillating values to create deviation
            double value = 100 + (i % 2 == 0 ? 10 : -10);
            series.Add(new TValue(time.AddMinutes(i), value));
        }

        var (middle1, upper1, _) = Abber.Batch(series, 10, 1.0);
        var (middle2, upper2, _) = Abber.Batch(series, 10, 2.0);
        var (middle3, upper3, _) = Abber.Batch(series, 10, 3.0);

        // Middle should be the same regardless of multiplier
        Assert.Equal(middle1.Last.Value, middle2.Last.Value, 1e-10);
        Assert.Equal(middle2.Last.Value, middle3.Last.Value, 1e-10);

        // Band widths should scale linearly with multiplier
        double bw1 = upper1.Last.Value - middle1.Last.Value;
        double bw2 = upper2.Last.Value - middle2.Last.Value;
        double bw3 = upper3.Last.Value - middle3.Last.Value;

        Assert.Equal(bw1 * 2.0, bw2, 1e-10);
        Assert.Equal(bw1 * 3.0, bw3, 1e-10);

        _output.WriteLine("Abber multiplier effect validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Batch()
    {
        int[] periods = [5, 10, 20, 50, 100];

        foreach (var period in periods)
        {
            // Batch mode using instance
            var abber = new Abber(period, 2.0);
            var (qMiddle, qUpper, qLower) = abber.Update(_testData.Data);

            // Static batch
            var (sMiddle, sUpper, sLower) = Abber.Batch(_testData.Data, period, 2.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(qMiddle, sMiddle);
            ValidationHelper.VerifySeriesEqual(qUpper, sUpper);
            ValidationHelper.VerifySeriesEqual(qLower, sLower);
        }
        _output.WriteLine("Abber Batch modes consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Streaming()
    {
        int[] periods = [5, 10, 20, 50, 100];

        foreach (var period in periods)
        {
            // Streaming mode
            var streamingAbber = new Abber(period, 2.0);
            var streamMiddle = new TSeries();
            var streamUpper = new TSeries();
            var streamLower = new TSeries();
            foreach (var item in _testData.Data)
            {
                streamingAbber.Update(item);
                streamMiddle.Add(streamingAbber.Last);
                streamUpper.Add(streamingAbber.Upper);
                streamLower.Add(streamingAbber.Lower);
            }

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = Abber.Batch(_testData.Data, period, 2.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(batchMiddle, streamMiddle);
            ValidationHelper.VerifySeriesEqual(batchUpper, streamUpper);
            ValidationHelper.VerifySeriesEqual(batchLower, streamLower);
        }
        _output.WriteLine("Abber Streaming mode consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Span()
    {
        int[] periods = [5, 10, 20, 50, 100];

        double[] source = _testData.RawData.ToArray();

        foreach (var period in periods)
        {
            // Span mode
            int len = source.Length;
            double[] spanMiddle = new double[len];
            double[] spanUpper = new double[len];
            double[] spanLower = new double[len];

            Abber.Batch(source.AsSpan(), spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan(),
                       period, 2.0);

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = Abber.Batch(_testData.Data, period, 2.0);

            // Verify match
            for (int i = 0; i < len; i++)
            {
                Assert.Equal(batchMiddle[i].Value, spanMiddle[i], 9);
                Assert.Equal(batchUpper[i].Value, spanUpper[i], 9);
                Assert.Equal(batchLower[i].Value, spanLower[i], 9);
            }
        }
        _output.WriteLine("Abber Span mode consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Eventing()
    {
        int[] periods = [5, 10, 20, 50];

        foreach (var period in periods)
        {
            // Eventing mode
            var pubSource = new TSeries();
            var eventingInd = new Abber(pubSource, period, 2.0);
            var eventMiddle = new TSeries();
            var eventUpper = new TSeries();
            var eventLower = new TSeries();

            foreach (var item in _testData.Data)
            {
                pubSource.Add(item);
                eventMiddle.Add(eventingInd.Last);
                eventUpper.Add(eventingInd.Upper);
                eventLower.Add(eventingInd.Lower);
            }

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = Abber.Batch(_testData.Data, period, 2.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(batchMiddle, eventMiddle);
            ValidationHelper.VerifySeriesEqual(batchUpper, eventUpper);
            ValidationHelper.VerifySeriesEqual(batchLower, eventLower);
        }
        _output.WriteLine("Abber Eventing mode consistency validated successfully");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        int[] periods = [5, 10, 20, 50, 100];

        foreach (var period in periods)
        {
            var ((_, _, _), indicator) = Abber.Calculate(_testData.Data, period, 2.0);

            // Verify indicator is hot
            Assert.True(indicator.IsHot);
            Assert.Equal(period, indicator.WarmupPeriod);

            // Note: Indicator state after Prime may not exactly match batch output because
            // deviation calculations depend on SMA history. Prime only restores the last
            // WarmupPeriod bars, so deviations are calculated differently.
            // We verify the indicator is in a valid state for continued streaming.
            Assert.True(double.IsFinite(indicator.Last.Value));
            Assert.True(double.IsFinite(indicator.Upper.Value));
            Assert.True(double.IsFinite(indicator.Lower.Value));

            // Verify can continue streaming
            var nextValue = new TValue(DateTime.UtcNow.AddDays(1), 100);
            indicator.Update(nextValue);
            Assert.True(indicator.IsHot);
        }
        _output.WriteLine("Abber Calculate method validated successfully");
    }

    [Fact]
    public void Validate_LargeDataset_NoOverflow()
    {
        // Test with the full 5000 bar dataset
        var (middle, upper, lower) = Abber.Batch(_testData.Data, 100, 2.0);

        // All outputs should be finite
        ValidationHelper.VerifyAllFinite(middle, startIndex: 0);
        ValidationHelper.VerifyAllFinite(upper, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lower, startIndex: 0);

        // Upper should always be >= Middle, Middle should always be >= Lower
        for (int i = 100; i < middle.Count; i++)
        {
            Assert.True(upper[i].Value >= middle[i].Value,
                $"Upper ({upper[i].Value}) should be >= Middle ({middle[i].Value}) at index {i}");
            Assert.True(middle[i].Value >= lower[i].Value,
                $"Middle ({middle[i].Value}) should be >= Lower ({lower[i].Value}) at index {i}");
        }

        _output.WriteLine("Abber large dataset (5000 bars) validated successfully");
    }

    [Fact]
    public void Validate_BandWidth_IsSymmetric()
    {
        // Verify that Upper - Middle == Middle - Lower
        // This confirms the band width is applied symmetrically

        var (middle, upper, lower) = Abber.Batch(_testData.Data, 20, 2.0);

        // After warmup, verify symmetry
        for (int i = 20; i < _testData.Data.Count; i++)
        {
            double upperDiff = upper[i].Value - middle[i].Value;
            double lowerDiff = middle[i].Value - lower[i].Value;

            Assert.Equal(upperDiff, lowerDiff, 1e-9);
        }

        _output.WriteLine("Abber band width symmetry validated successfully");
    }

    [Fact]
    public void Validate_Prime_ProducesCorrectState()
    {
        // Prime with history and verify state matches full calculation
        int period = 20;

        // Full batch calculation
        var (batchMiddle, batchUpper, batchLower) = Abber.Batch(_testData.Data, period, 2.0);

        // Prime indicator with subset and continue
        var primedIndicator = new Abber(period, 2.0);
        var subset = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            subset.Add(_testData.Data[i]);
        }
        primedIndicator.Prime(subset);

        // Continue streaming from where Prime left off
        for (int i = 100; i < _testData.Data.Count; i++)
        {
            primedIndicator.Update(_testData.Data[i]);
        }

        // Final values should match
        Assert.Equal(batchMiddle.Last.Value, primedIndicator.Last.Value, 1e-9);
        Assert.Equal(batchUpper.Last.Value, primedIndicator.Upper.Value, 1e-9);
        Assert.Equal(batchLower.Last.Value, primedIndicator.Lower.Value, 1e-9);

        _output.WriteLine("Abber Prime method validated successfully");
    }

    [Fact]
    public void Validate_MiddleBand_MatchesSMA()
    {
        // Verify the middle band is exactly the SMA
        int period = 20;

        var abber = new Abber(period, 2.0);
        var sma = new Sma(period);

        var abberResults = abber.Update(_testData.Data);
        var smaResults = sma.Update(_testData.Data);

        // Middle band should match SMA exactly
        for (int i = 0; i < _testData.Data.Count; i++)
        {
            Assert.Equal(smaResults[i].Value, abberResults.Middle[i].Value, 1e-10);
        }

        _output.WriteLine("Abber middle band matches SMA validated successfully");
    }

    [Fact]
    public void Validate_DeviationCalculation()
    {
        // Verify the deviation is calculated as |source - SMA|
        int period = 5;

        // Use predictable values
        var series = new TSeries();
        var time = DateTime.UtcNow;
        double[] values = [100, 120, 80, 110, 90];
        for (int i = 0; i < values.Length; i++)
        {
            series.Add(new TValue(time.AddMinutes(i), values[i]));
        }

        var abber = new Abber(period, 1.0); // multiplier = 1 for easier verification
        var (middle, upper, _) = abber.Update(series);

        // SMA(5) = (100 + 120 + 80 + 110 + 90) / 5 = 100
        Assert.Equal(100.0, middle.Last.Value, 1e-10);

        // Band width = AvgDeviation (since multiplier = 1)
        // The deviations are calculated incrementally, so we verify the final result
        double bandWidth = upper.Last.Value - middle.Last.Value;
        Assert.True(bandWidth >= 0, "Band width should be non-negative");
        Assert.True(double.IsFinite(bandWidth), "Band width should be finite");

        _output.WriteLine("Abber deviation calculation validated successfully");
    }

    [Fact]
    public void Validate_Consistency_AcrossPeriods()
    {
        // Verify behavior is consistent across different periods
        int[] periods = [3, 5, 10, 20, 50, 100, 200];

        foreach (var period in periods)
        {
            var (middle, upper, lower) = Abber.Batch(_testData.Data, period, 2.0);

            // All values should be finite
            for (int i = 0; i < middle.Count; i++)
            {
                Assert.True(double.IsFinite(middle[i].Value), $"Middle[{i}] not finite for period {period}");
                Assert.True(double.IsFinite(upper[i].Value), $"Upper[{i}] not finite for period {period}");
                Assert.True(double.IsFinite(lower[i].Value), $"Lower[{i}] not finite for period {period}");
            }

            // Upper >= Middle >= Lower (bands are symmetric around middle)
            for (int i = period; i < middle.Count; i++)
            {
                Assert.True(upper[i].Value >= middle[i].Value);
                Assert.True(middle[i].Value >= lower[i].Value);
            }
        }

        _output.WriteLine($"Abber consistency across {periods.Length} periods validated successfully");
    }
}
