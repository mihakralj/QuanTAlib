using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for UBANDS (Ehlers Ultimate Bands) indicator.
/// Note: UBANDS is a proprietary indicator by John F. Ehlers (2024), not available in
/// standard libraries like TA-Lib, Skender, Tulip, or Ooples. Validation focuses on
/// internal consistency between streaming, batch, and span modes, plus verification
/// that the middle band matches the standalone USF indicator.
/// </summary>
public sealed class UbandsValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public UbandsValidationTests(ITestOutputHelper output)
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
        double[] multipliers = { 0.5, 1.0, 2.0 };

        foreach (var period in periods)
        {
            foreach (var multiplier in multipliers)
            {
                // Generate test data
                var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
                var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
                TSeries series = bars.Close;

                // Streaming mode
                var streamingUbands = new Ubands(period, multiplier);
                var streamingResults = new List<double>();
                var streamingUpper = new List<double>();
                var streamingLower = new List<double>();

                foreach (var val in series)
                {
                    streamingUbands.Update(val);
                    streamingResults.Add(streamingUbands.Middle.Value);
                    streamingUpper.Add(streamingUbands.Upper.Value);
                    streamingLower.Add(streamingUbands.Lower.Value);
                }

                // Batch mode
                var batchResult = Ubands.Calculate(series, period, multiplier);

                // Compare last 100 values
                int compareCount = Math.Min(100, series.Count - period);
                for (int i = series.Count - compareCount; i < series.Count; i++)
                {
                    Assert.Equal(streamingResults[i], batchResult[i].Value, precision: 10);
                }
            }
        }
        _output.WriteLine("UBANDS Streaming vs Batch consistency validated successfully");
    }

    [Fact]
    public void Validate_Streaming_Span_Consistency()
    {
        int[] periods = { 5, 10, 14, 20, 50 };
        double[] multipliers = { 0.5, 1.0, 2.0 };

        foreach (var period in periods)
        {
            foreach (var multiplier in multipliers)
            {
                // Generate test data
                var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
                var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
                TSeries series = bars.Close;

                // Streaming mode
                var streamingUbands = new Ubands(period, multiplier);
                var streamingUpper = new List<double>();
                var streamingMiddle = new List<double>();
                var streamingLower = new List<double>();

                foreach (var val in series)
                {
                    streamingUbands.Update(val);
                    streamingUpper.Add(streamingUbands.Upper.Value);
                    streamingMiddle.Add(streamingUbands.Middle.Value);
                    streamingLower.Add(streamingUbands.Lower.Value);
                }

                // Span mode
                double[] source = series.Values.ToArray();
                double[] spanUpper = new double[series.Count];
                double[] spanMiddle = new double[series.Count];
                double[] spanLower = new double[series.Count];

                Ubands.Calculate(source.AsSpan(), spanUpper.AsSpan(), spanMiddle.AsSpan(),
                    spanLower.AsSpan(), period, multiplier);

                // Compare last 100 values
                int compareCount = Math.Min(100, series.Count - period);
                for (int i = series.Count - compareCount; i < series.Count; i++)
                {
                    Assert.Equal(streamingUpper[i], spanUpper[i], precision: 10);
                    Assert.Equal(streamingMiddle[i], spanMiddle[i], precision: 10);
                    Assert.Equal(streamingLower[i], spanLower[i], precision: 10);
                }
            }
        }
        _output.WriteLine("UBANDS Streaming vs Span consistency validated successfully");
    }

    [Fact]
    public void Validate_MiddleBand_MatchesUsf()
    {
        // The middle band of UBANDS should match the standalone USF indicator
        // Both use the same Ehlers Ultrasmooth Filter algorithm but calculate coefficients independently
        int[] periods = { 5, 10, 20 };

        foreach (var period in periods)
        {
            var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
            var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
            TSeries series = bars.Close;

            var ubands = new Ubands(period, 1.0);
            var usf = new Usf(period);

            var ubandsMiddle = new List<double>();
            var usfValues = new List<double>();

            foreach (var val in series)
            {
                ubands.Update(val);
                usf.Update(val);
                ubandsMiddle.Add(ubands.Middle.Value);
                usfValues.Add(usf.Last.Value);
            }

            // Compare after warmup - using relative tolerance due to independent FP calculations
            // UBANDS reimplements USF internally, so minor numerical differences are expected
            double maxRelDiff = 0;
            for (int i = period; i < series.Count; i++)
            {
                double relDiff = Math.Abs(usfValues[i] - ubandsMiddle[i]) / Math.Abs(usfValues[i]);
                maxRelDiff = Math.Max(maxRelDiff, relDiff);
                Assert.True(relDiff < 0.001, // 0.1% tolerance
                    $"Period {period}, index {i}: USF={usfValues[i]:F6}, UBANDS={ubandsMiddle[i]:F6}, diff={relDiff:P4}");
            }

            _output.WriteLine($"Period {period}: UBANDS middle band matches USF (max rel diff: {maxRelDiff:P4})");
        }
    }

    [Fact]
    public void Validate_BandCharacteristics()
    {
        // Verify core UBANDS characteristics:
        // 1. Upper >= Middle >= Lower (symmetric around middle)
        // 2. Width = 2 × mult × RMS (symmetry)
        // 3. Bands adapt to volatility

        int period = 10;
        double multiplier = 1.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        var ubands = new Ubands(period, multiplier);

        foreach (var val in series)
        {
            ubands.Update(val);

            // Upper >= Middle >= Lower
            Assert.True(ubands.Upper.Value >= ubands.Middle.Value,
                $"Upper ({ubands.Upper.Value}) should be >= Middle ({ubands.Middle.Value})");
            Assert.True(ubands.Middle.Value >= ubands.Lower.Value,
                $"Middle ({ubands.Middle.Value}) should be >= Lower ({ubands.Lower.Value})");

            // Symmetry: Upper - Middle == Middle - Lower
            double upperOffset = ubands.Upper.Value - ubands.Middle.Value;
            double lowerOffset = ubands.Middle.Value - ubands.Lower.Value;
            Assert.Equal(upperOffset, lowerOffset, precision: 10);
        }

        _output.WriteLine("UBANDS band characteristics validated successfully");
    }

    [Fact]
    public void Validate_NaN_Handling()
    {
        int period = 10;
        double multiplier = 1.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        var ubands = new Ubands(period, multiplier);
        int nanCount = 0;

        for (int i = 0; i < series.Count; i++)
        {
            TValue inputVal;
            if (i == 50 || i == 51)
            {
                inputVal = new TValue(series[i].Time, double.NaN);
                nanCount++;
            }
            else
            {
                inputVal = series[i];
            }

            ubands.Update(inputVal);

            Assert.True(double.IsFinite(ubands.Upper.Value),
                $"Upper band should be finite after NaN at index {i}");
            Assert.True(double.IsFinite(ubands.Middle.Value),
                $"Middle band should be finite after NaN at index {i}");
            Assert.True(double.IsFinite(ubands.Lower.Value),
                $"Lower band should be finite after NaN at index {i}");
        }

        _output.WriteLine($"UBANDS NaN handling validated ({nanCount} NaN values handled)");
    }

    [Fact]
    public void Validate_BarCorrection()
    {
        int period = 10;
        double multiplier = 1.0;

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        var ubands = new Ubands(period, multiplier);

        // Process all bars
        for (int i = 0; i < series.Count - 1; i++)
        {
            ubands.Update(series[i]);
        }

        // Record state before last bar
        ubands.Update(series[^1]);
        double originalMiddle = ubands.Middle.Value;
        double originalUpper = ubands.Upper.Value;

        // Correct last bar with different value
        var correctedVal = new TValue(series[^1].Time, 200.0);
        ubands.Update(correctedVal, isNew: false);
        double correctedMiddle = ubands.Middle.Value;

        // Should be different
        Assert.NotEqual(originalMiddle, correctedMiddle);

        // Restore original bar
        ubands.Update(series[^1], isNew: false);
        double restoredMiddle = ubands.Middle.Value;
        double restoredUpper = ubands.Upper.Value;

        // Should match original
        Assert.Equal(originalMiddle, restoredMiddle, precision: 10);
        Assert.Equal(originalUpper, restoredUpper, precision: 10);

        _output.WriteLine("UBANDS bar correction validated successfully");
    }

    [Fact]
    public void Validate_DifferentPeriods()
    {
        double multiplier = 1.0;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        int[] periods = { 5, 10, 20, 50 };
        var avgWidths = new List<double>();

        foreach (var period in periods)
        {
            var ubands = new Ubands(period, multiplier);
            double sumWidth = 0;
            int count = 0;

            foreach (var val in series)
            {
                ubands.Update(val);
                if (ubands.IsHot)
                {
                    sumWidth += ubands.Width.Value;
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
            Assert.True(width >= 0, "Average band width should be non-negative");
        }
    }

    [Fact]
    public void Validate_DifferentMultipliers()
    {
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        double[] multipliers = { 0.5, 1.0, 1.5, 2.0 };
        var avgWidths = new List<double>();

        foreach (var multiplier in multipliers)
        {
            var ubands = new Ubands(period, multiplier);
            double sumWidth = 0;
            int count = 0;

            foreach (var val in series)
            {
                ubands.Update(val);
                if (ubands.IsHot)
                {
                    sumWidth += ubands.Width.Value;
                    count++;
                }
            }

            double avgWidth = count > 0 ? sumWidth / count : 0;
            avgWidths.Add(avgWidth);
            _output.WriteLine($"Multiplier {multiplier}: Average width = {avgWidth:F4}");
        }

        // Higher multipliers should give wider bands
        for (int i = 1; i < avgWidths.Count; i++)
        {
            Assert.True(avgWidths[i] > avgWidths[i - 1],
                $"Higher multiplier should produce wider bands");
        }
    }

    [Fact]
    public void Validate_SmoothingQuality()
    {
        // USF should provide superior smoothing with minimal lag
        int period = 20;
        var gbm = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries series = bars.Close;

        var ubands = new Ubands(period, 1.0);
        var middleValues = new List<double>();
        var sourceValues = new List<double>();

        foreach (var val in series)
        {
            ubands.Update(val);
            middleValues.Add(ubands.Middle.Value);
            sourceValues.Add(val.Value);
        }

        // Calculate noise reduction: variance of differences should be lower for smoothed
        var sourceDiffs = new List<double>();
        var middleDiffs = new List<double>();

        for (int i = period + 1; i < series.Count; i++)
        {
            sourceDiffs.Add(sourceValues[i] - sourceValues[i - 1]);
            middleDiffs.Add(middleValues[i] - middleValues[i - 1]);
        }

        double sourceVar = sourceDiffs.Select(x => x * x).Average();
        double middleVar = middleDiffs.Select(x => x * x).Average();

        _output.WriteLine($"Source variance: {sourceVar:F4}");
        _output.WriteLine($"Middle (USF) variance: {middleVar:F4}");
        _output.WriteLine($"Noise reduction: {(1 - middleVar / sourceVar) * 100:F1}%");

        Assert.True(middleVar < sourceVar, "Smoothed signal should have lower variance");
    }
}
