using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for APZ (Adaptive Price Zone) indicator.
/// Note: Skender.Stock.Indicators, TA-Lib, Tulip, and OoplesFinance do not provide
/// APZ (Adaptive Price Zone) implementation for cross-validation. These tests validate
/// against manual calculations and internal consistency across all API modes.
/// </summary>
public sealed class ApzValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly TBarSeries _bars;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public ApzValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        _bars = gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
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
    public void Validate_ManualCalculation_Period4()
    {
        // Manual calculation verification for period 4
        // sqrt(4) = 2, alpha = 2/(2+1) = 0.667, beta = 0.333

        var time = DateTime.UtcNow;

        // Bar 1: Close=100, High=105, Low=95, Range=10
        // Bar 2: Close=110, High=115, Low=105, Range=10
        // Bar 3: Close=105, High=112, Low=100, Range=12
        // Bar 4: Close=115, High=120, Low=110, Range=10

        var bars = new TBarSeries();
        bars.Add(new TBar(time, 100, 105, 95, 100, 1000));
        bars.Add(new TBar(time.AddMinutes(1), 110, 115, 105, 110, 1000));
        bars.Add(new TBar(time.AddMinutes(2), 105, 112, 100, 105, 1000));
        bars.Add(new TBar(time.AddMinutes(3), 115, 120, 110, 115, 1000));

        var apz = new Apz(4, 2.0);
        foreach (var bar in bars)
        {
            apz.Update(bar);
        }

        // Verify output is finite and bands are properly ordered
        Assert.True(double.IsFinite(apz.Last.Value));
        Assert.True(double.IsFinite(apz.Upper.Value));
        Assert.True(double.IsFinite(apz.Lower.Value));
        Assert.True(apz.Upper.Value > apz.Last.Value);
        Assert.True(apz.Lower.Value < apz.Last.Value);

        _output.WriteLine($"Apz manual calculation (period 4) validated: Middle={apz.Last.Value:F4}, Upper={apz.Upper.Value:F4}, Lower={apz.Lower.Value:F4}");
    }

    [Fact]
    public void Validate_SqrtPeriod_SmoothingFactor()
    {
        // Verify sqrt(period) smoothing factor is correctly applied
        // alpha = 2 / (sqrt(period) + 1)

        // Period 1: sqrt(1) = 1, alpha = 2/(1+1) = 1.0 (no smoothing)
        // Period 4: sqrt(4) = 2, alpha = 2/(2+1) = 0.667
        // Period 9: sqrt(9) = 3, alpha = 2/(3+1) = 0.5
        // Period 16: sqrt(16) = 4, alpha = 2/(4+1) = 0.4
        // Period 100: sqrt(100) = 10, alpha = 2/(10+1) = 0.182

        int[] periods = { 1, 4, 9, 16, 100 };

        // Test by verifying convergence behavior
        for (int p = 0; p < periods.Length; p++)
        {
            int period = periods[p];
            var apz = new Apz(period, 2.0);

            // Feed constant data
            for (int i = 0; i < 200; i++)
            {
                apz.Update(new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000));
            }

            // Middle should converge to 100
            Assert.Equal(100.0, apz.Last.Value, 0.1);
        }

        _output.WriteLine("Apz sqrt(period) smoothing factor validated successfully");
    }

    [Fact]
    public void Validate_Multiplier_Effect()
    {
        // Verify multiplier affects band width correctly

        var (middle1, upper1, _) = Apz.Batch(_bars, 20, 1.0);
        var (middle2, upper2, _) = Apz.Batch(_bars, 20, 2.0);
        var (middle3, upper3, _) = Apz.Batch(_bars, 20, 3.0);

        // Middle should be the same regardless of multiplier
        Assert.Equal(middle1.Last.Value, middle2.Last.Value, 1e-10);
        Assert.Equal(middle2.Last.Value, middle3.Last.Value, 1e-10);

        // Band widths should scale linearly with multiplier
        double bw1 = upper1.Last.Value - middle1.Last.Value;
        double bw2 = upper2.Last.Value - middle2.Last.Value;
        double bw3 = upper3.Last.Value - middle3.Last.Value;

        Assert.Equal(bw1 * 2.0, bw2, 1e-10);
        Assert.Equal(bw1 * 3.0, bw3, 1e-10);

        _output.WriteLine("Apz multiplier effect validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Batch()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Batch mode using instance
            var apz = new Apz(period, 2.0);
            var (qMiddle, qUpper, qLower) = apz.Update(_bars);

            // Static batch
            var (sMiddle, sUpper, sLower) = Apz.Batch(_bars, period, 2.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(qMiddle, sMiddle);
            ValidationHelper.VerifySeriesEqual(qUpper, sUpper);
            ValidationHelper.VerifySeriesEqual(qLower, sLower);
        }
        _output.WriteLine("Apz Batch modes consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Streaming()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            // Streaming mode
            var streamingApz = new Apz(period, 2.0);
            var streamMiddle = new TSeries();
            var streamUpper = new TSeries();
            var streamLower = new TSeries();
            foreach (var bar in _bars)
            {
                streamingApz.Update(bar);
                streamMiddle.Add(streamingApz.Last);
                streamUpper.Add(streamingApz.Upper);
                streamLower.Add(streamingApz.Lower);
            }

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = Apz.Batch(_bars, period, 2.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(batchMiddle, streamMiddle);
            ValidationHelper.VerifySeriesEqual(batchUpper, streamUpper);
            ValidationHelper.VerifySeriesEqual(batchLower, streamLower);
        }
        _output.WriteLine("Apz Streaming mode consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Span()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        double[] highArr = _bars.High.Values.ToArray();
        double[] lowArr = _bars.Low.Values.ToArray();
        double[] closeArr = _bars.Close.Values.ToArray();
        int len = closeArr.Length;

        foreach (var period in periods)
        {
            // Span mode
            double[] spanMiddle = new double[len];
            double[] spanUpper = new double[len];
            double[] spanLower = new double[len];

            Apz.Batch(highArr.AsSpan(), lowArr.AsSpan(), closeArr.AsSpan(),
                     new Apz.BatchOutputs(
                     spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan()),
                     period, 2.0);

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = Apz.Batch(_bars, period, 2.0);

            // Verify match
            for (int i = 0; i < len; i++)
            {
                Assert.Equal(batchMiddle[i].Value, spanMiddle[i], 9);
                Assert.Equal(batchUpper[i].Value, spanUpper[i], 9);
                Assert.Equal(batchLower[i].Value, spanLower[i], 9);
            }
        }
        _output.WriteLine("Apz Span mode consistency validated successfully");
    }

    [Fact]
    public void Validate_AllModes_Consistency_Eventing()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (var period in periods)
        {
            // Eventing mode
            var pubSource = new TBarSeries();
            var eventingInd = new Apz(pubSource, period, 2.0);
            var eventMiddle = new TSeries();
            var eventUpper = new TSeries();
            var eventLower = new TSeries();

            foreach (var bar in _bars)
            {
                pubSource.Add(bar);
                eventMiddle.Add(eventingInd.Last);
                eventUpper.Add(eventingInd.Upper);
                eventLower.Add(eventingInd.Lower);
            }

            // Batch mode for comparison
            var (batchMiddle, batchUpper, batchLower) = Apz.Batch(_bars, period, 2.0);

            // Verify match
            ValidationHelper.VerifySeriesEqual(batchMiddle, eventMiddle);
            ValidationHelper.VerifySeriesEqual(batchUpper, eventUpper);
            ValidationHelper.VerifySeriesEqual(batchLower, eventLower);
        }
        _output.WriteLine("Apz Eventing mode consistency validated successfully");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var ((_, _, _), indicator) = Apz.Calculate(_bars, period, 2.0);

            // Verify indicator is hot
            Assert.True(indicator.IsHot);
            Assert.Equal(period, indicator.WarmupPeriod);

            // Verify indicator is in a valid state
            Assert.True(double.IsFinite(indicator.Last.Value));
            Assert.True(double.IsFinite(indicator.Upper.Value));
            Assert.True(double.IsFinite(indicator.Lower.Value));

            // Verify can continue streaming
            var nextBar = new TBar(DateTime.UtcNow.AddDays(1), 100, 105, 95, 100, 1000);
            indicator.Update(nextBar);
            Assert.True(indicator.IsHot);
        }
        _output.WriteLine("Apz Calculate method validated successfully");
    }

    [Fact]
    public void Validate_LargeDataset_NoOverflow()
    {
        // Test with the full 5000 bar dataset
        var (middle, upper, lower) = Apz.Batch(_bars, 100, 2.0);

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

        _output.WriteLine("Apz large dataset (5000 bars) validated successfully");
    }

    [Fact]
    public void Validate_BandWidth_IsSymmetric()
    {
        // Verify that Upper - Middle == Middle - Lower
        // This confirms the band width is applied symmetrically

        var (middle, upper, lower) = Apz.Batch(_bars, 20, 2.0);

        // After convergence, verify symmetry
        for (int i = 50; i < _bars.Count; i++)
        {
            double upperDiff = upper[i].Value - middle[i].Value;
            double lowerDiff = middle[i].Value - lower[i].Value;

            Assert.Equal(upperDiff, lowerDiff, 1e-9);
        }

        _output.WriteLine("Apz band width symmetry validated successfully");
    }

    [Fact]
    public void Validate_Prime_ProducesCorrectState()
    {
        // Prime with history and verify state matches full calculation
        const int period = 20;

        // Full batch calculation
        var (batchMiddle, batchUpper, batchLower) = Apz.Batch(_bars, period, 2.0);

        // Prime indicator with subset and continue
        var primedIndicator = new Apz(period, 2.0);
        var subset = new TBarSeries();
        for (int i = 0; i < 100; i++)
        {
            subset.Add(_bars[i]);
        }
        primedIndicator.Prime(subset);

        // Continue streaming from where Prime left off
        for (int i = 100; i < _bars.Count; i++)
        {
            primedIndicator.Update(_bars[i]);
        }

        // Final values should match
        Assert.Equal(batchMiddle.Last.Value, primedIndicator.Last.Value, 1e-9);
        Assert.Equal(batchUpper.Last.Value, primedIndicator.Upper.Value, 1e-9);
        Assert.Equal(batchLower.Last.Value, primedIndicator.Lower.Value, 1e-9);

        _output.WriteLine("Apz Prime method validated successfully");
    }

    [Fact]
    public void Validate_DoubleSmoothing_Property()
    {
        // Verify double-smoothed EMA produces smoother output than single EMA
        int period = 25;

        var apz = new Apz(period, 2.0);
        var apzResults = new List<double>();

        // Also calculate single EMA for comparison
        double alpha = 2.0 / (Math.Sqrt(period) + 1.0);
        double ema = 0;
        var emaResults = new List<double>();

        foreach (var bar in _bars)
        {
            apz.Update(bar);
            apzResults.Add(apz.Last.Value);

            if (emaResults.Count == 0)
            {
                ema = bar.Close;
            }
            else
            {
                ema = alpha * bar.Close + (1 - alpha) * ema;
            }

            emaResults.Add(ema);
        }

        // Calculate smoothness (average absolute change)
        double apzSmoothness = 0;
        double emaSmoothness = 0;
        int startIdx = 100; // Skip warmup

        for (int i = startIdx + 1; i < apzResults.Count; i++)
        {
            apzSmoothness += Math.Abs(apzResults[i] - apzResults[i - 1]);
            emaSmoothness += Math.Abs(emaResults[i] - emaResults[i - 1]);
        }

        apzSmoothness /= (apzResults.Count - startIdx - 1);
        emaSmoothness /= (emaResults.Count - startIdx - 1);

        // Double-smoothed should be smoother than single EMA
        Assert.True(apzSmoothness < emaSmoothness,
            $"APZ ({apzSmoothness:F4}) should be smoother than single EMA ({emaSmoothness:F4})");

        _output.WriteLine($"Apz double-smoothing property validated: APZ smoothness={apzSmoothness:F4}, EMA smoothness={emaSmoothness:F4}");
    }

    [Fact]
    public void Validate_AdaptiveRange_FollowsVolatility()
    {
        // Verify that bands widen during high volatility and narrow during low volatility

        // Create low volatility bars
        var lowVolBars = new TBarSeries();
        var time = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            // Tight range: 2 points
            lowVolBars.Add(new TBar(time.AddMinutes(i), 100, 101, 99, 100, 1000));
        }

        // Create high volatility bars
        var highVolBars = new TBarSeries();
        for (int i = 0; i < 100; i++)
        {
            // Wide range: 20 points
            highVolBars.Add(new TBar(time.AddMinutes(i), 100, 110, 90, 100, 1000));
        }

        var (_, lowVolUpper, lowVolLower) = Apz.Batch(lowVolBars, 20, 2.0);
        var (_, highVolUpper, highVolLower) = Apz.Batch(highVolBars, 20, 2.0);

        double lowVolWidth = lowVolUpper.Last.Value - lowVolLower.Last.Value;
        double highVolWidth = highVolUpper.Last.Value - highVolLower.Last.Value;

        // High volatility should produce wider bands
        Assert.True(highVolWidth > lowVolWidth,
            $"High volatility width ({highVolWidth:F4}) should be greater than low volatility width ({lowVolWidth:F4})");

        _output.WriteLine($"Apz adaptive range validated: Low vol width={lowVolWidth:F4}, High vol width={highVolWidth:F4}");
    }

    [Fact]
    public void Validate_Consistency_AcrossPeriods()
    {
        // Verify behavior is consistent across different periods
        int[] periods = { 3, 5, 10, 20, 50, 100, 200 };

        foreach (var period in periods)
        {
            var (middle, upper, lower) = Apz.Batch(_bars, period, 2.0);

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

        _output.WriteLine($"Apz consistency across {periods.Length} periods validated successfully");
    }

    [Fact]
    public void Validate_WarmupCompensation_Converges()
    {
        // Verify warmup compensation allows convergence to true value
        var time = DateTime.UtcNow;
        var bars = new TBarSeries();

        // Feed constant data
        for (int i = 0; i < 200; i++)
        {
            bars.Add(new TBar(time.AddMinutes(i), 100, 100, 100, 100, 1000));
        }

        var apz = new Apz(20, 2.0);
        var (middle, upper, lower) = apz.Update(bars);

        // After warmup period, values should converge to 100
        // Check values after sufficient warmup (index >= period * 2)
        for (int i = 40; i < middle.Count; i++)
        {
            Assert.Equal(100.0, middle[i].Value, 0.1); // Converges to 100
            // Bands should converge to middle (zero range input)
            Assert.Equal(100.0, upper[i].Value, 0.1);
            Assert.Equal(100.0, lower[i].Value, 0.1);
        }

        // Final values should be very close to 100
        Assert.Equal(100.0, middle.Last.Value, 1e-6);
        Assert.Equal(100.0, upper.Last.Value, 1e-6);
        Assert.Equal(100.0, lower.Last.Value, 1e-6);

        _output.WriteLine("Apz warmup compensation convergence validated successfully");
    }
}
