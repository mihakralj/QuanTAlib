using TALib;
using QuanTAlib.Tests;

namespace QuanTAlib;

/// <summary>
/// Validation tests for HtTrendmode against TA-Lib reference implementation.
/// Note: TA-Lib's HT_TRENDMODE is the reference for this indicator.
/// </summary>
public sealed class HtTrendmodeValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public HtTrendmodeValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void HtTrendmode_OutputsValidBinaryValues()
    {
        // Arrange
        var indicator = new HtTrendmode();
        var results = new List<double>();
        var closeSpan = _data.GetCloseSpan();
        var timestamps = _data.Timestamps.Span;

        // Act - Process data
        for (int i = 0; i < _data.Count; i++)
        {
            var result = indicator.Update(new TValue(timestamps[i], closeSpan[i]));
            results.Add(result.Value);
        }

        // Assert - After warmup, all values should be 0 or 1
        for (int i = 50; i < results.Count; i++)
        {
            double value = results[i];
            Assert.True(value == 0.0 || value == 1.0,
                $"TrendMode at index {i} should be 0 or 1, got {value}");
        }
    }

    [Fact]
    public void HtTrendmode_SmoothPeriod_InValidRange()
    {
        // Arrange
        var indicator = new HtTrendmode();

        // Act - Process with sinusoidal data
        for (int i = 0; i < 200; i++)
        {
            double value = 100.0 + Math.Sin(i * 0.2) * 10.0 + Math.Sin(i * 0.05) * 5.0;
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value));
        }

        // Assert - SmoothPeriod should be in valid range [6, 50]
        double smoothPeriod = indicator.SmoothPeriod;
        Assert.True(smoothPeriod >= 6.0 && smoothPeriod <= 50.0,
            $"SmoothPeriod {smoothPeriod} should be between 6 and 50");
    }

    [Fact]
    public void HtTrendmode_InstPeriod_Positive()
    {
        // Arrange
        var indicator = new HtTrendmode();

        // Act
        for (int i = 0; i < 200; i++)
        {
            double value = 100.0 + Math.Sin(i * 0.15) * 8.0;
            indicator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), value));
        }

        // Assert
        double instPeriod = indicator.InstPeriod;
        Assert.True(instPeriod > 0, $"InstPeriod should be positive, got {instPeriod}");
    }

    [Fact]
    public void HtTrendmode_StreamingVsBatch_Equal()
    {
        // Arrange
        var streamingIndicator = new HtTrendmode();
        var streamingResults = new List<double>();
        var closeSpan = _data.GetCloseSpan();
        var timestamps = _data.Timestamps.Span;

        // Act - Streaming
        var series = new TSeries();
        for (int i = 0; i < _data.Count; i++)
        {
            series.Add(timestamps[i], closeSpan[i]);
            var result = streamingIndicator.Update(new TValue(timestamps[i], closeSpan[i]));
            streamingResults.Add(result.Value);
        }

        // Act - Batch
        var batchResult = HtTrendmode.Batch(series);

        // Assert
        Assert.Equal(streamingResults.Count, batchResult.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i]);
        }
    }

    [Fact]
    public void HtTrendmode_TrendModeLogic_TALibAlgorithm()
    {
        // Arrange - Our implementation now follows TA-Lib's Ehlers algorithm
        var indicator = new HtTrendmode();
        var closeSpan = _data.GetCloseSpan();
        var timestamps = _data.Timestamps.Span;

        // Act - Prime the indicator with enough data
        for (int i = 0; i < 100; i++)
        {
            indicator.Update(new TValue(timestamps[i], closeSpan[i]));
        }

        // Assert - TA-Lib TrendMode: binary 0 or 1, using multi-criteria:
        // 1. SineWave crossings reset daysInTrend
        // 2. daysInTrend >= 0.5 * smoothPeriod → trending
        // 3. Phase rate check (normal range → cycle mode)
        // 4. Price-trendline deviation ≥1.5% → trend override
        int trendMode = indicator.TrendMode;
        Assert.True(trendMode == 0 || trendMode == 1, $"TrendMode should be 0 or 1, got {trendMode}");

        // Verify DaysInTrend property works
        Assert.True(indicator.DaysInTrend >= 0, "DaysInTrend should be non-negative");
    }

    /// <summary>
    /// Tests TA-Lib validation. Our implementation now follows TA-Lib's Ehlers algorithm.
    /// </summary>
    [Fact]
    public void MatchesTalib()
    {
        // Arrange
        var indicator = new HtTrendmode();
        var results = new List<double>();
        var closeSpan = _data.GetCloseSpan();
        var timestamps = _data.Timestamps.Span;

        // Act - Process data
        for (int i = 0; i < _data.Count; i++)
        {
            var result = indicator.Update(new TValue(timestamps[i], closeSpan[i]));
            results.Add(result.Value);
        }

        // Get TA-Lib results
        double[] inReal = closeSpan.ToArray();
        int[] outInteger = new int[inReal.Length];

        var retCode = Functions.HtTrendMode(inReal, 0..^0, outInteger, out var outRange);
        Assert.Equal(Core.RetCode.Success, retCode);

        // Compare after warmup
        int lookback = Functions.HtTrendModeLookback();
        double[] talibResults = outInteger.Select(x => (double)x).ToArray();
        ValidationHelper.VerifyData(results, talibResults, outRange, lookback);
    }

    [Fact]
    public void HtTrendmode_DeterministicOutput()
    {
        // Arrange
        var indicator1 = new HtTrendmode();
        var indicator2 = new HtTrendmode();
        var closeSpan = _data.GetCloseSpan();
        var timestamps = _data.Timestamps.Span;

        // Act - Same data, same results
        var results1 = new List<double>();
        var results2 = new List<double>();

        for (int i = 0; i < _data.Count; i++)
        {
            var r1 = indicator1.Update(new TValue(timestamps[i], closeSpan[i]));
            var r2 = indicator2.Update(new TValue(timestamps[i], closeSpan[i]));
            results1.Add(r1.Value);
            results2.Add(r2.Value);
        }

        // Assert - Deterministic
        for (int i = 0; i < results1.Count; i++)
        {
            Assert.Equal(results1[i], results2[i]);
        }
    }
}
