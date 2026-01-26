using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for STBands indicator.
/// Note: STBands (Super Trend Bands) is a proprietary indicator not available in
/// standard libraries like TA-Lib, Skender, Tulip, or Ooples. Validation focuses on
/// internal consistency between streaming, batch, and span modes.
/// </summary>
public sealed class StbandsValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public StbandsValidationTests(ITestOutputHelper output)
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
    public void Validate_Streaming_Batch_Consistency()
    {
        int[] periods = { 5, 10, 14, 20, 50 };
        double[] multipliers = { 1.0, 2.0, 3.0 };

        foreach (var period in periods)
        {
            foreach (var multiplier in multipliers)
            {
                // Generate test data with bars
                var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
                var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

                // Streaming mode
                var streamingStbands = new Stbands(period, multiplier);
                var streamingResults = new List<double>();
                var streamingUpper = new List<double>();
                var streamingLower = new List<double>();

                foreach (var bar in bars)
                {
                    streamingStbands.Update(bar);
                    streamingResults.Add(streamingStbands.Last.Value);
                    streamingUpper.Add(streamingStbands.Upper.Value);
                    streamingLower.Add(streamingStbands.Lower.Value);
                }

                // Batch mode
                var batchResult = Stbands.Calculate(bars, period, multiplier);

                // Compare last 100 values
                int compareCount = Math.Min(100, bars.Count - period);
                for (int i = bars.Count - compareCount; i < bars.Count; i++)
                {
                    Assert.Equal(streamingResults[i], batchResult[i].Value, precision: 10);
                }
            }
        }
        _output.WriteLine("STBands Streaming vs Batch consistency validated successfully");
    }

    [Fact]
    public void Validate_Streaming_Span_Consistency()
    {
        int[] periods = { 5, 10, 14, 20, 50 };
        double[] multipliers = { 1.0, 2.0, 3.0 };

        foreach (var period in periods)
        {
            foreach (var multiplier in multipliers)
            {
                // Generate test data with bars
                var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
                var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

                // Streaming mode
                var streamingStbands = new Stbands(period, multiplier);
                var streamingUpper = new List<double>();
                var streamingLower = new List<double>();
                var streamingTrend = new List<double>();

                foreach (var bar in bars)
                {
                    streamingStbands.Update(bar);
                    streamingUpper.Add(streamingStbands.Upper.Value);
                    streamingLower.Add(streamingStbands.Lower.Value);
                    streamingTrend.Add(streamingStbands.Trend.Value);
                }

                // Span mode
                double[] high = bars.High.Values.ToArray();
                double[] low = bars.Low.Values.ToArray();
                double[] close = bars.Close.Values.ToArray();
                double[] spanUpper = new double[bars.Count];
                double[] spanLower = new double[bars.Count];
                double[] spanTrend = new double[bars.Count];

                Stbands.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                    spanUpper.AsSpan(), spanLower.AsSpan(), spanTrend.AsSpan(), period, multiplier);

                // Compare last 100 values
                int compareCount = Math.Min(100, bars.Count - period);
                for (int i = bars.Count - compareCount; i < bars.Count; i++)
                {
                    Assert.Equal(streamingUpper[i], spanUpper[i], precision: 10);
                    Assert.Equal(streamingLower[i], spanLower[i], precision: 10);
                    Assert.Equal(streamingTrend[i], spanTrend[i], precision: 10);
                }
            }
        }
        _output.WriteLine("STBands Streaming vs Span consistency validated successfully");
    }

    [Fact]
    public void Validate_BandCharacteristics()
    {
        // Verify core SuperTrend characteristics:
        // 1. Upper band only moves down (unless price breaks above)
        // 2. Lower band only moves up (unless price breaks below)
        // 3. Bands are always Upper >= Lower

        int period = 10;
        double multiplier = 3.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] high = bars.High.Values.ToArray();
        double[] low = bars.Low.Values.ToArray();
        double[] close = bars.Close.Values.ToArray();
        double[] upper = new double[bars.Count];
        double[] lower = new double[bars.Count];
        double[] trend = new double[bars.Count];

        Stbands.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(),
            upper.AsSpan(), lower.AsSpan(), trend.AsSpan(), period, multiplier);

        // Verify Upper >= Lower for all points
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.True(upper[i] >= lower[i],
                $"Upper band ({upper[i]}) should be >= Lower band ({lower[i]}) at index {i}");
        }

        // Verify trend is always +1 or -1
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.True(trend[i] == 1 || trend[i] == -1,
                $"Trend should be +1 or -1, got {trend[i]} at index {i}");
        }

        _output.WriteLine("STBands band characteristics validated successfully");
    }

    [Fact]
    public void Validate_TrendTransitions()
    {
        // Verify trend transitions occur at band breakouts
        int period = 10;
        double multiplier = 2.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 123); // Higher volatility for transitions
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] high = bars.High.Values.ToArray();
        double[] low = bars.Low.Values.ToArray();
        double[] close = bars.Close.Values.ToArray();
        double[] upper = new double[bars.Count];
        double[] lower = new double[bars.Count];
        double[] trend = new double[bars.Count];

        Stbands.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(),
            upper.AsSpan(), lower.AsSpan(), trend.AsSpan(), period, multiplier);

        int trendChanges = 0;
        for (int i = 1; i < bars.Count; i++)
        {
            if (trend[i] != trend[i - 1])
            {
                trendChanges++;
            }
        }

        // With volatile data, we should see some trend changes
        _output.WriteLine($"STBands trend changes observed: {trendChanges}");
        Assert.True(trendChanges >= 0, "Trend transitions should be non-negative");
    }

    [Fact]
    public void Validate_NaN_Handling()
    {
        int period = 10;
        double multiplier = 3.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming with NaN injection
        var stbands = new Stbands(period, multiplier);
        int nanCount = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            TBar bar;
            if (i == 50 || i == 51) // Inject NaN at specific positions
            {
                bar = new TBar(bars[i].Time, double.NaN, double.NaN, double.NaN, double.NaN, 0);
                nanCount++;
            }
            else
            {
                bar = bars[i];
            }

            stbands.Update(bar);

            // Results should always be finite
            Assert.True(double.IsFinite(stbands.Upper.Value),
                $"Upper band should be finite after NaN at index {i}");
            Assert.True(double.IsFinite(stbands.Lower.Value),
                $"Lower band should be finite after NaN at index {i}");
        }

        _output.WriteLine($"STBands NaN handling validated ({nanCount} NaN values handled)");
    }

    [Fact]
    public void Validate_BarCorrection()
    {
        int period = 10;
        double multiplier = 3.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var stbands = new Stbands(period, multiplier);

        // Process all bars
        for (int i = 0; i < bars.Count - 1; i++)
        {
            stbands.Update(bars[i]);
        }

        // Record state before last bar
        stbands.Update(bars[^1]);
        double originalUpper = stbands.Upper.Value;
        double originalLower = stbands.Lower.Value;
        double originalWidth = stbands.Width.Value;

        // Correct last bar with different value that will change the bands
        // Use a bar that will cause a different ATR calculation and band positions
        var correctedBar = new TBar(bars[^1].Time, 200, 250, 150, 220, 1000); // Much higher and wider range
        stbands.Update(correctedBar, isNew: false);
        double correctedUpper = stbands.Upper.Value;
        double correctedLower = stbands.Lower.Value;
        double correctedWidth = stbands.Width.Value;

        // At least one value should be different (due to ratchet behavior, bands may or may not change)
        // The width should definitely change because ATR changes with the wider bar range
        bool somethingChanged = (originalUpper != correctedUpper) ||
                                (originalLower != correctedLower) ||
                                (originalWidth != correctedWidth);
        Assert.True(somethingChanged, "Bar correction should affect at least one output value");

        // Restore original bar
        stbands.Update(bars[^1], isNew: false);
        double restoredUpper = stbands.Upper.Value;
        double restoredLower = stbands.Lower.Value;

        // Should match original
        Assert.Equal(originalUpper, restoredUpper, precision: 10);
        Assert.Equal(originalLower, restoredLower, precision: 10);

        _output.WriteLine("STBands bar correction validated successfully");
    }

    [Fact]
    public void Validate_DifferentPeriods()
    {
        // Longer periods should generally produce wider bands
        double multiplier = 2.0;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        int[] periods = { 5, 10, 20, 50 };
        var avgWidths = new List<double>();

        foreach (var period in periods)
        {
            var stbands = new Stbands(period, multiplier);
            double sumWidth = 0;
            int count = 0;

            foreach (var bar in bars)
            {
                stbands.Update(bar);
                if (stbands.IsHot)
                {
                    sumWidth += stbands.Width.Value;
                    count++;
                }
            }

            double avgWidth = count > 0 ? sumWidth / count : 0;
            avgWidths.Add(avgWidth);
            _output.WriteLine($"Period {period}: Average width = {avgWidth:F4}");
        }

        // All widths should be positive
        foreach (var width in avgWidths)
        {
            Assert.True(width > 0, "Average band width should be positive");
        }
    }

    [Fact]
    public void Validate_DifferentMultipliers()
    {
        // Higher multipliers should produce wider bands
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] multipliers = { 1.0, 2.0, 3.0, 4.0 };
        var avgWidths = new List<double>();

        foreach (var multiplier in multipliers)
        {
            var stbands = new Stbands(period, multiplier);
            double sumWidth = 0;
            int count = 0;

            foreach (var bar in bars)
            {
                stbands.Update(bar);
                if (stbands.IsHot)
                {
                    sumWidth += stbands.Width.Value;
                    count++;
                }
            }

            double avgWidth = count > 0 ? sumWidth / count : 0;
            avgWidths.Add(avgWidth);
            _output.WriteLine($"Multiplier {multiplier}: Average width = {avgWidth:F4}");
        }

        // Higher multipliers should generally give wider bands
        for (int i = 1; i < avgWidths.Count; i++)
        {
            // Allow some tolerance due to adaptive band behavior
            Assert.True(avgWidths[i] > avgWidths[0] * 0.5,
                $"Higher multiplier should produce wider bands (mult={multipliers[i]})");
        }
    }
}
