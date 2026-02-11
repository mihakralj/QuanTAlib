using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Qstick indicator.
/// Validates against manual formula calculations since Qstick is not
/// available in TA-Lib, Skender, Tulip, or Ooples.
/// </summary>
public sealed class QstickValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public QstickValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Mathematical Correctness Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ManualCalculation_MatchesFormula()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        // Create known bars
        bars.Add(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));  // diff = 5
        bars.Add(new TBar(time.AddMinutes(1).Ticks, 100.0, 108.0, 92.0, 97.0, 1000));  // diff = -3
        bars.Add(new TBar(time.AddMinutes(2).Ticks, 100.0, 106.0, 94.0, 104.0, 1000));  // diff = 4
        bars.Add(new TBar(time.AddMinutes(3).Ticks, 100.0, 107.0, 93.0, 98.0, 1000));  // diff = -2
        bars.Add(new TBar(time.AddMinutes(4).Ticks, 100.0, 109.0, 91.0, 106.0, 1000));  // diff = 6

        var qstick = new Qstick(5);
        for (int i = 0; i < bars.Count; i++)
        {
            qstick.Update(bars[i]);
        }

        // Expected: SMA of (5, -3, 4, -2, 6) = 10/5 = 2.0
        Assert.Equal(2.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void ManualCalculation_Period3()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        // Bar 1: diff = 8
        qstick.Update(new TBar(time.Ticks, 100.0, 115.0, 95.0, 108.0, 1000));

        // Bar 2: diff = -4
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 102.0, 90.0, 96.0, 1000));

        // Bar 3: diff = 6
        var result = qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 106.0, 1000));

        // Expected: SMA(8, -4, 6) = 10/3 ≈ 3.333
        Assert.Equal(10.0 / 3.0, result.Value, 10);
    }

    [Fact]
    public void ManualCalculation_Period7()
    {
        var qstick = new Qstick(7);
        var time = DateTime.UtcNow;

        double[] diffs = { 5, -3, 4, -2, 6, -1, 3 };

        for (int i = 0; i < diffs.Length; i++)
        {
            double open = 100.0;
            double close = 100.0 + diffs[i];
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, open, 110.0, 90.0, close, 1000));
        }

        // Expected: SMA of 5, -3, 4, -2, 6, -1, 3 = 12/7 ≈ 1.714
        double expectedSum = 5 - 3 + 4 - 2 + 6 - 1 + 3;  // = 12
        Assert.Equal(expectedSum / 7.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void EmaCalculation_MatchesFormula()
    {
        var qstick = new Qstick(3, useEma: true);  // alpha = 2/(3+1) = 0.5
        var time = DateTime.UtcNow;

        // Bar 1: diff = 10
        qstick.Update(new TBar(time.Ticks, 100.0, 115.0, 95.0, 110.0, 1000));
        Assert.Equal(10.0, qstick.Last.Value, 10);

        // Bar 2: diff = -6, EMA = 0.5 * -6 + 0.5 * 10 = 2.0
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 102.0, 90.0, 94.0, 1000));
        Assert.Equal(2.0, qstick.Last.Value, 10);

        // Bar 3: diff = 4, EMA = 0.5 * 4 + 0.5 * 2 = 3.0
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 108.0, 95.0, 104.0, 1000));
        Assert.Equal(3.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void EmaCalculation_Period5()
    {
        var qstick = new Qstick(5, useEma: true);  // alpha = 2/(5+1) = 1/3
        var time = DateTime.UtcNow;
        double alpha = 2.0 / 6.0;

        // Bar 1: diff = 6
        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 106.0, 1000));
        double expectedEma = 6.0;
        Assert.Equal(expectedEma, qstick.Last.Value, 10);

        // Bar 2: diff = 3, EMA = alpha * 3 + (1-alpha) * 6-
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 108.0, 96.0, 103.0, 1000));
        expectedEma = alpha * 3 + (1 - alpha) * expectedEma;
        Assert.Equal(expectedEma, qstick.Last.Value, 10);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Edge Case Validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ZeroCrossing_IdentifiesCorrectly()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        // Start bullish
        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 106.0, 1000));  // +6
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 108.0, 92.0, 104.0, 1000));  // +4
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 106.0, 94.0, 102.0, 1000));  // +2

        Assert.True(qstick.Last.Value > 0);

        // Shift to bearish
        qstick.Update(new TBar(time.AddMinutes(3).Ticks, 100.0, 105.0, 90.0, 92.0, 1000));  // -8
        qstick.Update(new TBar(time.AddMinutes(4).Ticks, 100.0, 104.0, 88.0, 90.0, 1000));  // -10
        qstick.Update(new TBar(time.AddMinutes(5).Ticks, 100.0, 103.0, 86.0, 88.0, 1000));  // -12

        Assert.True(qstick.Last.Value < 0);
    }

    [Fact]
    public void LargeGaps_HandledCorrectly()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        // Gap up scenario - previous close has no effect on body calculation
        qstick.Update(new TBar(time.Ticks, 100.0, 105.0, 95.0, 103.0, 1000));  // diff = 3
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 110.0, 118.0, 108.0, 115.0, 1000));  // diff = 5 (gap up)
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 120.0, 125.0, 118.0, 122.0, 1000));  // diff = 2 (gap up)

        // SMA = (3 + 5 + 2) / 3 = 10/3 ≈ 3.333
        Assert.Equal(10.0 / 3.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void AlternatingBullishBearish_AveragesToNearZero()
    {
        var qstick = new Qstick(4);
        var time = DateTime.UtcNow;

        // Alternating pattern
        qstick.Update(new TBar(time.Ticks, 100.0, 110.0, 95.0, 105.0, 1000));  // +5
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 105.0, 90.0, 95.0, 1000));  // -5
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 110.0, 95.0, 105.0, 1000));  // +5
        qstick.Update(new TBar(time.AddMinutes(3).Ticks, 100.0, 105.0, 90.0, 95.0, 1000));  // -5

        // SMA = (5 - 5 + 5 - 5) / 4 = 0
        Assert.Equal(0.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void AllDoji_ReturnsZero()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, 100.0, 105.0, 95.0, 100.0, 1000));  // diff = 0
        }

        Assert.Equal(0.0, qstick.Last.Value, 10);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Stability Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LongSeries_MaintainsStability()
    {
        var qstick = new Qstick(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var result = qstick.Update(_data.Bars[i]);
            results.Add(result.Value);
        }

        // Verify no NaN or Infinity after warmup
        for (int i = 14; i < results.Count; i++)
        {
            Assert.True(double.IsFinite(results[i]), $"Result at index {i} is not finite: {results[i]}");
        }
    }

    [Fact]
    public void BatchVsStreaming_MatchesExactly()
    {
        // Batch processing
        var batchResults = Qstick.Batch(_data.Bars, period: 14);

        // Streaming processing
        var streamQstick = new Qstick(14);
        var streamResults = new List<double>();
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var result = streamQstick.Update(_data.Bars[i]);
            streamResults.Add(result.Value);
        }

        // Compare
        Assert.Equal(batchResults.Count, streamResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(batchResults.Values[i], streamResults[i], 12);
        }
    }

    [Fact]
    public void SmaVsEma_ConvergesOverLongPeriod()
    {
        // With constant input, SMA and EMA should converge
        var smaQstick = new Qstick(10, useEma: false);
        var emaQstick = new Qstick(10, useEma: true);
        var time = DateTime.UtcNow;

        // Feed constant bars (close - open = 5)
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i).Ticks, 100.0, 110.0, 95.0, 105.0, 1000);
            smaQstick.Update(bar);
            emaQstick.Update(bar);
        }

        // Both should converge to 5.0 with constant input
        Assert.Equal(5.0, smaQstick.Last.Value, 10);
        Assert.Equal(5.0, emaQstick.Last.Value, 4);  // EMA converges slower
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Period Boundary Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Period1_ReturnsDiffDirectly()
    {
        var qstick = new Qstick(1);
        var time = DateTime.UtcNow;

        var bar = new TBar(time.Ticks, 100.0, 110.0, 95.0, 107.0, 1000);
        var result = qstick.Update(bar);

        Assert.Equal(7.0, result.Value, 10);  // close - open = 107 - 100 = 7
    }

    [Fact]
    public void LargePeriod_CalculatesCorrectly()
    {
        var qstick = new Qstick(50);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var result = qstick.Update(_data.Bars[i]);
            results.Add(result.Value);
        }

        // Verify indicator is hot after warmup
        Assert.True(qstick.IsHot);

        // Verify values are finite after warmup
        for (int i = 50; i < results.Count; i++)
        {
            Assert.True(double.IsFinite(results[i]), $"Result at index {i} is not finite");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Rolling Window Tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RollingWindow_DropsOldestValue()
    {
        var qstick = new Qstick(3);
        var time = DateTime.UtcNow;

        // Fill window: +10, +10, +10
        qstick.Update(new TBar(time.Ticks, 100.0, 115.0, 95.0, 110.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(1).Ticks, 100.0, 115.0, 95.0, 110.0, 1000));
        qstick.Update(new TBar(time.AddMinutes(2).Ticks, 100.0, 115.0, 95.0, 110.0, 1000));

        Assert.Equal(10.0, qstick.Last.Value, 10);

        // Add -20 (replaces oldest +10)
        qstick.Update(new TBar(time.AddMinutes(3).Ticks, 100.0, 105.0, 75.0, 80.0, 1000));

        // Window is now: +10, +10, -20 → SMA = 0/3 = 0
        Assert.Equal(0.0, qstick.Last.Value, 10);
    }

    [Fact]
    public void RollingWindow_MaintainsCorrectSum()
    {
        var qstick = new Qstick(5);
        var time = DateTime.UtcNow;

        // Create predictable pattern
        double[] diffs = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        for (int i = 0; i < diffs.Length; i++)
        {
            double open = 100.0;
            double close = 100.0 + diffs[i];
            qstick.Update(new TBar(time.AddMinutes(i).Ticks, open, 110.0, 90.0, close, 1000));

            if (i >= 4)  // After warmup
            {
                // Expected: SMA of last 5 values
                double expectedSum = 0;
                for (int j = i - 4; j <= i; j++)
                {
                    expectedSum += diffs[j];
                }
                Assert.Equal(expectedSum / 5.0, qstick.Last.Value, 10);
            }
        }
    }
}
